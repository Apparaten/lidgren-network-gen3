using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorProviderBase<TServiceContract, TSerializedSendType> : CommunicatorBase<TServiceContract, TSerializedSendType> where TServiceContract : ICallbackContract
    {
        protected IAuthenticator Authenticator;
        protected string[] RequiredAuthenticationRoles;

        private List<AuthenticationResult> AuthenticationResults { get; set; } = new List<AuthenticationResult>();

        protected CommunicatorProviderBase(NetPeerConfiguration configuration,ConverterBase<TSerializedSendType> converter, IAuthenticator authenticator = null, string[] requiredAuthenticationRoles = null)
        {
            Converter = converter;
            if (authenticator != null)
            {
                RequiredAuthenticationRoles = requiredAuthenticationRoles;
                configuration.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            }
            NetConnector = new NetServer(configuration);

            Authenticator = authenticator;
            Initialize(typeof(ICallbackContract), typeof(IProviderContract));
        }

        public virtual void StartService()
        {
            NetConnector.Start();
        }

        public override void Tick(int interval)
        {
            while (AuthenticationResults.Count > 0)
            {
                var result = AuthenticationResults[0];
                if (AuthenticationResults[0].Success)
                {
                    result.Connection.Approve();
                    OnAuthenticationApproved(AuthenticationResults[0]);
                }
                else
                {
                    result.Connection.Deny();
                    OnAuthenticationDenied(AuthenticationResults[0]);
                }
                AuthenticationResults.Remove(AuthenticationResults[0]);
            }
            RunTasks();

            NetIncomingMessage msg;
            while ((msg = NetConnector.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        break;
                    case NetIncomingMessageType.Error:
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var change = (NetConnectionStatus)msg.ReadByte();
                        OnConnectionStatusChanged(change,msg.SenderConnection);
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        ConnectionApproval(msg);
                        break;
                    case NetIncomingMessageType.Data:
                        FilterMessage(msg);
                        break;
                    case NetIncomingMessageType.Receipt:
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        break;
                    case NetIncomingMessageType.DiscoveryResponse:
                        break;
                    case NetIncomingMessageType.NatIntroductionSuccess:
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        break;
                    default:
                        Log("Unhandled type: " + msg.MessageType);
                        break;
                }
                NetConnector.Recycle(msg);
            }

            
            Task.Delay(interval).Wait();
        }

        protected override void Log(object message, [CallerMemberName]string caller = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]\t[{caller}]\t{message.ToString()}");
        }

        protected virtual void ConnectionApproval(NetIncomingMessage msg)
        {
            var connection = msg.SenderConnection;
            var asd = Authenticator.Authenticate(msg.ReadString(), msg.ReadString())
                .ContinueWith((approval) =>
                {
                    var authentication = approval.Result;
                    authentication.Connection = connection;
                    AuthenticationResults.Add(authentication);
                });
            AddRunningTask(asd);
        }

        
        protected virtual void OnAuthenticationApproved(AuthenticationResult authenticationResult)
        {

        }
        protected virtual void OnAuthenticationDenied(AuthenticationResult authenticationResult)
        {

        }
    }
}

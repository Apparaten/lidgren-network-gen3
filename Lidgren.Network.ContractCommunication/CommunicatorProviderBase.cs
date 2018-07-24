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
    public abstract class CommunicatorProviderBase<TServiceContract, TSerializedSendType> : CommunicatorBase<TServiceContract, TSerializedSendType> where TServiceContract : ICallbackContract,new()
    {
        protected IAuthenticator Authenticator;
        protected string[] RequiredAuthenticationRoles;

        private List<AuthenticationResult> AuthenticationResults { get; set; } = new List<AuthenticationResult>();
        private Stopwatch _tickWatch = new Stopwatch();
        protected CommunicatorProviderBase(NetPeerConfiguration configuration,ConverterBase<TSerializedSendType> converter, IAuthenticator authenticator = null, string[] requiredAuthenticationRoles = null)
        {
            Configuration = configuration;
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
            Log($"Service started on port: {Configuration.Port}");
        }

        public override void Tick(int interval)
        {
            _tickWatch.Restart();
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
            _tickWatch.Stop();
            var elapsedTime = (int)_tickWatch.ElapsedMilliseconds;
            var finalInterval = interval - elapsedTime;
            if (finalInterval > 0)
            {
                Task.Delay(finalInterval).Wait();
            }
            else
            {
                Console.WriteLine($"¤    elapsed: {elapsedTime} vs interval:{interval} = {finalInterval}");
            }
        }

        protected override void Log(object message, [CallerMemberName]string caller = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]\t[{caller}]\t{message.ToString()}");
        }

        protected virtual void ConnectionApproval(NetIncomingMessage msg)
        {
            var connection = msg.SenderConnection;
            var authTask = Authenticator.Authenticate(msg.ReadString(), msg.ReadString())
                .ContinueWith((approval) =>
                {
                    var authentication = approval.Result;
                    authentication.Connection = connection;
                    AuthenticationResults.Add(authentication);
                });
            AddRunningTask(authTask);
        }

        
        protected virtual void OnAuthenticationApproved(AuthenticationResult authenticationResult)
        {

        }
        protected virtual void OnAuthenticationDenied(AuthenticationResult authenticationResult)
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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

        public override void Tick()
        {
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
                        OnConnectionStatusChanged?.Invoke(change);
                        Console.WriteLine(msg.ReadString());
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        OnConnectionApproval(msg);
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
                        Console.WriteLine("Unhandled type: " + msg.MessageType);
                        break;
                }
                NetConnector.Recycle(msg);
            }

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
        }



        protected virtual void OnConnectionApproval(NetIncomingMessage msg)
        {
            var connection = msg.SenderConnection;
            Authenticator.Authenticate(msg.ReadString(), msg.ReadString())
                .ContinueWith((approval) =>
                {
                    var authentication = approval.Result;
                    authentication.Connection = connection;
                    AuthenticationResults.Add(authentication);
                });
        }

        protected virtual void OnAuthenticationApproved(AuthenticationResult authenticationResult)
        {

        }
        protected virtual void OnAuthenticationDenied(AuthenticationResult authenticationResult)
        {

        }
    }
}

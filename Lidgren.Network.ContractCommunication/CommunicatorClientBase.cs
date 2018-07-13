using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorClientBase<TServiceContract, TSerializedSendType> : CommunicatorBase<TServiceContract,TSerializedSendType> where TServiceContract : IProviderContract
    {
        private string _host;
        private int _port;
        protected CommunicatorClientBase(NetPeerConfiguration configuration,ConverterBase<TSerializedSendType> converter, string host, int port)
        {
            Converter = converter;
            _host = host;
            _port = port;
            NetConnector = new NetClient(configuration);
            Initialize(typeof(IProviderContract), typeof(ICallbackContract));
            NetConnector.Start();
        }
        public virtual void Connect(string user, string password)
        {
            var msg = NetConnector.CreateMessage();
            msg.Write(user);
            msg.Write(password);
            NetConnector.Connect(_host, _port, msg);
        }
        public override void Tick(int interval)
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
                        Console.WriteLine("connectionApproval Client...");
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
            RunTasks();
            Task.Delay(interval).Wait();
        }
    }
}

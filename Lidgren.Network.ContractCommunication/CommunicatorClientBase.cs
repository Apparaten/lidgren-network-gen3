using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorClientBase : CommunicatorBase
    {
        private string _host;
        private int _port;
        protected CommunicatorClientBase(NetPeerConfiguration configuration,ConverterBase converter, string host, int port)
        {
            Converter = converter;
            _host = host;
            _port = port;
            NetConnector = new NetClient(configuration);
        }
        public virtual void Connect(string user, string password,[CallerMemberName]string caller = "")
        {
            Log("TRYING TO CONNECT - "+caller);
            var status = NetConnector.Status;
            switch (status)
            {
                case NetPeerStatus.NotRunning:
                    NetConnector.Start();
                    Log("Networking Thread Started");
                    break;
                case NetPeerStatus.Starting:
                    break;
                case NetPeerStatus.Running:
                    break;
                case NetPeerStatus.ShutdownRequested:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
                        var connectionResult = (NetConnectionResult) msg.ReadByte();
                        OnConnectionStatusChanged(change,connectionResult,msg.SenderConnection);
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
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

        public override void CloseConnection()
        {
            NetConnector.Connections.FirstOrDefault()?.Disconnect();
            NetConnector.Shutdown("shutdown");
        }
        public void Call<TContract, TMethod>(Expression<Func<TContract, TMethod>> selector,
            params object[] args) =>
            CreateAndCall(typeof(TContract), (UnaryExpression)selector.Body, args, null);

        public Task<TReturn> CallAsync<TContract, TMethod, TReturn>(
            Expression<Func<TContract, TMethod>> selector, params object[] args) =>
            CreateAndCallAsync<TReturn>(typeof(TContract), (UnaryExpression) selector.Body, args, null);
    }
}

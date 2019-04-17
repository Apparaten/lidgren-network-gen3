using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network.Encryption;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorClientBase<TServiceContract> : CommunicatorBase<TServiceContract> where TServiceContract : IProviderContract,new ()
    {
        private string _host;
        private int _port;
        protected CommunicatorClientBase(NetPeerConfiguration configuration,ConverterBase converter, string host, int port)
        {
            Converter = converter;
            _host = host;
            _port = port;
            NetConnector = new NetClient(configuration);
            NetEncryptor = new ClientRsaTripleDesNetEncryptor(NetConnector,1024);
            Initialize(typeof(IProviderContract), typeof(ICallbackContract));
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
            var cspBlob = ((ClientRsaTripleDesNetEncryptor) NetEncryptor).ExportHandShakeKey();
            msg.Write(cspBlob.Length);
            msg.Write(cspBlob);
            Log(msg.LengthBytes);
            ((ClientRsaTripleDesNetEncryptor)NetEncryptor).EncryptHail(msg);
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
                        if (change == NetConnectionStatus.Connected)
                        {
                            var hailMessage = msg.SenderConnection.RemoteHailMessage;
                            ((ClientRsaTripleDesNetEncryptor)NetEncryptor).DecryptHail(hailMessage);
                            var keyLength = hailMessage.ReadInt32();
                            var key = hailMessage.ReadBytes(keyLength);
                            var ivLength = hailMessage.ReadInt32();
                            var iv = hailMessage.ReadBytes(ivLength);
                            ((ClientRsaTripleDesNetEncryptor)NetEncryptor).ImportRemoteTripleDes(key,iv);
                        }
                        OnConnectionStatusChanged(change,connectionResult,msg.SenderConnection);
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        break;
                    case NetIncomingMessageType.Data:
                        NetEncryptor.Decrypt(msg);
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
    }
}

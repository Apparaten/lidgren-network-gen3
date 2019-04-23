using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.Encryption
{
    public interface INetEncryptor
    {
        void Encrypt(NetOutgoingMessage msg, NetConnection reciever = null);
        void Decrypt(NetIncomingMessage msg);
    }

    public class ServerTripleDesNetEncryptor : INetEncryptor
    {
        public Dictionary<NetConnection, TripleDESCryptoServiceProvider> ConnectionCryptoProviders { get; set; } = new Dictionary<NetConnection, TripleDESCryptoServiceProvider>();

        private NetPeer _peer;
        private int _dwKeySize;
        public ServerTripleDesNetEncryptor(NetPeer peer, int dwKeySize)
        {
            _dwKeySize = dwKeySize;
            _peer = peer;
        }

        public void ImportKeyForConnection(NetConnection connection, byte[] key, byte[] iv)
        {
            var provider = new TripleDESCryptoServiceProvider();
            if (ConnectionCryptoProviders.ContainsKey(connection))
            {
                ConnectionCryptoProviders[connection] = provider;
            }
            else
            {
                ConnectionCryptoProviders.Add(connection, provider);
            }
            provider.Key = key;
            provider.IV = iv;
        }
        public void Encrypt(NetOutgoingMessage msg, NetConnection reciever = null)
        {
            ConnectionCryptoProviders.TryGetValue(reciever, out var provider);
            if(provider == null)
                return;
            int unEncLenBits = msg.LengthBits;

            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, provider.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(msg.m_data, 0, msg.LengthBytes);
            cs.Close();

            // get results
            var arr = ms.ToArray();
            ms.Close();

            msg.EnsureBufferSize((arr.Length + 4) * 8);
            msg.LengthBits = 0; // reset write pointer
            msg.Write((uint)unEncLenBits);
            msg.Write(arr);
            msg.LengthBits = (arr.Length + 4) * 8;

        }

        public void Decrypt(NetIncomingMessage msg)
        {
            ConnectionCryptoProviders.TryGetValue(msg.SenderConnection, out var provider);
            if(provider == null)
                return;
            int unEncLenBits = (int)msg.ReadUInt32();
            
            var ms = new MemoryStream(msg.m_data, msg.PositionInBytes, msg.LengthBytes - msg.PositionInBytes);
            //var ms = new MemoryStream(msg.Data);
            var cs = new CryptoStream(ms, provider.CreateDecryptor(), CryptoStreamMode.Read);

            var byteLen = NetUtility.BytesToHoldBits(unEncLenBits);
            //var result = _peer.GetStorage(byteLen);
            var result = new byte[byteLen];
            cs.Read(result, 0, byteLen);
            cs.Close();

            // TODO: recycle existing msg

            msg.m_data = result;
            msg.m_bitLength = unEncLenBits;
            msg.m_readPosition = 0;
        }
    }
    public class ClientTripleDesNetEncryptor : INetEncryptor
    {
        private TripleDESCryptoServiceProvider _connectionCryptoProvider;
        private NetPeer _peer;
        private int _dwKeySize;
        public ClientTripleDesNetEncryptor(NetPeer peer, int dwKeySize)
        {
            _dwKeySize = dwKeySize;
            _peer = peer;
        }

        public void ImportRemoteTripleDes(byte[] key, byte[] iv)
        {
            _connectionCryptoProvider = new TripleDESCryptoServiceProvider();
            _connectionCryptoProvider.Key = key;
            _connectionCryptoProvider.IV = iv;
        }

        public void EncryptHail(NetOutgoingMessage msg,string token, NetConnection reciever = null)
        {
            
            var provider = _connectionCryptoProvider;
            int unEncLenBits = msg.LengthBits;

            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, provider.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(msg.m_data, 0, msg.LengthBytes);
            cs.Close();

            // get results
            var arr = ms.ToArray();
            ms.Close();

            msg.EnsureBufferSize((arr.Length + 4 + token.Length) * 8);
            msg.LengthBits = 0; // reset write pointer
            msg.Write(token);
            var tokenLength = msg.LengthBytes;
            msg.Write((uint)unEncLenBits);
            msg.Write(arr);
            msg.LengthBits = (arr.Length + 4 + tokenLength) * 8;
        }

        public void Encrypt(NetOutgoingMessage msg, NetConnection reciever = null)
        {
            var provider = _connectionCryptoProvider;
            int unEncLenBits = msg.LengthBits;

            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, provider.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(msg.m_data, 0, msg.LengthBytes);
            cs.Close();

            // get results
            var arr = ms.ToArray();
            ms.Close();

            msg.EnsureBufferSize((arr.Length + 4) * 8);
            msg.LengthBits = 0; // reset write pointer
            msg.Write((uint)unEncLenBits);
            msg.Write(arr);
            msg.LengthBits = (arr.Length + 4) * 8;
        }

        public void Decrypt(NetIncomingMessage msg)
        {
            var provider = _connectionCryptoProvider;
            int unEncLenBits = (int)msg.ReadUInt32();

            var ms = new MemoryStream(msg.m_data, 4, msg.LengthBytes - 4);
            var cs = new CryptoStream(ms, provider.CreateDecryptor(), CryptoStreamMode.Read);

            var byteLen = NetUtility.BytesToHoldBits(unEncLenBits);
            var result = _peer.GetStorage(byteLen);
            cs.Read(result, 0, byteLen);
            cs.Close();

            // TODO: recycle existing msg

            msg.m_data = result;
            msg.m_bitLength = unEncLenBits;
            msg.m_readPosition = 0;
        }
    }
}

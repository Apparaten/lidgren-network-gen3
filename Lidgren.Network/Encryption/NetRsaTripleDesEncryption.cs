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

    public class ServerRsaTripleDesNetEncryptor : INetEncryptor
    {
        private Dictionary<NetConnection, RSACryptoServiceProvider> HandShakeCryptoProviders { get; set; } = new Dictionary<NetConnection, RSACryptoServiceProvider>();
        public Dictionary<NetConnection, TripleDESCryptoServiceProvider> ConnectionCryptoProviders { get; set; } = new Dictionary<NetConnection, TripleDESCryptoServiceProvider>();

        private RSACryptoServiceProvider _handShakeDecryptor;
        private NetPeer _peer;
        private int _dwKeySize;
        public ServerRsaTripleDesNetEncryptor(NetPeer peer, int dwKeySize)
        {
            _dwKeySize = dwKeySize;
            _peer = peer;
        }

        public byte[] GenerateHandShakeKey()
        {
            _handShakeDecryptor = new RSACryptoServiceProvider(_dwKeySize);
            return _handShakeDecryptor.ExportCspBlob(false);
        }

        public void ImportClientHandShakeKey(byte[] cspBlob, NetConnection connection)
        {
            var rsaProvider = new RSACryptoServiceProvider();
            rsaProvider.ImportCspBlob(cspBlob);
            if (HandShakeCryptoProviders.ContainsKey(connection))
            {
                HandShakeCryptoProviders[connection] = rsaProvider;
            }
            else
            {
                HandShakeCryptoProviders.Add(connection, rsaProvider);
            }
        }
        public void GenerateKeyForConnection(NetConnection connection, out byte[] key, out byte[] iv)
        {
            var provider = new TripleDESCryptoServiceProvider();
            provider.GenerateKey();
            provider.GenerateIV();
            if (ConnectionCryptoProviders.ContainsKey(connection))
            {
                ConnectionCryptoProviders[connection] = provider;
            }
            else
            {
                ConnectionCryptoProviders.Add(connection, provider);
            }
            key = provider.Key;
            iv = provider.IV;
        }
        public void EncryptHail(NetOutgoingMessage msg, NetConnection reciever)
        {
            var encryptor = HandShakeCryptoProviders[reciever];
            if (msg.m_data.Length > ((encryptor.KeySize - 384) / 8) + 37)
            {
                //WORKAROUND, Sometimes the data is faulty
                var data = msg.m_data.Take(msg.LengthBytes).ToArray();
                msg.m_data = data;
            }
            HandShakeCryptoProviders.Remove(reciever);
            var unEncLenBits = msg.LengthBits;
            var encryptedData = encryptor.Encrypt(msg.m_data, RSAEncryptionPadding.OaepSHA1);

            msg.EnsureBufferSize((encryptedData.Length + 4) * 8);
            msg.LengthBits = 0; // reset write pointer
            msg.Write((uint)unEncLenBits);
            msg.Write(encryptedData);
            msg.LengthBits = (encryptedData.Length + 4) * 8;
        }

        public void DecryptHail(NetIncomingMessage msg)
        {
            var unEncLenBits = (int)msg.ReadUInt32();

            var ms = new MemoryStream(msg.m_data, 4, msg.LengthBytes - 4);
            var decyptedData = _handShakeDecryptor.Decrypt(ms.ToArray(), RSAEncryptionPadding.OaepSHA1);

            // TODO: recycle existing msg
            msg.m_data = decyptedData;
            msg.m_bitLength = unEncLenBits;
            msg.m_readPosition = 0;
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
            var provider = ConnectionCryptoProviders.FirstOrDefault(f => f.Key == msg.SenderConnection).Value;
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
    public class ClientRsaTripleDesNetEncryptor : INetEncryptor
    {
        private TripleDESCryptoServiceProvider _connectionCryptoProvider;
        private RSACryptoServiceProvider _handShakeEncryptor;
        private RSACryptoServiceProvider _handShakeDecryptor;
        private NetPeer _peer;
        private int _dwKeySize;
        public ClientRsaTripleDesNetEncryptor(NetPeer peer, int dwKeySize)
        {
            _dwKeySize = dwKeySize;
            _peer = peer;
        }
        public byte[] ExportHandShakeKey()
        {
            _handShakeDecryptor = new RSACryptoServiceProvider(_dwKeySize);
            return _handShakeDecryptor.ExportCspBlob(false);
        }

        public void ImportRemoteTripleDes(byte[] key, byte[] iv)
        {
            _connectionCryptoProvider = new TripleDESCryptoServiceProvider();
            _connectionCryptoProvider.Key = key;
            _connectionCryptoProvider.IV = iv;
        }

        public void ImportPublicRsa(byte[] csvBlob)
        {
            _handShakeEncryptor = new RSACryptoServiceProvider();
            _handShakeEncryptor.ImportCspBlob(csvBlob);
            _handShakeEncryptor.PersistKeyInCsp = true;
        }


        public void EncryptHail(NetOutgoingMessage msg, NetConnection reciever = null)
        {

            var encryptor = _handShakeEncryptor;
            if (msg.m_data.Length > ((encryptor.KeySize - 384) / 8) + 37)
            {
                //WORKAROUND, Sometimes the data is faulty
                var data = msg.m_data.Take(msg.LengthBytes).ToArray();
                msg.m_data = data;
            }
            var unEncLenBits = msg.LengthBits;
            var encryptedData = encryptor.Encrypt(msg.m_data, RSAEncryptionPadding.OaepSHA1);

            msg.EnsureBufferSize((encryptedData.Length + 4) * 8);
            msg.LengthBits = 0; // reset write pointer
            msg.Write((uint)unEncLenBits);
            msg.Write(encryptedData);
            msg.LengthBits = (encryptedData.Length + 4) * 8;
        }

        public void DecryptHail(NetIncomingMessage msg)
        {
            var unEncLenBits = (int)msg.ReadUInt32();

            var ms = new MemoryStream(msg.m_data, 4, msg.LengthBytes - 4);
            var decyptedData = _handShakeDecryptor.Decrypt(ms.ToArray(), RSAEncryptionPadding.OaepSHA1);

            // TODO: recycle existing msg
            msg.m_data = decyptedData;
            msg.m_bitLength = unEncLenBits;
            msg.m_readPosition = 0;
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

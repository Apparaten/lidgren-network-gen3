using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using Lidgren.Network.Encryption;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorProviderBase<TServiceContract, TAuthenticationUser, TSerializedSendType> : CommunicatorBase<TServiceContract> where TServiceContract : ICallbackContract,new() where TAuthenticationUser : new()
    {
        protected IAuthenticator Authenticator;
        protected string[] RequiredAuthenticationRoles;
        private List<Tuple<AuthenticationResult, string>> AuthenticationResults { get; } = new List<Tuple<AuthenticationResult, string>>();
        private readonly object _usersLock = new object();
        private Dictionary<NetConnection, CommunicationUser<TAuthenticationUser>> _users =new Dictionary<NetConnection, CommunicationUser<TAuthenticationUser>>();
        private bool _isRunning;
        public bool IsRunning => _isRunning;
        protected Dictionary<NetConnection, CommunicationUser<TAuthenticationUser>> PendingAndLoggedInUsers
        {
            get
            {
                lock (_usersLock)
                {
                    return _users;
                }
            }
        }

        private Stopwatch TickWatch { get; } = new Stopwatch();

        protected CommunicatorProviderBase(NetPeerConfiguration configuration,ConverterBase converter, IAuthenticator authenticator = null, string[] requiredAuthenticationRoles = null)
        {
            Configuration = configuration;
            Converter = converter;
            if (authenticator != null)
            {
                RequiredAuthenticationRoles = requiredAuthenticationRoles;
                configuration.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            }
            NetConnector = new NetServer(configuration);
            NetEncryptor = new ServerRsaTripleDesNetEncryptor(NetConnector,4096);
            Authenticator = authenticator;
            Initialize(typeof(ICallbackContract), typeof(IProviderContract));
        }

        public virtual void StartService()
        {
            _isRunning = true;
            NetConnector.Start();
            Log($"Service started on port: {Configuration.Port}");
        }
        public virtual void StopService(string message)
        {
            NetConnector.Shutdown(message);
            _isRunning = false;
        }

        public override void Tick(int repeatRate)
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
                        var connectionResult = (NetConnectionResult)msg.ReadByte();
                        OnConnectionStatusChanged(change, connectionResult, msg.SenderConnection);
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        ConnectionApproval(msg);
                        break;
                    case NetIncomingMessageType.Data:
                        if (AuthorizedForMessage(msg.SenderConnection))
                        {
                            NetEncryptor.Decrypt(msg);
                            FilterMessage(msg);
                        }
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
            while (AuthenticationResults.Count > 0)
            {
                var result = AuthenticationResults[0].Item1;
                var user = AuthenticationResults[0].Item2;
                try
                {
                    if (result.Success)
                    {
                        byte[] key;
                        byte[] iv;
                        ((ServerRsaTripleDesNetEncryptor)NetEncryptor).GenerateKeyForConnection(result.Connection, out key, out iv);
                        var hailMessage = NetConnector.CreateMessage();
                        Log($"HAIL => keyLength: {key.Length} ivLength:{iv.Length}");
                        Log($"HAIL => no data HailmessageLength: {hailMessage.LengthBytes}");
                        Log($"HAIL => keyLength: {key.Length} ivLength:{iv.Length} Total:{key.Length + iv.Length}");
                        hailMessage.Write(key.Length);
                        hailMessage.Write(key);
                        hailMessage.Write(iv.Length);
                        hailMessage.Write(iv);
                        Log($"HAIL => Full data HailmessageLength: {hailMessage.LengthBytes}");
                        ((ServerRsaTripleDesNetEncryptor)NetEncryptor).EncryptHail(hailMessage, result.Connection);
                        result.Connection.Approve(hailMessage);
                        OnAuthenticationApproved(result, user);
                    }
                    else
                    {
                        switch (result.RequestState)
                        {
                            case RequestState.EndpointFailure:
                                result.Connection.Deny(NetConnectionResult.NoResponseFromRemoteHost);
                                break;
                            case RequestState.Success:
                                result.Connection.Deny(NetConnectionResult.Unknown);
                                break;
                            case RequestState.UserAlreadyLoggedIn:
                                result.Connection.Deny(NetConnectionResult.UserAlreadyLoggedIn);
                                break;
                            case RequestState.WrongCredentials:
                                result.Connection.Deny(NetConnectionResult.WrongCredentials);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        OnAuthenticationDenied(result, user);
                    }
                }
                catch (Exception e)
                {
                    Log(e);
                    result.Connection.Deny(NetConnectionResult.Unknown);
                    OnAuthenticationDenied(result, user);
                }
                finally
                {
                    AuthenticationResults.Remove(AuthenticationResults[0]);
                }
                
            }
            RunTasks();
            TickWatch.Stop();
            var interval = 1000 / repeatRate;
            var elapsedTime = (int)TickWatch.ElapsedMilliseconds;
            var finalInterval = interval - elapsedTime;
            if (finalInterval > 0)
            {
                Task.Delay(finalInterval).Wait();
            }
            else
            {
                Log($"Tick loop is working overhead at {elapsedTime}ms, configured interval is at {interval}ms");
            }
            TickWatch.Restart();
        }

        protected override void OnDisconnected_Internal(NetConnection connection)
        {
            OnUserDisconnected(PendingAndLoggedInUsers[connection]);
            PendingAndLoggedInUsers.Remove(connection);
            ((ServerRsaTripleDesNetEncryptor)NetEncryptor).ConnectionCryptoProviders.Remove(connection);
        }

        protected virtual void OnUserDisconnected(CommunicationUser<TAuthenticationUser> user)
        {
            
        }
        protected override void OnDisconnected(NetConnection connection)
        {
            
        }

        //protected override void Log(object message, [CallerMemberName]string caller = null)
        //{
        //    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]\t[{caller}]\t{message.ToString()}");
        //}
        protected virtual bool AuthorizedForMessage(NetConnection connection)
        {
            return PendingAndLoggedInUsers.ContainsKey(connection);
        }
        protected virtual void ConnectionApproval(NetIncomingMessage msg)
        {
            ((ServerRsaTripleDesNetEncryptor)NetEncryptor).DecryptHail(msg);
            var connection = msg.SenderConnection;
            var user = msg.ReadString().ToLower();
            var password = msg.ReadString();
            var cspBlob = msg.ReadBytes(msg.ReadInt32());

            ((ServerRsaTripleDesNetEncryptor)NetEncryptor).ImportClientHandShakeKey(cspBlob,msg.SenderConnection);
            if (PendingAndLoggedInUsers.Values.Any(c => c.UserName == user))
            {
                AuthenticationResults.Add(new Tuple<AuthenticationResult, string>(
                    new AuthenticationResult()
                    {
                        Connection = connection,
                        Success = false,
                        RequestState = RequestState.UserAlreadyLoggedIn
                    }, user));
                return;
            }
            PendingAndLoggedInUsers.Add(connection, new CommunicationUser<TAuthenticationUser>() { UserName = user });
            var authTask = Authenticator.Authenticate(user, password)
                .ContinueWith(async approval =>
               {
                   var authentication = await approval;
                   authentication.Connection = connection;

                   ConnectionApprovalExtras(authentication);
                   if (authentication.Success && !string.IsNullOrEmpty(authentication.UserId))
                   {
                        var userData = await GetUser(authentication.UserId);
                        PendingAndLoggedInUsers[connection].UserData = userData;
                        PendingAndLoggedInUsers[connection].LoggedInTime = DateTime.UtcNow;
                   }
                   AuthenticationResults.Add(new Tuple<AuthenticationResult, string>(authentication, user));
               });
            AddRunningTask(authTask);
        }

        protected abstract Task<TAuthenticationUser> GetUser(string id);
        protected virtual void ConnectionApprovalExtras(AuthenticationResult authenticationResult)
        {

        }
        protected virtual void OnAuthenticationApproved(AuthenticationResult authenticationResult, string user)
        {

        }
        protected virtual void OnAuthenticationDenied(AuthenticationResult authenticationResult, string user)
        {
            PendingAndLoggedInUsers.Remove(authenticationResult.Connection);
        }
        protected void DoForUsers(Func<TAuthenticationUser, bool> predicate, Action<NetConnection, TAuthenticationUser> onConnectionAction)
        {
            var users = PendingAndLoggedInUsers.ToList();
            foreach (var kv in users)
            {
                if (kv.Value.UserData == null) // user pending a log in - ignored...
                    continue;
                if (predicate(kv.Value.UserData))
                {
                    onConnectionAction(kv.Key, kv.Value.UserData);
                }
            }
        }
    }
}

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorProviderBase<TServiceContract, TAuthenticationUser, TSerializedSendType> : CommunicatorBase<TServiceContract, TSerializedSendType> where TServiceContract : ICallbackContract,new() where TAuthenticationUser : new()
    {
        protected IAuthenticator Authenticator;
        protected string[] RequiredAuthenticationRoles;

        private List<Tuple<AuthenticationResult,string>> AuthenticationResults { get;} = new List<Tuple<AuthenticationResult,string>>();
        protected Dictionary<NetConnection,CommunicationUser<TAuthenticationUser>> PendingAndLoggedInUsers { get; } = new Dictionary<NetConnection, CommunicationUser<TAuthenticationUser>>();
        private Stopwatch TickWatch { get; } = new Stopwatch();

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
                        var connectionResult = (NetConnectionResult) msg.ReadByte();
                        OnConnectionStatusChanged(change,connectionResult,msg.SenderConnection);
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        ConnectionApproval(msg);
                        break;
                    case NetIncomingMessageType.Data:
                        if (AuthorizedForMessage(msg.SenderConnection))
                        {
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
                if (result.Success)
                {
                    result.Connection.Approve();
                    OnAuthenticationApproved(result, user);
                }
                else
                {
                    result.Connection.Deny(result.RequestState == RequestState.EndpointFailure
                        ? NetConnectionResult.NoResponseFromRemoteHost
                        : NetConnectionResult.WrongCredentials);
                    OnAuthenticationDenied(result, user);
                }
                AuthenticationResults.Remove(AuthenticationResults[0]);
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
            var connected = NetConnector.Connections.Count;
            var users = PendingAndLoggedInUsers.Count;
            if(connected != users)
                Log($"Connections {connected} users {users}");
            TickWatch.Restart();
        }

        protected override void OnDisconnected_Internal(NetConnection connection)
        {
            Log($"removing user: {PendingAndLoggedInUsers[connection].UserName}");
            PendingAndLoggedInUsers.Remove(connection);
        }
        protected override void OnDisconnected(NetConnection connection)
        {
            
        }

        protected override void Log(object message, [CallerMemberName]string caller = null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]\t[{caller}]\t{message.ToString()}");
        }
        protected virtual bool AuthorizedForMessage(NetConnection connection)
        {
            return PendingAndLoggedInUsers.ContainsKey(connection);
        }
        protected virtual void ConnectionApproval(NetIncomingMessage msg)
        {
            var connection = msg.SenderConnection;
            var user = msg.ReadString().ToLower();
            var password = msg.ReadString();
            if (PendingAndLoggedInUsers.Values.Any(c => c.UserName == user))
            {
                AuthenticationResults.Add(new Tuple<AuthenticationResult, string>(
                    new AuthenticationResult() {Connection = connection, Success = false}, user));
                return;
            }
            PendingAndLoggedInUsers.Add(connection,new CommunicationUser<TAuthenticationUser>(){UserName = user});

            var authTask = Authenticator.Authenticate(user, password)
                .ContinueWith( async approval=>
                {
                    var authentication = await approval;
                    authentication.Connection = connection;

                    ConnectionApprovalExtras(authentication);
                    if (authentication.Success && !string.IsNullOrEmpty(authentication.UserId))
                    {
                        PendingAndLoggedInUsers[connection].UserData = await GetUser(authentication.UserId);
                    }
                    AuthenticationResults.Add(new Tuple<AuthenticationResult,string>(authentication,user));
                });
            AddRunningTask(authTask);
        }

        protected abstract Task<TAuthenticationUser> GetUser(string id);
        protected virtual void ConnectionApprovalExtras(AuthenticationResult authenticationResult)
        {
            
        }
        protected virtual void OnAuthenticationApproved(AuthenticationResult authenticationResult,string user)
        {
            
        }
        protected virtual void OnAuthenticationDenied(AuthenticationResult authenticationResult,string user)
        {
            PendingAndLoggedInUsers.Remove(authenticationResult.Connection);
        }
    }
}

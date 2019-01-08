using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorBase
    {
        private Dictionary<int, Dictionary<string, MessagePointer>> RemoteContracts { get; } = new Dictionary<int, Dictionary<string, MessagePointer>>();
        private Dictionary<int, Dictionary<ushort, MessagePointer>> LocalContracts { get; } = new Dictionary<int, Dictionary<ushort, MessagePointer>>();
        private Dictionary<string, AwaitingCallJob> AwaitingCalls { get; }= new Dictionary<string, AwaitingCallJob>();
        private readonly List<TaskJob> _runningTasks = new List<TaskJob>();
        public int CurrentTaskCount => _runningTasks.Count;

        protected NetPeer NetConnector;
        protected NetPeerConfiguration Configuration;
        protected CommunicationSettings CommunicationSettings = new CommunicationSettings();
        public NetConnectionStatus ConnectionStatus { get; protected set; }
        
        protected ConverterBase Converter;

        public event Action<NetConnectionStatus,NetConnectionResult> OnConnectionStatusChangedEvent;
        private List<Tuple<Object, string>> _logList = new List<Tuple<object, string>>();
        private event Action<object, string> _onLoggedEvent;
        public event Action<object, string> OnLoggedEvent
        {
            add
            {
                _onLoggedEvent += value;
                foreach (var logEntry in _logList)
                {
                    value.Invoke(logEntry.Item1,logEntry.Item2);
                }
            }
            remove => _onLoggedEvent -= value;
        }

        public List<Exception> ExceptionsCaught { get; } = new List<Exception>();
        /// <summary>
        /// Adds a reciever for provided contract.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reciever"></param>
        public void AddLocalContract<T>(T reciever) where T : IContract
        {
            var map = reciever.GetType().GetInterfaceMap(typeof(T));
            var recieverMapping = map.TargetMethods.OrderBy(m => m.Name).ToList();
            var hash = typeof(T).Name.GetHashCode();
            var messagePointers = new Dictionary<ushort, MessagePointer>();
            LocalContracts.Add(hash, messagePointers);
            ushort counter = 0;
            foreach (var methodInfo in recieverMapping)
            {
                var authenticationAttributes = methodInfo.GetCustomAttributes<AuthenticationAttribute>().ToArray();
                var pointer = new MessagePointer
                {
                    MessagePointerKey = counter++,
                    Method = methodInfo,
                    ContractObject = reciever,
                    ContractHash = hash,
                    ParameterInfos = methodInfo.GetParameters(),
                    AuthenticationAttributes = authenticationAttributes
                };
                messagePointers.Add(pointer.MessagePointerKey, pointer);
            }
        }
        /// <summary>
        /// Maps a contract to communicate with.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddRemoteContract<T>() where T : IContract
        {
            var type = typeof(T);
            var hash = type.Name.GetHashCode();
            var methods = type.GetMethods().OrderBy(m => m.Name);
            ushort counter = 0;
            foreach (var methodInfo in methods)
            {
                if (!RemoteContracts.ContainsKey(hash))
                {
                    RemoteContracts.Add(hash, new Dictionary<string, MessagePointer>());
                }
                var messagePointer = new MessagePointer
                {
                    ContractHash = hash,
                    MessagePointerKey = counter++,
                    Method = methodInfo,
                    ParameterInfos = methodInfo.GetParameters()
                };
                RemoteContracts[hash].Add(methodInfo.ToString(), messagePointer);
            }
        }
        protected void CreateAndCall(Type contract,UnaryExpression unaryExpression, object[] args, List<NetConnection> connections)
        {
            var methodCallExpression = (MethodCallExpression)unaryExpression.Operand;
            var method = methodCallExpression.Object;
            
            var methodName = method.ToString();
            var contractHash = contract.Name.GetHashCode();
            var methodPointer = RemoteContracts[contractHash][methodName];
            var netMessage = CreateMessage(methodPointer, CommunicationType.Call, args);

            if (connections == null)
            {
                (NetConnector as NetClient)?.SendMessage(netMessage, NetDeliveryMethod.ReliableOrdered);
            }
            else
            {
                (NetConnector as NetServer)?.SendMessage(netMessage, connections, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        protected Task<TReturn> CreateAndCallAsync<TReturn>(Type contract, UnaryExpression unaryExpression, object[] args, NetConnection connection = null)
        {
            var methodCallExpression = (MethodCallExpression)unaryExpression.Operand;
            var method = methodCallExpression.Object;

            var methodName = method.ToString();
            var contractHash = contract.Name.GetHashCode();
            var methodPointer = RemoteContracts[contractHash][methodName];
            var netMessage = CreateMessage(methodPointer, CommunicationType.CallAsync, args);
            var taskKey = Guid.NewGuid().ToString();
            netMessage.Write(taskKey);

            if (connection == null)
            {
                (NetConnector as NetClient)?.SendMessage(netMessage, NetDeliveryMethod.ReliableOrdered);
            }
            else
            {
                (NetConnector as NetServer)?.SendMessage(netMessage, connection, NetDeliveryMethod.ReliableOrdered, 0);
            }
            
            return Task.Run((() =>
            {
                var time = DateTime.Now;
                AwaitingCalls.Add(taskKey, new AwaitingCallJob() { ReturnType = typeof(TReturn), Data = null });
                while (true)
                {
                    var elapsedTime = (DateTime.Now - time).TotalSeconds;
                    if (elapsedTime >= CommunicationSettings.AwaitCallTimeOut)
                    {
                        AwaitingCalls.Remove(taskKey);
                        throw new CommunicationTimeOutException($"Calling {methodPointer.Method.Name} took longer than configured time in CommunicationSettings, elapsed time: {elapsedTime:F1}");
                    }
                    if (AwaitingCalls[taskKey].Data != null)
                        break;
                }
                var data = (TReturn)AwaitingCalls[taskKey].Data;
                AwaitingCalls.Remove(taskKey);
                return data;
            }));
        }

        private NetOutgoingMessage CreateMessage(MessagePointer messagePointer, CommunicationType type,object[] args = null)
        {
            var callMessage = Converter.CreateSendCallMessage(messagePointer.ParameterInfos, args);
            var netMessage = NetConnector.CreateMessage();
            netMessage.Write((byte)type);
            netMessage.Write(messagePointer.ContractHash);
            netMessage.Write(messagePointer.MessagePointerKey);
            netMessage.Write(Converter.SerializeCallMessage(callMessage));
            return netMessage;
        }

        private void SendAwaitedReturnMessage(string identifier, object result, NetConnection connection)
        {
            var netMessage = NetConnector.CreateMessage();
            netMessage.Write((byte)CommunicationType.CallAsyncReturn);
            netMessage.Write(identifier);
            netMessage.Write(Converter.SerializeArgument(result,result.GetType()));
            if (NetConnector is NetServer server)
            {
                server.SendMessage(netMessage, connection, NetDeliveryMethod.ReliableOrdered, 0);
            }
            else
            {
                (NetConnector as NetClient)?.SendMessage(netMessage, NetDeliveryMethod.ReliableOrdered);
            }
        }
        protected void FilterMessage(NetIncomingMessage message)
        {
            var messageType = (CommunicationType)message.ReadByte();
            if (messageType == CommunicationType.CallAsyncReturn)
            {
                var identifier = message.ReadString();
                var awaitingCall = AwaitingCalls[identifier];
                awaitingCall.Data = Converter.DeserializeArgument(message.ReadString(), awaitingCall.ReturnType);
                return;
            }
            var contractHash = message.ReadInt32();
            var key = message.ReadUInt16();
            var pointer = LocalContracts[contractHash][key];

            if (!AuthorizedForMessage(pointer, message.SenderConnection))
            {
                return;
            }

            var args = Converter.HandleRecieveMessage(message.ReadString(), pointer,message.SenderConnection);
            switch (messageType)
            {
                case CommunicationType.Call:
                    try
                    {
                        if (pointer.Method.ReturnType == typeof(Task))
                        {
                            AddRunningTask((Task)pointer.Method.Invoke(pointer.ContractObject, args));
                        }
                        else
                        {
                            pointer.Method.Invoke(pointer.ContractObject, args);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex.InnerException ?? ex);
                    }
                    break;
                case CommunicationType.CallAsync:
                    try
                    {
                        var taskKey = message.ReadString();
                        AddRunningTask((Task)pointer.Method.Invoke(pointer.ContractObject,args),pointer.Method.ReturnType,taskKey,message.SenderConnection);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.InnerException ?? ex);
                    }
                    break;
                case CommunicationType.CallAsyncReturn:

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
        }
        protected virtual bool AuthorizedForMessage(MessagePointer pointer,NetConnection connection)
        {
            return true;
        }
        protected void RunTasks()
        {
            for (var i = _runningTasks.Count; i-- > 0;)
            {
                var job = _runningTasks[i];
                var task = (Task) job.Task;
                switch (task.Status)
                {
                    case TaskStatus.Created:
                        break;
                    case TaskStatus.WaitingForActivation:
                        break;
                    case TaskStatus.WaitingToRun:
                        break;
                    case TaskStatus.Running:
                        break;
                    case TaskStatus.WaitingForChildrenToComplete:
                        break;
                    case TaskStatus.RanToCompletion:
                        _runningTasks.Remove(job);
                        var type = job.TaskType;
                        if (type == null)
                        {
                            
                        }
                        else if (type == typeof(Task))
                        {
                            
                        }
                        else
                        {
                            var taskResultObject = job.Task.GetType().GetProperty("Result").GetValue(job.Task, null);
                            SendAwaitedReturnMessage(job.Identifier,taskResultObject,job.Reciever);
                        }
                        break;
                    case TaskStatus.Canceled:
                        _runningTasks.Remove(job);
                        if(task.Exception != null)
                            ExceptionsCaught.Add(task.Exception);
                        break;
                    case TaskStatus.Faulted:
                        _runningTasks.Remove(job);
                        if (task.Exception != null)
                            ExceptionsCaught.Add(task.Exception);
                        break;
                }
            }
        }
        protected virtual void Log(object message, [CallerMemberName] string caller = null)
        {
            _onLoggedEvent?.Invoke(message.ToString(),caller);
            _logList.Add(new Tuple<object, string>(message, caller));
        }
        protected void AddRunningTask(Task task,Type taskType = null,string identifier = null,NetConnection reciever = null)
        {
            _runningTasks.Add(new TaskJob(){Task = task,TaskType = taskType,Identifier = identifier,Reciever = reciever});
        }
        public virtual void Tick(int interval)
        {
            
        }

        public virtual void CloseConnection()
        {
            NetConnector.Shutdown("shutdown");
        }

        
        protected virtual void OnConnectionStatusChanged(NetConnectionStatus status,NetConnectionResult connectionResult, NetConnection connection)
        {
            ConnectionStatus = status;
            OnConnectionStatusChangedEvent?.Invoke(status,connectionResult);
            switch (status)
            {
                case NetConnectionStatus.None:
                    break;
                case NetConnectionStatus.InitiatedConnect:
                    break;
                case NetConnectionStatus.ReceivedInitiation:
                    break;
                case NetConnectionStatus.RespondedAwaitingApproval:
                    break;
                case NetConnectionStatus.RespondedConnect:
                    break;
                case NetConnectionStatus.Connected:
                    OnConnected(connection);
                    break;
                case NetConnectionStatus.Disconnecting:
                    break;
                case NetConnectionStatus.Disconnected:
                    OnDisconnected_Internal(connection);
                    OnDisconnected(connection);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
            Log(status);
        }

        protected virtual void OnDisconnected_Internal(NetConnection connection)
        {
            
        }
        protected abstract void OnDisconnected(NetConnection connection);
        protected abstract void OnConnected(NetConnection connection);

        private class AwaitingCallJob
        {
            public Type ReturnType { get; set; }
            public object Data { get; set; }
        }
        private class TaskJob
        {
            public object Task { get; set; }
            public Type TaskType { get; set; }
            public string Identifier { get; set; }
            public NetConnection Reciever { get; set; }
        }
          
    }
    public class MessagePointer
    {
        public int ContractHash;
        public ushort MessagePointerKey;
        public ParameterInfo[] ParameterInfos;
        public MethodInfo Method;
        public object ContractObject;
        public AuthenticationAttribute[] AuthenticationAttributes { get; set; }
    }
    public class CallMessage
    {
        public string[] Args;
    }
}

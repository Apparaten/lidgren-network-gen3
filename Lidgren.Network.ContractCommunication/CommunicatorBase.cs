using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class CommunicatorBase<TServiceContract,TSerializedSendType> where TServiceContract : IContract
    {

        protected Dictionary<ushort, MessageFilter> _recieveFilters;
        protected Dictionary<ushort, MessageFilter> _sendFilters;

        protected Dictionary<string, ushort> _sendAddressDictionary;
        public TServiceContract Contract { get; private set; }

        protected NetPeer NetConnector;
        protected NetConnection CallerConnection;
        protected NetPeerConfiguration Configuration;

        protected ConverterBase<TSerializedSendType> Converter;

        public Action<NetConnectionStatus> OnConnectionStatusChanged;

        protected void Initialize(Type sendContractType, Type recieveContractType)
        {
            if (!sendContractType.IsInterface || !recieveContractType.IsInterface)
            {
                throw new Exception("type must be an interface!");
            }

            Contract = ClassBuilder.BuildProType<TServiceContract>();
            _recieveFilters = MapContract(this, recieveContractType);
            _sendFilters = MapContract(Contract, sendContractType);
            _sendAddressDictionary = GetAddresses(typeof(TServiceContract));
        }
        private Dictionary<string, ushort> GetAddresses(Type type)
        {
            var addresses = new Dictionary<string, ushort>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            var methods = new List<MethodInfo>(type.GetMethods(flags).OrderByDescending(m => m.Name));
            var addressIndexer = default(ushort);
            foreach (var methodInfo in methods)
            {
                addresses.Add(methodInfo.Name, addressIndexer++);
            }

            return addresses;
        }
        private Dictionary<ushort, MessageFilter> MapContract(object mapObject, Type inheritedType)
        {
            var interfaces = mapObject.GetType().GetInterfaces();
            var contract = interfaces.First(@interface => inheritedType.IsAssignableFrom(@interface) && @interface != inheritedType);

            const BindingFlags flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            var methods = new List<MethodInfo>(contract.GetMethods(flags).OrderByDescending(m => m.Name));

            var addresses = GetAddresses(contract);
            var callbackFilters = new Dictionary<ushort, MessageFilter>();

            foreach (var methodInfo in methods)
            {
                var messageFilter = new MessageFilter();
                messageFilter.Method = methodInfo;
                messageFilter.Types = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                callbackFilters.Add(addresses[methodInfo.Name], messageFilter);
            }
            return callbackFilters;
        }
        public void Call(Action method, NetConnection connection = null) => CreateAndSendCall(method.Method, null, connection);
        public void Call(Action<NetConnection> method, NetConnection connection = null) => CreateAndSendCall(method.Method, null, connection);
        public void Call<T1>(Action<T1> method, T1 arg1, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1 }, connection);
        public void Call<T1, T2>(Action<T1, T2> method, T1 arg1, T2 arg2, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2 }, connection);
        public void Call<T1, T2, T3>(Action<T1, T2, T3> method, T1 arg1, T2 arg2, T3 arg3, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3 }, connection);
        public void Call<T1, T2, T3, T4>(Action<T1, T2, T3,T4> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4 }, connection);
        public void Call<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5 }, connection);
        public void Call<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5,T6 arg6, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5, arg6 }, connection);
        public void Call<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5,T6 arg6, T7 arg7, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7 }, connection);
        public void Call<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5,T6 arg6, T7 arg7, T8 arg8, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8 }, connection);
        public void Call<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5,T6 arg6, T7 arg7, T8 arg8, T9 arg9, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9 }, connection);
        public void Call<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> method, T1 arg1, T2 arg2, T3 arg3, T4 arg4,T5 arg5,T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, NetConnection connection = null) => CreateAndSendCall(method.Method, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10 }, connection);

        private void CreateAndSendCall(MethodInfo info, object[] args = null, NetConnection recipient = null)
        {
            var callMessage =
                Converter.CreateSendCallMessage(_sendAddressDictionary[info.Name], info.GetParameters(), args);
            var netMessage = NetConnector.CreateMessage();
            netMessage.Write(callMessage.Key);
            netMessage.Write(Converter.SerializeCallMessage(callMessage));
            if (recipient == null)
            {
                (NetConnector as NetClient)?.SendMessage(netMessage, NetDeliveryMethod.ReliableOrdered);
            }
            else
            {
                (NetConnector as NetServer)?.SendMessage(netMessage, recipient, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        protected void FilterMessage(NetIncomingMessage message)
        {
            var key = message.ReadUInt16();
            var pointer = _recieveFilters[key];
            var args = Converter.HandleRecieveMessage(message.ReadString(), pointer,message.SenderConnection);
            CallerConnection = message.SenderConnection;
            pointer.Method.Invoke(this, args);
        }

        public virtual void Tick()
        {

        }
    }
    public class MessageFilter
    {
        public MethodInfo Method;
        public Type[] Types;
    }
    public class CallMessage<T>
    {
        public ushort Key;
        public T[] Args;
    }
    
}

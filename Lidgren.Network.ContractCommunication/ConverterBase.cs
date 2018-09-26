using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class ConverterBase
    {
        //TODO change string to TBaseData
        public abstract string SerializeCallMessage(CallMessage callMessage);
        //TODO change string to TBaseData
        protected abstract CallMessage DeserializeCallMessage(string message);
        public abstract string SerializeArgument(object obj, Type type);
        //public abstract object DeserializeArgument(TBaseData baseTypeData, Type type);
        public CallMessage CreateSendCallMessage(ushort key, ParameterInfo[] parameters, object[] args)
        {
            var serializedArgs = new List<string>();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(NetConnection))
                    continue;
                serializedArgs.Add(SerializeArgument(args[i], parameters[i].ParameterType));
            }
            return new CallMessage() { Args = serializedArgs.ToArray(), Key = key };
        }

        public CallMessage CreateSendCallMessage(object obj,Type type)
        {
            return new CallMessage(){Args = new []{SerializeArgument(obj,type)}};
        }

        public object[] HandleRecieveMessage(string message, MessageFilter pointer, NetConnection senderConnection)
        {
            var callMessage = DeserializeCallMessage(message);
            var args = new object[pointer.Types.Length];
            for (var i = 0; i < pointer.Types.Length; i++)
            {
                var pointerArgType = pointer.Types[i];
                if (pointerArgType == typeof(NetConnection))
                {
                    args[i] = senderConnection;
                }
                else
                {
                    args[i] = DeserializeArgument(callMessage.Args[i], pointerArgType);
                }
            }
            return args;
        }

        public abstract object DeserializeArgument(string message, Type returnType);
    }
}

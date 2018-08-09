using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public class CommunicationTimeOutException : Exception
    {
        public CommunicationTimeOutException(string message) : base(message)
        {
        }
    }
}

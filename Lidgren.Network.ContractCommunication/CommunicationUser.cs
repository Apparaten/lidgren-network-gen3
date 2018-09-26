using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public class CommunicationUser<T>
    {
        public string UserName { get; set; }
        public T UserData { get; set; }
        public DateTime LoggedInTime { get; set; }
    }
}

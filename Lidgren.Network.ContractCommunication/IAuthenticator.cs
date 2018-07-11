using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public interface IAuthenticator
    {
        Task<AuthenticationResult> Authenticate(string user, string password);
    }

    public class AuthenticationResult
    {
        public string UserId { get; set; }
        public bool Success { get; set; }
        public string[] Roles { get; set; }
        public RequestState RequestState { get; set; }
        public NetConnection Connection { get; set; }
    }

    public enum RequestState
    {
        /// <summary>
        /// Can not reach authenticator service
        /// </summary>
        EndpointFailure,
        /// <summary>
        /// Can reach authenticator service
        /// </summary>
        Success
    }
}

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
        Task<TripleDesInformation> GetKeyFromToken(string token);
    }

    public class TripleDesInformation
    {
        public byte[] Key { get; set; }
        public byte[] Iv { get; set; }
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
        Success,
        /// <summary>
        /// The user is already logged in to the service
        /// </summary>
        UserAlreadyLoggedIn,
        WrongCredentials
    }
}

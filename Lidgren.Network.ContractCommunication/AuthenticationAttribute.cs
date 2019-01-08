using System;

namespace Lidgren.Network.ContractCommunication
{
    public abstract class AuthenticationAttribute : Attribute
    {
        public abstract bool IsAuthorized<TUser>(NetConnection connection, CommunicationUser<TUser> user);
    }
}

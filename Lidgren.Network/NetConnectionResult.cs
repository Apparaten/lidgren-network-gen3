using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network
{
    /// <summary>
    /// 
    /// </summary>
    public enum NetConnectionResult
    {
        Unknown,
        NoResponseFromRemoteHost,
        Connected,
        WrongCredentials,
        WrongApplicationIdentifier,
        HandshakeDataValidationFailed,
        Reconnecting,
        RequestedDisconnect,
        ConnectionTimeOut,
        ShutDown,
        AwaitingApproval,
        LocallyRequestedConnect,
        RespondedConnect,
        UserAlreadyLoggedIn
    }
}

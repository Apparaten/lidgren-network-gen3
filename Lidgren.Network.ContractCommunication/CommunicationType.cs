﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lidgren.Network.ContractCommunication
{
    public enum CommunicationType
    {
        FireAndForget,
        Awaitable,
        AwaitableReturn
    }
}
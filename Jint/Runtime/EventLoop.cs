using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Runtime
{
    internal sealed record EventLoop
    {
        internal readonly Queue<Action> Events = new();
    }
}
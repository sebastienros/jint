using System;
using System.Collections.Generic;

namespace Jint.Runtime
{
    public sealed class EventLoop
    {
        internal readonly Queue<Action> PromiseContinuations = new();
    }
}
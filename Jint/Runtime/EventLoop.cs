using System;
using System.Collections.Generic;
using Jint.Native.Promise;

namespace Jint.Runtime
{
    internal sealed record EventLoop(Action OnFinished)
    {
        internal readonly Queue<Action> Events = new();
        internal readonly List<PromiseInstance> ManualPromises = new();
    }
}
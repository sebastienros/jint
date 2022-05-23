#nullable enable
using System;
using Esprima;
using Jint.Native;

namespace Jint.Runtime;

public abstract class JavaScriptException : JintException
{
    public abstract Location Location { get; }
    public abstract JsValue Error { get; }
    
    protected JavaScriptException()
    {
    }
    
    protected JavaScriptException(string? message)
        : base(message)
    {
    }

    protected JavaScriptException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

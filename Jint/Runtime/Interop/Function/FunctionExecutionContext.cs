using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;

#nullable disable

namespace Jint.Runtime.Interop.Function;

public class FunctionExecutingContext
{
    public Guid TraceId { get; set; }
    public Engine Engine { get; set; }
    public IFunction Function { get; set; }
    public FunctionInstance FunctionInstance { get; set; }
    public JsValue[] Arguments { get; set; }
}

public class FunctionExecutedContext
{
    public Guid TraceId { get; set; }
    public Completion? Result { get; set; }
}

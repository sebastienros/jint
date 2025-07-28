using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Jint.Tests;

public class RunnableInDebugOnlyAttribute : FactAttribute
{
    public RunnableInDebugOnlyAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (!Debugger.IsAttached)
        {
            Skip = "Only running in interactive mode.";
        }
    }
}

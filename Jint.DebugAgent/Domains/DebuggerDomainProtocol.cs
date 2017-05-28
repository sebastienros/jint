using System.Collections.Generic;
// ReSharper disable InconsistentNaming
#pragma warning disable 414

namespace Jint.DebugAgent.Domains
{
    /// <summary>
    /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/
    /// </summary>
    internal static class DebuggerDomainProtocol
    {
        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#event-scriptParsed
        /// </summary>
        public class ScriptParsedEventParameter
        {
            public string url;
            public string scriptId;
            public int startLine;
            public int startColumn;
            public int endLine;
            public int endColumn;
            public int executionContextId;
            public string hash;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#event-paused
        /// </summary>
        public class PausedEventParameters
        {
            public IEnumerable<CallFrame> callFrames;
            public string reason;
            public string[] hitBreakpoints;
            public AuxData data; //Object containing break-specific auxiliary properties. 
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-getScriptSource
        /// </summary>
        public class GetScriptSourceResult
        {
            public string scriptSource;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-evaluateOnCallFrame
        /// </summary>
        public class EvaluateOnCallFrameResult
        {
            public RuntimeDomainProtocol.RemoteObject result; // Object wrapper for the evaluation result.
            public RuntimeDomainProtocol.ExceptionDetails exceptionDetails; //optional Exception details.
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-setBreakpoint
        /// </summary>
        public class SetBreakpointResult
        {
            public string breakpointId;
            public Location[] locations;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#type-CallFrame
        /// </summary>
        public class CallFrame
        {
            public string callFrameId;
            public string functionName;
            public Location location;
            public Scope[] scopeChain;
            public RuntimeDomainProtocol.RemoteObject @this;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#type-Scope
        /// </summary>
        public class Scope
        {
            public string type;
            public RuntimeDomainProtocol.RemoteObject @object;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#type-Location
        /// </summary>
        public class Location
        {
            public string scriptId;
            public int lineNumber;
            public int columnNumber;
        }

        public class AuxData
        {
            public string description;
        }

        public enum PauseOnExceptionMode
        {
            None,
            Uncaught,
            All
        }
    }
}
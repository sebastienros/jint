// ReSharper disable InconsistentNaming
#pragma warning disable 414

namespace Jint.DebugAgent.Domains
{
    /// <summary>
    /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/
    /// </summary>
    internal static class RuntimeDomainProtocol
    {
        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#method-getProperties
        /// </summary>
        public class GetPropertiesResult
        {
            public PropertyDescriptor[] result;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-RemoteObject
        /// </summary>
        public class RemoteObject
        {
            public string type;
            public string value;
            public string description;
            public string objectId;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-PropertyDescriptor
        /// </summary>
        public class PropertyDescriptor
        {
            public string name; //Property name or symbol description.
            public RemoteObject value; //optionalRemoteObject  The value associated with the property.  
            public bool writable; //optional True if the value associated with the property may be changed (data descriptors only).  
            //public RemoteObject get;//optional A function which serves as a getter for the property, or undefined if there is no getter(accessor descriptors only).  
            //public RemoteObject set;//optional A function which serves as a setter for the property, or undefined if there is no setter(accessor descriptors only).  
            public bool configurable; //boolean True if the type of this property descriptor may be changed and if the property may be deleted from the corresponding object.  
            public bool enumerable; //True if this property shows up during enumeration of the properties on the corresponding object.  
            public bool wasThrown; //optionalTrue if the result was thrown during the evaluation.
            public bool isOwn; //optional True if the property is owned for the object.  
            public RemoteObject symbol; //optional Property symbol object, if the property is of the symbol type. 
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-ExceptionDetails
        /// </summary>
        public class ExceptionDetails
        {
            public int exceptionId;//Exception id.
            public string text;// Exception text, which should be used together with exception object when available.
            public int lineNumber;// Line number of the exception location(0-based).  
            public int columnNumber;//Column number of the exception location(0-based).  
            public string scriptId;//Script ID of the exception location.
            public string url;//URL of the exception location, to be used when the script was not reported.
            public StackTrace stackTrace;//optional JavaScript stack trace if available.
            public RemoteObject exception;//optional Exception object if available.
            public string executionContextId;//Identifier of the context where exception happened. 
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-StackTrace
        /// </summary>
        public class StackTrace
        {
            public string description;//optional String label of this stack trace.For async traces this may be a name of the function that initiated the async call.  
            public CallFrame[] callFrames;//JavaScript function name.  
            public StackTrace parent;//optional Asynchronous JavaScript stack trace that preceded this stack, if available.
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-CallFrame
        /// </summary>
        public class CallFrame
        {
            public string functionName; //JavaScript function name.
            public string scriptId; //JavaScript script id.
            public string url; //JavaScript script name or url.  
            public int lineNumber; //JavaScript script line number (0-based).  
            public int columnNumber; //JavaScript script column number(0-based). 
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#event-executionContextCreated
        /// </summary>
        public class ExecutionContextCreatedEvent
        {
            public ExecutionContextDescription context;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#type-ExecutionContextDescription
        /// </summary>
        public class ExecutionContextDescription
        {
            public string id;
            public string origin;
            public string name;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#event-executionContextDestroyed
        /// </summary>
        public class ExecutionContextDestroyedEvent
        {
            public string executionContextId;
        }
    }
}
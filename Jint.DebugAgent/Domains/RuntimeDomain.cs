using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using ExecutionContext = Jint.Runtime.Environments.ExecutionContext;

namespace Jint.DebugAgent.Domains
{
    /// <summary>
    /// Implements commands from the 'Debugger' Domain
    /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/
    /// </summary>
    internal class RuntimeDomain : DomainBase
    {
        private readonly ManualResetEvent continueExecutionEvent = new ManualResetEvent(true);
        private readonly Dictionary<string,WeakReference<ObjectInstance>> runtimeValues= new Dictionary<string, WeakReference<ObjectInstance>>();

        public RuntimeDomain(IDebugAgent debugAgent, bool waitForDebugger) : base("Runtime", debugAgent)
        {
            if (waitForDebugger)
            {
                this.continueExecutionEvent.Reset();
            }
        }

        public void NotifyDisconnected()
        {
            this.continueExecutionEvent.Set();
        }

        public void NotifyStep()
        {
            //Wait for debugger to connect (if opted for this)
            this.continueExecutionEvent.WaitOne();
        }

        #region Domain Methods
        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#method-enable
        /// </summary>
        [UsedImplicitly]
        private void Enable()
        {
            //this method is defined to signal the debugger that it is understood
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#method-runIfWaitingForDebugger
        /// </summary>
        [UsedImplicitly]
        private void RunIfWaitingForDebugger()
        {
            this.continueExecutionEvent.Set();
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#method-getProperties
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [UsedImplicitly]
        private RuntimeDomainProtocol.GetPropertiesResult GetProperties(string objectId)
        {
            ObjectInstance Value;
            WeakReference<ObjectInstance> ValueReference = this.runtimeValues.Where(_ => _.Key == objectId).Select(_=>_.Value).FirstOrDefault();
            if (ValueReference != null && ValueReference.TryGetTarget(out Value))
            {

                IEnumerable<KeyValuePair<string, JsValue>> KeyValuePairs;
                if (Value is EnvironmentRecord)
                {
                    KeyValuePairs = ((EnvironmentRecord) Value).GetAllBindingNames().Select(_ => new KeyValuePair<string, JsValue>(_, ((EnvironmentRecord)Value).GetBindingValue(_,false)));
                }
                else
                {
                    KeyValuePairs = Value.GetOwnProperties().Select(_=> new KeyValuePair<string, JsValue>(_.Key,_.Value.Value));
                }
                return new RuntimeDomainProtocol.GetPropertiesResult
                {
                    result = KeyValuePairs.Select(property => new RuntimeDomainProtocol.PropertyDescriptor
                    {
                        isOwn = true,configurable = false, name = property.Key, writable = false,value=GetRemoteObject(property.Value)
                    }).ToArray()
                };
            }
            else
            {
                return null;
            }
        }
        #endregion
        #region Domain events

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#event-executionContextCreated
        /// </summary>
        /// <param name="executionContext"></param>
        [UsedImplicitly]
        private void SendExecutionContextCreatedEvent(ExecutionContext executionContext)
        {
            this.Transmit("executionContextCreated", new RuntimeDomainProtocol.ExecutionContextCreatedEvent
            {
                context = new RuntimeDomainProtocol.ExecutionContextDescription
                {
                    id = GetIdFromHash(executionContext),
                    origin = "",
                    name = ""
                }
            });
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Runtime/#event-executionContextDestroyed
        /// </summary>
        /// <param name="executionContext"></param>
        [UsedImplicitly]
        private void SendExecutionContextDestroyedEvent(ExecutionContext executionContext)
        {
            this.Transmit("executionContextDestroyed", new RuntimeDomainProtocol.ExecutionContextDestroyedEvent
            {
                executionContextId= GetIdFromHash(executionContext)
            });
        }
        #endregion

        public RuntimeDomainProtocol.RemoteObject GetRemoteObject(ObjectInstance value, string description=null)
        {
            string ObjectId = GetIdFromHash(value);
            if (this.runtimeValues.ContainsKey(ObjectId) == false)
            {
                this.runtimeValues.Add(ObjectId, new WeakReference<ObjectInstance>(value));
            }

            return new RuntimeDomainProtocol.RemoteObject
            {
                type=GetObjectType(value),
                value=GetValue(value),
                description = description??value.ToString(),
                objectId=ObjectId,
            };
        }

        public RuntimeDomainProtocol.RemoteObject GetRemoteObject(JsValue value)
        {
            if (value.IsObject())
            {
                return this.GetRemoteObject(value.AsObject());
            }
            else
            {
                return new RuntimeDomainProtocol.RemoteObject
                {
                    type = GetObjectType(value),
                    value = GetValue(value),
                    description = value.ToString(),
                    objectId = GetIdFromHash(value),
                };
            }
        }

        private static string GetValue(JsValue value)
        {
            switch (value.Type)
            {
                case Types.None:
                    return "<none>";
                case Types.Undefined:
                    return "<undefined>";
                case Types.Null:
                    return "<null>";
                case Types.Boolean:
                    return value.AsBoolean().ToString();
                case Types.String:
                    return value.AsString();
                case Types.Number:
                    return value.AsNumber().ToString(CultureInfo.InvariantCulture);
                case Types.Object:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string GetObjectType(JsValue value)
        {
            /*typestring  Object type. Allowed values: object, function, undefined, string, number, boolean, symbol. */
            switch (value.Type)
            {
                case Types.Undefined:
                    return "undefined";
                case Types.Null:
                    return "object";
                case Types.Boolean:
                    return "boolean";
                case Types.String:
                    return "string";
                case Types.Number:
                    return "number";
                case Types.Object:
                    if (value.AsObject() is FunctionInstance)
                    {
                        return "function";
                    }
                    else
                    {
                        return "object";
                    }
                //case Types.None:
                default:
                    return "symbol";
            }
        }
    }
}
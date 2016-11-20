using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Jint.DebugAgent.Domains
{
    /// <summary>
    /// Implements base functionality for the domain implementations. Most of all this is a reflections-based
    /// Dispatching of debugger methods and results
    /// </summary>
    internal abstract class DomainBase
    {
        private readonly IDebugAgent debugAgent;
        public string Name { get; }

        protected DomainBase(string name, IDebugAgent debugAgent)
        {
            this.debugAgent = debugAgent;
            Name = name;
        }

        /// <summary>
        /// Process debugger emthods.
        /// The methods are named as specified int he debugger protocol (case insensitive) and may have an 'Async' postfix.
        /// If the return value is given, it is sent back to the debugger. If the result value is a task type, then an 'await' async
        /// call is made.
        /// </summary>
        public async Task<JObject> ProcessMessageAsync(string method, JObject parameter)
        {
            try
            {
                //Get class method for the message. Prefer Async implementations
                MethodInfo Method = this.GetType().GetMethod($"{method}Async", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? this.GetType().GetMethod($"{method}", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (Method != null)
                {
                    ParameterInfo[] Parameters = Method.GetParameters();
                    object[] Arguments = new object[Parameters.Length];
                    for (int Index = 0; Index < Parameters.Length; Index++)
                    {
                        JToken Value=null;
                        if (parameter?.TryGetValue(Parameters[Index].Name, StringComparison.InvariantCultureIgnoreCase, out Value)==true)
                        {
                            Arguments[Index] = Value.ToObject(Parameters[Index].ParameterType);
                        }
                        else
                        {
                            //parameter value not found in message, maybe its optional. use default(T)
                        }

                        //Set default if not set before
                        Arguments[Index] = Arguments[Index] ?? (Parameters[Index].ParameterType.IsValueType ? Activator.CreateInstance(Parameters[Index].ParameterType) : null);
                    }

                    //Call method in a typed manner
                    object Result = Method.Invoke(this, Arguments);

                    if (Method.ReturnType == typeof (void))
                    {
                        // there is no return value
                    }
                    else if (Method.ReturnType.IsGenericType)
                    {
                        if (Method.ReturnType.BaseType == typeof (Task))
                        {
                            //Result is a Task<T>. Await its content
                            Result = await (dynamic) Result;
                        }
                        else
                        {
                            //Leave Result
                        }
                    }
                    else
                    {
                        if (Method.ReturnType == typeof (Task))
                        {
                            await (Task) Result;
                            //there is no real return type, only a Task that was awaited before continuation
                        }
                        else
                        {
                            //Leave Result
                        }
                    }

                    //Null means, response must be sent but no content. Use empty JObject for that. Otherwise create Json from result
                    return Result != null ? JObject.FromObject(Result) : new JObject();
                }
                else
                {
                    //Method no found, ignore
                    return null;
                }
            }
            catch (Exception Exception)
            {
                Debug.WriteLine($"Exception calling {method}:{Exception.Message}");
                return null;
            }
        }

        protected void Transmit(string method, object parameters)
        {
           this.debugAgent.Transmit(this.Name, method, parameters);
        }

        protected Engine GetEngine(int engineId)
        {
            return this.debugAgent.GetEngine(engineId);
        }

        protected IEnumerable<Engine> GetEngines()
        {
            return this.debugAgent.GetEngines();
        }

        protected static string GetIdFromHash(object value)
        {
            return value.GetHashCode().ToString("X8");
        }
    }
}
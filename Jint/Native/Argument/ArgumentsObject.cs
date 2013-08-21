using System;
using System.Collections.Generic;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;

namespace Jint.Native.Argument
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.6
    /// </summary>
    public class ArgumentsInstance : ObjectInstance
    {
        public ArgumentsInstance(Engine engine) : base(engine)
        {
            // todo: complete implementation
        }

        public static ArgumentsInstance CreateArgumentsObject(Engine engine, FunctionInstance func, string[] names, object[] args, EnvironmentRecord env, bool strict)
        {
            var len = args.Length;
            var obj = new ArgumentsInstance(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.DefineOwnProperty("length", new DataDescriptor(len) { Writable = true, Enumerable = false, Configurable = true }, false);
            var map = engine.Object.Construct(Arguments.Empty);
            var mappedNamed = new List<string>();
            var indx = len - 1;
            while (indx >= 0)
            {
                var indxStr = TypeConverter.ToString(indx);
                var val = args[indx];
                obj.DefineOwnProperty(indxStr, new DataDescriptor(val) { Writable = true, Enumerable = false, Configurable = true }, false);
                if (indx < names.Length)
                {
                    var name = names[indx];
                    if (!strict && !mappedNamed.Contains(name))
                    {
                        mappedNamed.Add(name);
                        Func<string, object> g = n => env.GetBindingValue(name, false);
                        var p = new Action<string, object>((n, o) => env.SetMutableBinding(name, o, true));

                        map.DefineOwnProperty(indxStr, new ClrAccessDescriptor<string>(engine, g, p) { Configurable = true }, false);
                    }
                }
                indx--;
            }
            if (mappedNamed.Count > 0)
            {
                obj.ParameterMap = map;
            }
            if (!strict)
            {
                obj.DefineOwnProperty("callee",
                                      new DataDescriptor(func)
                                      {
                                          Writable = true,
                                          Enumerable = false,
                                          Configurable = false
                                      }, false);
            }
            else
            {
                var thrower = engine.Function.ThrowTypeError;
                obj.DefineOwnProperty("caller", new AccessorDescriptor(thrower, thrower) { Enumerable = false, Configurable = false }, false);
                obj.DefineOwnProperty("callee", new AccessorDescriptor(thrower, thrower) { Enumerable = false, Configurable = false }, false);
            }

            return obj;
        }

        public ObjectInstance ParameterMap { get; set; }

        public override string Class
        {
            get
            {
                return "Arguments";
            }
        }



    }
}

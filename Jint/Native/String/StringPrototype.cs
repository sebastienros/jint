using System;
using Jint.Runtime.Interop;

namespace Jint.Native.String
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.5.4
    /// </summary>
    public sealed class StringPrototype : StringInstance
    {
        private StringPrototype(Engine engine)
            : base(engine)
        {
        }

        public static StringPrototype CreatePrototypeObject(Engine engine, StringConstructor stringConstructor)
        {
            var obj = new StringPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = "";

            obj.FastAddProperty("constructor", stringConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToStringString), false, false, false);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, object>(Engine, ValueOf), false, false, false);
            FastAddProperty("charAt", new ClrFunctionInstance<object, object>(Engine, CharAt), false, false, false);
            FastAddProperty("charCodeAt", new ClrFunctionInstance<object, object>(Engine, CharCodeAt), false, false, false);
            FastAddProperty("concat", new ClrFunctionInstance<object, object>(Engine, Concat), false, false, false);
            FastAddProperty("indexOf", new ClrFunctionInstance<object, object>(Engine, IndexOf), false, false, false);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance<object, object>(Engine, LastIndexOf), false, false, false);
            FastAddProperty("localeCompare", new ClrFunctionInstance<object, object>(Engine, LocaleCompare), false, false, false);
            FastAddProperty("match", new ClrFunctionInstance<object, object>(Engine, Match), false, false, false);
            FastAddProperty("replace", new ClrFunctionInstance<object, object>(Engine, Replace), false, false, false);
            FastAddProperty("search", new ClrFunctionInstance<object, object>(Engine, Search), false, false, false);
            FastAddProperty("slice", new ClrFunctionInstance<object, object>(Engine, Slice), false, false, false);
            FastAddProperty("split", new ClrFunctionInstance<object, object>(Engine, Split), false, false, false);
            FastAddProperty("substring", new ClrFunctionInstance<object, object>(Engine, Substring), false, false, false);
            FastAddProperty("toLowerCase", new ClrFunctionInstance<object, object>(Engine, ToLowerCase), false, false, false);
            FastAddProperty("toLocaleLowerCase", new ClrFunctionInstance<object, object>(Engine, ToLocaleLowerCase), false, false, false);
            FastAddProperty("toUpperCase", new ClrFunctionInstance<object, object>(Engine, ToUpperCase), false, false, false);
            FastAddProperty("toLocaleUpperCase", new ClrFunctionInstance<object, object>(Engine, ToLocaleUpperCase), false, false, false);
            FastAddProperty("trim", new ClrFunctionInstance<object, object>(Engine, Trim), false, false, false);
        }
        private object Trim(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ToLocaleUpperCase(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ToUpperCase(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ToLocaleLowerCase(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ToLowerCase(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Substring(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Split(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Slice(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Search(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Replace(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Match(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object LocaleCompare(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object LastIndexOf(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object IndexOf(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object Concat(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object CharCodeAt(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object CharAt(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ValueOf(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
        private object ToStringString(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
    }
}

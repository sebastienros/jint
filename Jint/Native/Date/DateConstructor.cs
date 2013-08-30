using System;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    public sealed class DateConstructor : FunctionInstance, IConstructor
    {
        private static readonly DateTime Origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateConstructor(Engine engine) : base(engine, null, null, false)
        {
        }

        public static DateConstructor CreateDateConstructor(Engine engine)
        {
            var obj = new DateConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Date constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = DatePrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 7, false, false, false);

            // The initial value of Date.prototype is the Date prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("parse", new ClrFunctionInstance<object, object>(Engine, Parse, 0), true, false, true);
            FastAddProperty("UTC", new ClrFunctionInstance<object, object>(Engine, Utc, 0), true, false, true);
            FastAddProperty("now", new ClrFunctionInstance<object, object>(Engine, Now, 0), true, false, true);
        }

        private object Parse(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object Utc(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object Now(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.3
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(object[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Construct(DateTime.Now);
            }
            else if (arguments.Length == 1)
            {
                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (TypeConverter.GetType(v) == TypeCode.String)
                {
                    var d = DateTime.Parse(TypeConverter.ToString(v));
                    return Construct(d);
                }

                return Construct(TypeConverter.ToNumber(v));
            }
            else
            {
                var y = TypeConverter.ToNumber(arguments[0]);
                var m = (int) TypeConverter.ToInteger(arguments[0]);
                var date = arguments.Length > 2 ? (int)TypeConverter.ToInteger(arguments[2]) : 1;
                var hours = arguments.Length > 3 ? (int)TypeConverter.ToInteger(arguments[3]) : 0;
                var minutes = arguments.Length > 4 ? (int)TypeConverter.ToInteger(arguments[4]) : 0;
                var seconds = arguments.Length > 5 ? (int)TypeConverter.ToInteger(arguments[5]) : 0;
                var ms = arguments.Length > 6 ? (int)TypeConverter.ToInteger(arguments[6]) : 0;

                if ((!double.IsNaN(y)) && (0 <= TypeConverter.ToInteger(y)) && (TypeConverter.ToInteger(y) <= 99))
                {
                    y += 1900;
                }

                return Construct(new DateTime((int) y, m + 1, date, hours, minutes, seconds, ms, DateTimeKind.Utc));
            }
        }

        public DatePrototype PrototypeObject { get; private set; }

        public DateInstance Construct(DateTime value)
        {
            var instance = new DateInstance(Engine)
                {
                    Prototype = PrototypeObject,
                    PrimitiveValue = (value - Origin).TotalMilliseconds,
                    Extensible = true
                };

            return instance;
        }

        public DateInstance Construct(double time)
        {
            var instance = new DateInstance(Engine)
                {
                    Prototype = PrototypeObject,
                    PrimitiveValue = TimeClip(time),
                    Extensible = true
                };

            return instance;
        }

        public double TimeClip(double time)
        {
            if (double.IsInfinity(time) || double.IsNaN(time))
            {
                return double.NaN;
            }

            if (System.Math.Abs(time) > 8640000000000000)
            {
                return double.NaN;
            }

            return TypeConverter.ToInteger(time);
        }
    }
}

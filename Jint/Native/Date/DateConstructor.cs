using System;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Date
{
    public sealed class DateConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public DateConstructor(Engine engine)
            : base(engine, new ObjectInstance(engine.Object), null, null)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            this.Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            this.Prototype.DefineOwnProperty("prototype", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

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
                return Construct(DateTime.UtcNow);
            }
            else if (arguments.Length == 1)
            {
                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (TypeConverter.GetType(v) == TypeCode.String)
                {
                    var d = DateTime.Parse(TypeConverter.ToString(v));
                    return Construct(d);
                }

                v = TypeConverter.ToNumber(v);

                // todo: implement TimeClip
                return Construct(new DateTime(TypeConverter.ToUint32(v)));
            }
            else
            {
                var y = TypeConverter.ToNumber(arguments[0]);
                var m = TypeConverter.ToInteger(arguments[0]);
                var date = arguments.Length > 2 ? TypeConverter.ToInteger(arguments[2]) : 1;
                var hours = arguments.Length > 3 ? TypeConverter.ToInteger(arguments[3]) : 0;
                var minutes = arguments.Length > 4 ? TypeConverter.ToInteger(arguments[4]) : 0;
                var seconds = arguments.Length > 5 ? TypeConverter.ToInteger(arguments[5]) : 0;
                var ms = arguments.Length > 6 ? TypeConverter.ToInteger(arguments[6]) : 0;

                if ((!double.IsNaN(y)) && (0 <= TypeConverter.ToInteger(y)) && (TypeConverter.ToInteger(y) <= 99))
                {
                    y += 1900;
                }

                return Construct(new DateTime((int) y, m + 1, date, hours, minutes, seconds, ms, DateTimeKind.Utc));
            }
        }

        public DateInstance Construct(DateTime value)
        {
            var instance = new DateInstance(Prototype);
            instance.PrimitiveValue = value;
            return instance;
        }


    }
}

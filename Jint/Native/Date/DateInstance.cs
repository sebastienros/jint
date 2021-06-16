using System;
using System.Globalization;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Date
{
    public class DateInstance : ObjectInstance
    {
        // Maximum allowed value to prevent DateTime overflow
        private static readonly double Max = (DateTime.MaxValue - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        // Minimum allowed value to prevent DateTime overflow
        private static readonly double Min = -(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) - DateTime.MinValue).TotalMilliseconds;

        public DateInstance(Engine engine)
            : base(engine, ObjectClass.Date)
        {
            PrimitiveValue = double.NaN;
        }

        public DateTime ToDateTime()
        {
            if (DateTimeRangeValid)
            {
                return DateConstructor.Epoch.AddMilliseconds(PrimitiveValue);
            }

            ExceptionHelper.ThrowRangeError(_engine.Realm);
            return DateTime.MinValue;
        }

        public double PrimitiveValue { get; set; }

        internal bool DateTimeRangeValid => !double.IsNaN(PrimitiveValue) && PrimitiveValue <= Max && PrimitiveValue >= Min;

        public override string ToString()
        {
            if (double.IsNaN(PrimitiveValue))
            {
                return "NaN";
            }

            if (double.IsInfinity(PrimitiveValue))
            {
                return "Infinity";
            }

            return ToDateTime().ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'K", CultureInfo.InvariantCulture);
        }
    }
}

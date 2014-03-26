using System;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        public object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsEnum)
            {
                var integer = System.Convert.ChangeType(value, typeof (int), formatProvider);
                if (integer == null)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return Enum.ToObject(type, integer);
            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }
    }
}

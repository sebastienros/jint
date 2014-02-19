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

            return System.Convert.ChangeType(value, type, formatProvider);
        }
    }
}

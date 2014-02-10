using System;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        public object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            return System.Convert.ChangeType(value, type, formatProvider);
        }
    }
}

using System;
using System.Collections.Generic;
using Jint.Native;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private static readonly Dictionary<string, bool> KnownConversions = new Dictionary<string, bool>();
        private static readonly object LockObject = new object();

        public object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsEnum)
            {
                var integer = System.Convert.ChangeType(value, typeof(int), formatProvider);
                if (integer == null)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return Enum.ToObject(type, integer);
            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }

        public bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
        {
            bool canConvert;
            var key = value == null ? String.Format("Null->{0}", type) : String.Format("{0}->{1}", value.GetType(), type);

            if (!KnownConversions.TryGetValue(key, out canConvert))
            {
                lock (LockObject)
                {
                    if (!KnownConversions.TryGetValue(key, out canConvert))
                    {
                        try
                        {
                            converted = Convert(value, type, formatProvider);
                            KnownConversions.Add(key, true);
                            return true;
                        }
                        catch
                        {
                            converted = null;
                            KnownConversions.Add(key, false);
                            return false;
                        }
                    }
                }
            }

            if (canConvert)
            {
                converted = Convert(value, type, formatProvider);
                return true;
            }

            converted = null;
            return false;
        }
    }
}

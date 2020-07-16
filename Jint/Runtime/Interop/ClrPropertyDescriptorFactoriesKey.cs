using System;

namespace Jint.Runtime.Interop
{
    internal readonly struct ClrPropertyDescriptorFactoriesKey : IEquatable<ClrPropertyDescriptorFactoriesKey>
    {
        public ClrPropertyDescriptorFactoriesKey(Type type, Key propertyName)
        {
            Type = type;
            PropertyName = propertyName;
        }

        private readonly Type Type;
        private readonly Key PropertyName;

        public bool Equals(ClrPropertyDescriptorFactoriesKey other)
        {
            return Type == other.Type && PropertyName == other.PropertyName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is ClrPropertyDescriptorFactoriesKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Type.GetHashCode() * 397) ^ PropertyName.GetHashCode();
            }
        }
    }
}

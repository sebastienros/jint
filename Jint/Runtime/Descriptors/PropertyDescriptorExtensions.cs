using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Descriptors
{
    public static class PropertyDescriptorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAccessorDescriptor(this IPropertyDescriptor descriptor)
        {
            return descriptor.Get != null || descriptor.Set != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDataDescriptor(this IPropertyDescriptor descriptor)
        {
            return descriptor.Writable.HasValue || descriptor.Value != null;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.3
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGenericDescriptor(this IPropertyDescriptor descriptor)
        {
            return !descriptor.IsDataDescriptor() && !descriptor.IsAccessorDescriptor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetValue(this IPropertyDescriptor descriptor, ObjectInstance thisArg, out JsValue value)
        {
            value = JsValue.Undefined;

            if (descriptor == PropertyDescriptor.Undefined)
            {
                value = JsValue.Undefined;
                return false;
            }

            if (descriptor.IsDataDescriptor())
            {
                var val = descriptor.Value;
                if (val != null)
                {
                    value = val;
                    return true;
                }
            }

            if (descriptor.Get != null && !descriptor.Get.IsUndefined())
            {
                // if getter is not undefined it must be ICallable
                var callable = descriptor.Get.TryCast<ICallable>();
                value = callable.Call(thisArg, Arguments.Empty);
            }

            return true;
        }
    }
}
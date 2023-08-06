using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class ReflectionDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly ReflectionAccessor _reflectionAccessor;
        private readonly object _target;

        public ReflectionDescriptor(
            Engine engine,
            ReflectionAccessor reflectionAccessor,
            object target,
            bool enumerable)
            : base((enumerable ? PropertyFlag.Enumerable : PropertyFlag.None) | PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _reflectionAccessor = reflectionAccessor;
            _target = target;
            Writable = reflectionAccessor.Writable && engine.Options.Interop.AllowWrite;
        }


        protected internal override JsValue? CustomValue
        {
            get
            {
                var value = _reflectionAccessor.GetValue(_engine, _target);
                var type = _reflectionAccessor.MemberType;
                return JsValue.FromObjectWithType(_engine, value, type);
            }
            set
            {
                try
                {
                    _reflectionAccessor.SetValue(_engine, _target, value!);
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }
        }
    }
}

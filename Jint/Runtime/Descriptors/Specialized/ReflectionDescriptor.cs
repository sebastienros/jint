using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop;
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
            _flags |= PropertyFlag.NonData;
            _engine = engine;
            _reflectionAccessor = reflectionAccessor;
            _target = target;

            if (reflectionAccessor.Writable && engine.Options.Interop.AllowWrite)
            {
                Set = new SetterFunctionInstance(_engine, DoSet);
            }
            if (reflectionAccessor.Readable)
            {
                Get = new GetterFunctionInstance(_engine, DoGet);
            }
        }

        public override JsValue? Get { get; }
        public override JsValue? Set { get; }


        protected internal override JsValue? CustomValue
        {
            get => DoGet(null);
            set => DoSet(null, value);
        }

        private JsValue DoGet(JsValue? thisObj)
        {
            var value = _reflectionAccessor.GetValue(_engine, _target);
            var type = _reflectionAccessor.MemberType;
            return JsValue.FromObjectWithType(_engine, value, type);
        }

        private void DoSet(JsValue? thisObj, JsValue? v)
        {
            try
            {
                _reflectionAccessor.SetValue(_engine, _target, v!);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(_engine, exception);
            }
        }
    }
}

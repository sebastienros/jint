using System.Dynamic;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class DynamicObjectAccessor : ReflectionAccessor
    {
        private readonly string _memberName;
        private JintSetMemberBinder? _setter;
        private JintGetMemberBinder? _getter;

        public DynamicObjectAccessor(
            Type memberType,
            string memberName,
            PropertyInfo? indexer = null) : base(memberType, memberName, indexer)
        {
            _memberName = memberName;
        }

        public override bool Writable => true;

        protected override object? DoGetValue(object target)
        {
            var dynamicObject = (DynamicObject) target;
            var getter = _getter ??= new JintGetMemberBinder(_memberName, ignoreCase: true);
            dynamicObject.TryGetMember(getter, out var result);
            return result;
        }

        protected override void DoSetValue(object target, object? value)
        {
            var dynamicObject = (DynamicObject) target;
            var setter = _setter ??= new JintSetMemberBinder(_memberName, ignoreCase: true);
            dynamicObject.TrySetMember(setter, value);
        }

        protected override object? ConvertValueToSet(Engine engine, object value)
        {
            // we expect value to be generally CLR type, convert when possible
            return value switch
            {
                JsBoolean jsBoolean => jsBoolean._value ? JsBoolean.BoxedTrue : JsBoolean.BoxedFalse,
                JsString jsString => jsString.ToString(),
                JsNumber jsNumber => jsNumber._value,
                JsNull => null,
                JsUndefined => null,
                _ => value
            };
        }

        private sealed class JintGetMemberBinder : GetMemberBinder
        {
            public JintGetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
            {
            }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
            {
                throw new NotImplementedException(nameof(FallbackGetMember) + " not implemented");
            }
        }

        private sealed class JintSetMemberBinder : SetMemberBinder
        {
            public JintSetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
            {
            }

            public override DynamicMetaObject FallbackSetMember(
                DynamicMetaObject target,
                DynamicMetaObject value,
                DynamicMetaObject? errorSuggestion)
            {
                throw new NotImplementedException(nameof(FallbackSetMember) + " not implemented");
            }
        }
    }
}

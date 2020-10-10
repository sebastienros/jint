using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintTaggedTemplateExpression : JintExpression
    {
        internal static  readonly JsString PropertyRaw = new JsString("raw");

        private readonly TaggedTemplateExpression _taggedTemplateExpression;
        private JintExpression _tagIdentifier;
        private JintTemplateLiteralExpression _quasi;

        public JintTaggedTemplateExpression(TaggedTemplateExpression expression) : base(expression)
        {
            _taggedTemplateExpression = expression;
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _tagIdentifier = Build(engine, _taggedTemplateExpression.Tag);
            _quasi = new JintTemplateLiteralExpression(_taggedTemplateExpression.Quasi);
            _quasi.DoInitialize(context);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var tagger = engine.GetValue(_tagIdentifier.GetValue(context)) as ICallable;
            if (tagger is null)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "Argument must be callable");
            }

            var expressions = _quasi._expressions;

            var args = engine._jsValueArrayPool.RentArray(expressions.Length + 1);

            var template = GetTemplateObject(context);
            args[0] = template;

            for (int i = 0; i < expressions.Length; ++i)
            {
                args[i + 1] = expressions[i].GetValue(context).Value;
            }

            var result = tagger.Call(JsValue.Undefined, args);
            engine._jsValueArrayPool.ReturnArray(args);

            return NormalCompletion(result);
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-gettemplateobject
        /// </summary>
        private ArrayInstance GetTemplateObject(EvaluationContext context)
        {
            var count = (uint) _quasi._templateLiteralExpression.Quasis.Count;
            var template = context.Engine.Realm.Intrinsics.Array.ConstructFast(count);
            var rawObj = context.Engine.Realm.Intrinsics.Array.ConstructFast(count);
            for (uint i = 0; i < _quasi._templateLiteralExpression.Quasis.Count; ++i)
            {
                var templateElementValue = _quasi._templateLiteralExpression.Quasis[(int) i].Value;
                template.SetIndexValue(i, templateElementValue.Cooked, updateLength: false);
                rawObj.SetIndexValue(i, templateElementValue.Raw, updateLength: false);
            }

            template.DefineOwnProperty(PropertyRaw, new PropertyDescriptor(rawObj, PropertyFlag.AllForbidden));
            return template;
        }
    }
}
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintTaggedTemplateExpression : JintExpression
    {
        private readonly TaggedTemplateExpression _taggedTemplateExpression;
        private JintExpression _tagIdentifier;
        private JintTemplateLiteralExpression _quasi;

        public JintTaggedTemplateExpression(Engine engine, TaggedTemplateExpression expression) : base(engine, expression)
        {
            _taggedTemplateExpression = expression;
            _initialized = false;
        }

        protected override void Initialize()
        {
            _tagIdentifier = Build(_engine, _taggedTemplateExpression.Tag);
            _quasi = (JintTemplateLiteralExpression) Build(_engine, _taggedTemplateExpression.Quasi);
            _quasi.DoInitialize();
        }

        protected override object EvaluateInternal()
        {
            var tagger = _engine.GetValue(_tagIdentifier.GetValue()) as ICallable
                         ?? ExceptionHelper.ThrowTypeError<ICallable>(_engine, "Argument must be callable");

            var expressions = _quasi._expressions;

            var args = _engine._jsValueArrayPool.RentArray((expressions.Length + 1));

            var template = GetTemplateObject();
            args[0] = template;

            for (int i = 0; i < expressions.Length; ++i)
            {
                args[i + 1] = expressions[i].GetValue();
            }

            var result = tagger.Call(JsValue.Undefined, args);
            _engine._jsValueArrayPool.ReturnArray(args);

            return result;
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-gettemplateobject
        /// </summary>
        private ArrayInstance GetTemplateObject()
        {
            var count = (uint) _quasi._templateLiteralExpression.Quasis.Count;
            var template = _engine.Array.ConstructFast(count);
            var rawObj = _engine.Array.ConstructFast(count);
            for (int i = 0; i < _quasi._templateLiteralExpression.Quasis.Count; ++i)
            {
                var templateElementValue = _quasi._templateLiteralExpression.Quasis[i].Value;
                template.SetIndexValue((uint) i, templateElementValue.Cooked, updateLength: false);
                rawObj.SetIndexValue((uint) i, templateElementValue.Raw, updateLength: false);
            }

            template.DefineOwnProperty("raw", new PropertyDescriptor(rawObj, PropertyFlag.AllForbidden), false);
            return template;
        }
    }
}
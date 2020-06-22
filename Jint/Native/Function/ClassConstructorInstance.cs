#nullable enable

using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.Function
{
    internal sealed class ClassConstructorInstance : FunctionInstance, IConstructor
    {
        private readonly Expression? _superClass;
        private readonly ClassBody _body;

        public ClassConstructorInstance(
            Engine engine,
            Expression? superClass,
            ClassBody body,
            LexicalEnvironment scope,
            string? name = null) : base(engine, name != null ? new JsString(name) : null, FunctionThisMode.Global)
        {
            _superClass = superClass;
            _body = body;
            _environment = scope;

            var env = _engine.ExecutionContext.LexicalEnvironment;
            var classScope = LexicalEnvironment.NewDeclarativeEnvironment(_engine, env);

            ObjectInstance? protoParent = null;
            JsValue? constructorParent = null;
            if (_superClass is null)
            {
                protoParent = _engine.Object.PrototypeObject;
                constructorParent = _engine.Function.PrototypeObject;
            }
            else
            {
                _engine.ExecutionContext.UpdateLexicalEnvironment(classScope);
                var superclassRef = JintExpression.Build(_engine, _superClass).GetValue();
                _engine.ExecutionContext.UpdateLexicalEnvironment(env);

                var superclass = _engine.GetValue(superclassRef);

                if (superclass.IsNull())
                {
                    protoParent = null;
                    constructorParent = _engine.Function.PrototypeObject;
                }
                else if (!superclass.IsConstructor)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "super class is not a constructor");
                }
                else
                {
                    var temp = superclass.Get("prototype");
                    if (temp is ObjectInstance protoParentObject)
                    {
                        protoParent = protoParentObject;
                    }
                    else if (temp._type == InternalTypes.Null)
                    {
                        // OK
                    }
                    else
                    {
                        ExceptionHelper.ThrowTypeError(_engine);
                        return;
                    }

                    constructorParent = superclass;
                }
            }

            var proto = _engine.Object.Construct(Arguments.Empty);
            proto._prototype = protoParent;

            MethodDefinition? constructor = null;
            var classBody = _body.Body;
            for (var i = 0; i < classBody.Count; ++i)
            {
                if (classBody[i].Kind == PropertyKind.Constructor)
                {
                    constructor = (MethodDefinition) classBody[i];
                    break;
                }
            }

            if (constructor is null)
            {
                string constructorText;
                if (_superClass != null)
                {
                    constructorText = "class temp { constructor(...args) { super(...args); } }";
                }
                else
                {
                    constructorText = "class temp { constructor() {} }";
                }

                var parser = new JavaScriptParser(constructorText, Engine.DefaultParserOptions);
                var script = parser.ParseScript();
                constructor = (MethodDefinition) script.Body[0].ChildNodes[2].ChildNodes[0];
            }

            _engine.ExecutionContext.UpdateLexicalEnvironment(classScope);

            foreach (var classProperty in _body.Body)
            {
                if (classProperty is not MethodDefinition m)
                {
                    continue;
                }

                var target = !m.Static ? proto : this;
                var property = TypeConverter.ToPropertyKey(m.GetKey(_engine));
                var valueExpression = JintExpression.Build(_engine, m.Value);
                PropertyDefinitionEvaluation(
                    target,
                    m, 
                    property, 
                    valueExpression, 
                    isStrictModeCode: true);
            }

            _prototype = proto;
            //var temp = constructorParent as ObjectInstance ?? _engine.Function.PrototypeObject;
            _prototypeDescriptor = new PropertyDescriptor(proto, PropertyFlag.AllForbidden);

            _engine.ExecutionContext.UpdateLexicalEnvironment(env);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var obj = new ObjectInstance(_engine)
            {
                _prototype = this
            };
            return obj;
        }

        public override string ToString()
        {
            // TODO no way to extract SourceText from Esprima at the moment, just returning native code
            var nameValue = _nameDescriptor != null ? UnwrapJsValue(_nameDescriptor) : JsString.Empty;
            var name = "";
            if (!nameValue.IsUndefined())
            {
                name = TypeConverter.ToString(nameValue);
            }
            return "function " + name + "() {{[native code]}}";
        }
        
        private void PropertyDefinitionEvaluation(
            ObjectInstance obj,
            MethodDefinition property,
            JsValue propName,
            JintExpression valueExpression,
            bool isStrictModeCode)
        {
            PropertyDescriptor? propDesc;
            if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
            {
                var function = property.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(obj.Engine);

                var functionInstance = new ScriptFunctionInstance(
                    obj.Engine,
                    function,
                    obj.Engine.ExecutionContext.LexicalEnvironment,
                    isStrictModeCode
                );
                functionInstance.SetFunctionName(propName);
                functionInstance._prototypeDescriptor = null;

                propDesc = new GetSetPropertyDescriptor(
                    get: property.Kind == PropertyKind.Get ? functionInstance : null,
                    set: property.Kind == PropertyKind.Set ? functionInstance : null,
                    PropertyFlag.Enumerable | PropertyFlag.Configurable);
            }
            else
            {
                var expr = valueExpression;
                var propValue = expr.GetValue().Clone();
                if (expr._expression.IsFunctionWithName())
                {
                    var functionInstance = (FunctionInstance) propValue;
                    functionInstance.SetFunctionName(propName);
                }

                propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }

            obj.DefinePropertyOrThrow(propName, propDesc);
        }
    }
}
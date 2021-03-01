using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint
{
    public partial class Engine
    {
        internal async Task<JsValue> CallAsync(ICallable callable, JsValue thisObject, JsValue[] arguments, JintExpression expression)
        {
            if (callable is FunctionInstance functionInstance)
            {
                return await CallAsync(functionInstance, thisObject, arguments, expression);
            }

            return await callable.CallAsync(thisObject, arguments);
        }

        internal async Task<JsValue> CallAsync(
            FunctionInstance functionInstance,
            JsValue thisObject,
            JsValue[] arguments,
            JintExpression expression)
        {
            var callStackElement = new CallStackElement(functionInstance, expression);
            var recursionDepth = CallStack.Push(callStackElement);

            if (recursionDepth > Options.MaxRecursionDepth)
            {
                // pop the current element as it was never reached
                CallStack.Pop();
                ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack, callStackElement.ToString());
            }

            if (_isDebugMode)
            {
                DebugHandler.AddToDebugCallStack(functionInstance);
            }

            var result = await functionInstance.CallAsync(thisObject, arguments);

            if (_isDebugMode)
            {
                DebugHandler.PopDebugCallStack();
            }

            CallStack.Pop();

            return result;
        }

        public Task<Engine> ExecuteAsync(string source)
        {
            return ExecuteAsync(source, DefaultParserOptions);
        }

        public Task<Engine> ExecuteAsync(string source, ParserOptions parserOptions)
        {
            var parser = new JavaScriptParser(source, parserOptions);
            return ExecuteAsync(parser.ParseScript());
        }

        public async Task<Engine> ExecuteAsync(Script program)
        {
            ResetConstraints();
            ResetLastStatement();
            ResetCallStack();

            using (new StrictModeScope(_isStrict || program.Strict))
            {
                GlobalDeclarationInstantiation(
                    program,
                    GlobalEnvironment);

                var list = new JintStatementList(this, null, program.Body);

                var result = await list.ExecuteAsync();
                if (result.Type == CompletionType.Throw)
                {
                    var ex = new JavaScriptException(result.GetValueOrDefault()).SetCallstack(this, result.Location);
                    throw ex;
                }

                _completionValue = result.GetValueOrDefault();
            }

            return this;
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="propertyName">The name of the function to call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public Task<JsValue> InvokeAsync(string propertyName, params object[] arguments)
        {
            return InvokeAsync(propertyName, null, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="propertyName">The name of the function to call.</param>
        /// <param name="thisObj">The this value inside the function call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public Task<JsValue> InvokeAsync(string propertyName, object thisObj, object[] arguments)
        {
            var value = GetValue(propertyName);

            return InvokeAsync(value, thisObj, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="value">The function to call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public Task<JsValue> InvokeAsync(JsValue value, params object[] arguments)
        {
            return InvokeAsync(value, null, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="value">The function to call.</param>
        /// <param name="thisObj">The this value inside the function call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public async Task<JsValue> InvokeAsync(JsValue value, object thisObj, object[] arguments)
        {
            var callable = value as ICallable ?? ExceptionHelper.ThrowArgumentException<ICallable>("Can only invoke functions");

            var items = _jsValueArrayPool.RentArray(arguments.Length);
            for (int i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            var result = await callable.CallAsync(JsValue.FromObject(this, thisObj), items);
            _jsValueArrayPool.ReturnArray(items);

            return result;
        }

        async internal Task<JsValue> GetValueAsync(object value, bool returnReferenceToPool)
        {
            if (value is JsValue jsValue)
            {
                return jsValue;
            }

            if (!(value is Reference reference))
            {
                return ((Completion)value).Value;
            }

            return await GetValueAsync(reference, returnReferenceToPool);
        }

        async internal Task<JsValue> GetValueAsync(Reference reference, bool returnReferenceToPool)
        {
            var baseValue = reference.GetBase();

            if (baseValue._type == InternalTypes.Undefined)
            {
                if (_referenceResolver.TryUnresolvableReference(this, reference, out JsValue val))
                {
                    return val;
                }

                ExceptionHelper.ThrowReferenceError(this, reference);
            }

            if ((baseValue._type & InternalTypes.ObjectEnvironmentRecord) == 0
                && _referenceResolver.TryPropertyReference(this, reference, ref baseValue))
            {
                return baseValue;
            }

            if (reference.IsPropertyReference())
            {
                var property = reference.GetReferencedName();
                if (returnReferenceToPool)
                {
                    _referencePool.Return(reference);
                }

                if (baseValue.IsObject())
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    var v = await o.GetAsync(property);
                    return v;
                }
                else
                {
                    // check if we are accessing a string, boxing operation can be costly to do index access
                    // we have good chance to have fast path with integer or string indexer
                    ObjectInstance o = null;
                    if ((property._type & (InternalTypes.String | InternalTypes.Integer)) != 0
                        && baseValue is JsString s
                        && TryHandleStringValue(property, s, ref o, out var jsValue))
                    {
                        return jsValue;
                    }

                    if (o is null)
                    {
                        o = TypeConverter.ToObject(this, baseValue);
                    }

                    var desc = o.GetProperty(property);
                    if (desc == PropertyDescriptor.Undefined)
                    {
                        return JsValue.Undefined;
                    }

                    if (desc.IsDataDescriptor())
                    {
                        return desc.Value;
                    }

                    var getter = desc.Get;
                    if (getter.IsUndefined())
                    {
                        return Undefined.Instance;
                    }

                    var callable = (ICallable)getter.AsObject();
                    return await callable.CallAsync(baseValue, Arguments.Empty);
                }
            }

            if (!(baseValue is EnvironmentRecord record))
            {
                return ExceptionHelper.ThrowArgumentException<JsValue>();
            }

            var bindingValue = record.GetBindingValue(reference.GetReferencedName().ToString(), reference.IsStrictReference());

            if (returnReferenceToPool)
            {
                _referencePool.Return(reference);
            }

            return bindingValue;
        }
    }
}
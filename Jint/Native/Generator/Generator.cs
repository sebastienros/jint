using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Generator
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-generator-instances
    /// </summary>
    internal sealed class Generator : ObjectInstance
    {
        internal GeneratorState _generatorState;
        private ExecutionContext _generatorContext;
        private readonly object _generatorBrand = null;
        private FunctionInstance _generatorBody;

        public Generator(Engine engine) : base(engine)
        {
            _prototype = engine.GeneratorPrototype;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generatorstart
        /// </summary>
        public JsValue GeneratorStart(FunctionInstance generatorBody)
        {
            var genContext = _engine.UpdateGenerator(this);
            _generatorBody = generatorBody;
            
            /*
             *Set the code evaluation state of genContext such that when evaluation is resumed for that execution context the following steps will be performed:
If generatorBody is a Parse Node, then
Let result be the result of evaluating generatorBody.
Else,
Assert: generatorBody is an Abstract Closure with no parameters.
Let result be generatorBody().
Assert: If we return here, the generator either threw an exception or performed either an implicit or explicit return.
Remove genContext from the execution context stack and restore the execution context that is at the top of the execution context stack as the running execution context.
Set generator.[[GeneratorState]] to completed.
Once a generator enters the completed state it never leaves it and its associated execution context is never resumed. Any execution state associated with generator can be discarded at this point.
If result.[[Type]] is normal, let resultValue be undefined.
Else if result.[[Type]] is return, let resultValue be result.[[Value]].
Else,
Assert: result.[[Type]] is throw.
Return Completion(result).
Return CreateIterResultObject(resultValue, true).
             */

            _generatorContext = genContext;
            _generatorState = GeneratorState.SuspendedStart;
            
            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generatorresume
        /// </summary>
        public JsValue GeneratorResume(JsValue value, object generatorBrand)
        {
            var state = GeneratorValidate(generatorBrand);
            if (state == GeneratorState.Completed)
            {
                return new IteratorResult(_engine, Undefined, JsBoolean.True);
            }

            var genContext = _generatorContext;
            var methodContext = _engine.ExecutionContext;

            // 6. Suspend methodContext.

            _generatorState = GeneratorState.Executing;
            _engine.EnterExecutionContext(genContext);

            // TODO
            var result = _generatorBody._functionDefinition.Execute();
            
            return result.Value.IsUndefined()
                ? new IteratorResult(_engine, Undefined, JsBoolean.True)
                : result.Value;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generatorresumeabrupt
        /// </summary>
        public JsValue GeneratorResumeAbrupt(in Completion abruptCompletion, object generatorBrand)
        {
            var state = GeneratorValidate(generatorBrand);
            if (state == GeneratorState.SuspendedStart)
            {
                _generatorState = GeneratorState.Completed;
                state = GeneratorState.Completed;
            }

            if (state == GeneratorState.Completed)
            {
                if (abruptCompletion.Type == CompletionType.Return)
                {
                    return new IteratorResult(_engine, abruptCompletion.Value, JsBoolean.True);
                }

                return abruptCompletion.Value;
            }

            var genContext = _generatorContext;
            var methodContext = _engine.ExecutionContext;

            // Suspend methodContext.

            _generatorState = GeneratorState.Executing;

            _engine.EnterExecutionContext(genContext);

            // Resume the suspended evaluation of genContext using abruptCompletion as the result of the operation that suspended it. Let result be the completion record returned by the resumed computation.
            var result = _generatorBody.OrdinaryCallEvaluateBody(Arguments.Empty);

            return result.Value;
        }


        private GeneratorState GeneratorValidate(object generatorBrand)
        {
            if (!ReferenceEquals(generatorBrand, _generatorBrand))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
            
            if (_generatorState == GeneratorState.Executing)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return _generatorState;
        }

    }
}
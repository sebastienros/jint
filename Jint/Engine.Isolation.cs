using System;
using Jint.Collections;
using Jint.Native.Global;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint
{
    public partial class Engine : IEngine
    {
        void IScriptExecutor.SetValue(string name, object value)
        {
            SetValue(name, value);
        }

        void IScriptExecutor.SetValue(string name, int value)
        {
            SetValue(name, value);
        }

        void IScriptExecutor.SetValue(string name, double value)
        {
            SetValue(name, value);
        }

        void IScriptExecutor.SetValue(string name, bool value)
        {
            SetValue(name, value);
        }

        void IScriptExecutor.SetValue(string name, string value)
        {
            SetValue(name, value);
        }

        /// <summary>
        /// Activates isolated context in which introduced variables will be destroyed when returned <see cref="IDisposable"/>
        /// has been disposed.
        /// </summary>
        /// <remarks>This is not fool proof and might leak information, use with care!</remarks>
        /// <returns>A handle to use for disposal.</returns>
        public IDisposable ActivateIsolatedContext()
        {
            var originalGlobalEnvironment = GlobalEnvironment;
            if (originalGlobalEnvironment.GetType() != typeof(GlobalEnvironmentRecord))
            {
                ExceptionHelper.ThrowInvalidOperationException(
                    "Cannot enter isolated context when global environment is not default, did you already enter?");
            }

            if (_executionContexts.Count != 1)
            {
                ExceptionHelper.ThrowInvalidOperationException(
                    "Cannot enter isolated context when execution context stack is not on root level");
            }

            if (!_isStrict)
            {
                ExceptionHelper.ThrowInvalidOperationException(
                    "Cannot enter isolated context when engine is not in strict mode");
            }

            var originalGlobal = Global;
            var propertyLessGlobal = new GlobalObject(this)
            {
                _properties = new PropertyDictionary()
            };

            var newGlobal = new IsolatedEnvironmentRecord(this, propertyLessGlobal, originalGlobalEnvironment);
            GlobalEnvironment = newGlobal;
            Global = propertyLessGlobal;

            var context = new ExecutionContext(newGlobal, newGlobal);
            _executionContexts.Push(context);

            return new ContextRestorer(this, originalGlobalEnvironment, originalGlobal);
        }

        /// <summary>
        /// Allows temporarily disable or change constraint checks for the engine.
        /// Useful for example when initializing engine state with large scripts and/or logic by supplying empty array.
        /// </summary>
        /// <param name="newConstraints">New constraints to have in effect during disposable time frame.</param>
        /// <returns>A <see cref="IDisposable"/> that should be disposed when original constraints should come back into effect.</returns>
        public IDisposable ActivateTemporaryConstraints(IConstraint[] newConstraints)
        {
            if (newConstraints is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(newConstraints));
            }

            var state = new ConstraintRestorer(
                this,
                _constraints,
                newConstraints);

            return state;
        }

        private readonly struct ContextRestorer : IDisposable
        {
            private readonly Engine _engine;
            private readonly GlobalEnvironmentRecord _originalGlobalEnvironment;
            private readonly GlobalObject _originalGlobal;

            public ContextRestorer(Engine engine, GlobalEnvironmentRecord originalGlobalEnvironment,
                GlobalObject originalGlobal)
            {
                _engine = engine;
                _originalGlobalEnvironment = originalGlobalEnvironment;
                _originalGlobal = originalGlobal;
            }

            public void Dispose()
            {
                if (_engine.GlobalEnvironment.GetType() != typeof(IsolatedEnvironmentRecord))
                {
                    ExceptionHelper.ThrowInvalidOperationException(
                        "Cannot leave isolated context when global environment is not isolated, did you already dispose?");
                }

                if (_engine._executionContexts.Count != 2)
                {
                    ExceptionHelper.ThrowInvalidOperationException(
                        "Cannot enter isolated context when execution context stack is not on root level");
                }

                _engine.GlobalEnvironment = _originalGlobalEnvironment;
                _engine.Global = _originalGlobal;
                _engine._executionContexts.Pop();
            }
        }

        public void Dispose()
        {
            // currently no-op
        }

        private readonly struct ConstraintRestorer : IDisposable
        {
            private readonly Engine _engine;
            private readonly IConstraint[] _oldConstraints;

            public ConstraintRestorer(Engine engine, IConstraint[] activeConstraints, IConstraint[] newConstraints)
            {
                _engine = engine;
                _oldConstraints = activeConstraints;
                _engine._constraints = newConstraints;
            }

            public void Dispose()
            {
                _engine._constraints = _oldConstraints;
            }
        }
    }
}
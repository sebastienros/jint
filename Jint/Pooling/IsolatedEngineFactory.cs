using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Pooling
{
    /// <summary>
    /// Produces engines that have activated isolation context which tries to restrict dirtying the context.
    /// Offers a pool of engines that can be initialized to certain state and then used with temporary state that
    /// will be discarded. Every engine in pool should be configured to be in strict mode.
    /// </summary>
    public sealed class IsolatedEngineFactory : EngineFactory<IIsolatedEngine>, IIsolatedEngineFactory
    {
        private readonly ObjectPool<Engine> _enginePool;

        /// <summary>
        /// Creates a new instance of an engine pool.
        /// </summary>
        /// <param name="options">Options to use for each new Engine instance.</param>
        /// <param name="initialize">Actions to run against the engine to create initial state, any execution constraints are not honored during this step!</param>
        /// <param name="size">Size of the pool, defaults to 10.</param>
        public IsolatedEngineFactory(Options options, Action<IEngine> initialize, int size = 10) 
            : base(options, initialize)
        {
            if (!options.IsStrict)
            {
                throw new ArgumentException("options must be set to be in strict mode");
            }
            _enginePool = new ObjectPool<Engine>(CreateAndInitializeEngine, size);
        }

        /// <summary>
        /// Gets a new engine instance. If there are free items in the pool, one is returned from the pool, otherwise a new instance will be allocated.
        /// Isolated execution context will be created to shield pooled engine from permanent changes.
        /// </summary>
        public override IIsolatedEngine Build()
        {
            Engine engine;
            lock (_enginePool)
            {
                engine = _enginePool.Allocate();
            }

            return new EngineWrapper(engine, this, engine.ActivateIsolatedContext());
        }

        private void Return(Engine engine)
        {
            lock (_enginePool)
            {
                _enginePool.Free(engine);
            }
        }

        private sealed class EngineWrapper : IIsolatedEngine
        {
            private readonly Engine _engine;
            private readonly IsolatedEngineFactory _pool;
            private IDisposable _context;

            public EngineWrapper(Engine engine, IsolatedEngineFactory pool, IDisposable context)
            {
                _engine = engine;
                _pool = pool;
                _context = context;
            }

            public void SetValue(string name, object obj)
            {
                AssertNotDisposed();
                _engine.SetValue(name, obj);
            }

            public void SetValue(string name, bool obj)
            {
                AssertNotDisposed();
                _engine.SetValue(name, obj);
            }

            public void SetValue(string name, int obj)
            {
                AssertNotDisposed();
                _engine.SetValue(name, obj);
            }

            public void SetValue(string name, string obj)
            {
                AssertNotDisposed();
                _engine.SetValue(name, obj);
            }

            public void SetValue(string name, double obj)
            {
                AssertNotDisposed();
                _engine.SetValue(name, obj);
            }

            public JsValue Evaluate(string source, ParserOptions parserOptions)
            {
                AssertNotDisposed();
                return _engine.Evaluate(source, parserOptions ?? Engine.DefaultParserOptions);
            }

            public JsValue Evaluate(Script program)
            {
                AssertNotDisposed();
                return _engine.Evaluate(program);
            }

            public IDisposable ActivateTemporaryConstraints(IConstraint[] newConstraints)
            {
                AssertNotDisposed();
                return _engine.ActivateTemporaryConstraints(newConstraints);
            }

            public void Dispose()
            {
                AssertNotDisposed();

                try
                {
                    _context.Dispose();
                    _context = null;
                }
                finally
                {
                    _pool.Return(_engine);
                }

                _context = null;
            }

            private void AssertNotDisposed()
            {
                if (_context is null)
                {
                    ExceptionHelper.ThrowObjectDisposedException("This engine instance has already been disposed");
                }
            }
        }
    }
}
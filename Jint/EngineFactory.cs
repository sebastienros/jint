#nullable enable

using System;

namespace Jint
{
    /// <summary>
    /// Convenience base class for engine factories.
    /// </summary>
    public abstract class EngineFactory<T> : IEngineFactory<T>
    {
        private readonly Options _options;
        private readonly Action<IEngine>? _initialize;

        protected EngineFactory(Options options, Action<IEngine>? initialize)
        {
            _options = options;
            _initialize = initialize;
        }

        public abstract T Build();

        protected Engine CreateAndInitializeEngine()
        {
            var engine = new Engine(_options);
            using (engine.ActivateTemporaryConstraints(Array.Empty<IConstraint>()))
            {
                _initialize?.Invoke(engine);
            }

            return engine;
        }
    }
}
#nullable enable

using System;

namespace Jint
{
    /// <summary>
    /// Default engine factory for creating <see cref="IEngine" /> instances.
    /// </summary>
    public sealed class DefaultEngineFactory : EngineFactory<IEngine>
    {
        /// <summary>
        /// Creates a new instance default engine factory.
        /// </summary>
        /// <param name="options">Options to use for each new Engine instance.</param>
        /// <param name="initialize">Actions to run against the engine to create initial state, any execution constraints are not honored during this step!</param>
        public DefaultEngineFactory(Options options, Action<IEngine>? initialize = null) : base(options, initialize)
        {
        }
        
        public override IEngine Build()
        {
            return CreateAndInitializeEngine();
        }
    }
}
using System;

namespace Jint
{
    /// <summary>
    /// Service interface for creating new engines.
    /// </summary>
    public interface IEngineFactory<out T>
    {
        /// <summary>
        /// Builds a new engine instance. When you are done using it, you should call <see cref="IDisposable.Dispose"/>.
        /// </summary>
        /// <returns></returns>
        T Build();
    }
}
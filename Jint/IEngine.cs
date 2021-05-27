using System;

namespace Jint
{
    /// <summary>
    /// Exposes limited set of engine operations to work with.
    /// </summary>
    public interface IEngine : IScriptExecutor
    {
        /// <summary>
        /// Activates isolated context in which introduced variables will be destroyed when returned <see cref="IDisposable"/>
        /// has been disposed.
        /// </summary>
        /// <remarks>This is not fool proof and might leak information, use with care!</remarks>
        /// <returns>A handle to use for disposal.</returns>
        IDisposable ActivateIsolatedContext();

        /// <summary>
        /// Allows temporarily disable or change constraint checks for the engine.
        /// Useful for example when initializing engine state with large scripts and/or logic by supplying empty array.
        /// </summary>
        /// <param name="newConstraints">New constraints to have in effect during disposable time frame.</param>
        /// <returns>A <see cref="IDisposable"/> that should be disposed when original constraints should come back into effect.</returns>
        IDisposable ActivateTemporaryConstraints(IConstraint[] newConstraints);
    }
}
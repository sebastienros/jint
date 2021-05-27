using System;

namespace Jint.Pooling
{
    public interface IIsolatedEngine : IScriptExecutor
    {
        /// <summary>
        /// Allows temporarily disable or change constraint checks for the engine.
        /// Useful for example when initializing engine state with large scripts and/or logic by supplying empty array.
        /// </summary>
        /// <param name="newConstraints">New constraints to have in effect during disposable time frame.</param>
        /// <returns>A <see cref="IDisposable"/> that should be disposed when original constraints should come back into effect.</returns>
        IDisposable ActivateTemporaryConstraints(IConstraint[] newConstraints);
    }
}
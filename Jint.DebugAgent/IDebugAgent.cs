using System.Collections.Generic;

namespace Jint.DebugAgent
{
    /// <summary>
    /// Methods provided to the domain implementations from the debugger agent
    /// </summary>
    internal interface IDebugAgent
    {
        /// <summary>
        /// Transmit a debugger message through the websocket
        /// </summary>
        void Transmit(string domain, string method, object parameter);
        /// <summary>
        /// Gets the engine by its ID transferred to the debugger
        /// </summary>
        Engine GetEngine(int engineId);
        /// <summary>
        /// Get all engines registered
        /// </summary>
        IEnumerable<Engine> GetEngines();
    }
}
namespace Jint.DebugAgent
{
    /// <summary>
    /// Methods called from the websocket debug protocol handler
    /// </summary>
    internal interface IProtocolServerOwner
    {
        /// <summary>
        /// Notifies that a debugger has connected to the websocket
        /// </summary>
        void NotifyConnected();
        /// <summary>
        /// Notifies that a debugger has disconnected from the websocket
        /// </summary>
        void NotifyDisconnected();
    }
}
namespace Jint
{
    public class Options
    {
        private bool _discardGlobal;
        private bool _strict;
        private bool _allowDebuggerStatement;

        /// <summary>
        /// When called, doesn't initialize the global scope.
        /// Can be useful in lightweight scripts for performance reason.
        /// </summary>
        public Options DiscardGlobal(bool discard = true)
        {
            _discardGlobal = discard;
            return this;
        }

        /// <summary>
        /// Run the script in strict mode.
        /// </summary>
        public Options Strict(bool strict = true)
        {
            _strict = strict;
            return this;
        }

        /// <summary>
        /// Allow the <code>debugger</code> statement to be called in a script.
        /// </summary>
        /// <remarks>
        /// Because the <code>debugger</code> statement can start the 
        /// Visual Studio debugger, is it disabled by default
        /// </remarks>
        public Options AllowDebuggerStatement(bool allowDebuggerStatement = true)
        {
            _allowDebuggerStatement = allowDebuggerStatement;
            return this;
        }

        internal bool GetDiscardGlobal()
        {
            return _discardGlobal;
        }

        internal bool IsStrict()
        {
            return _strict;
        }
        
        internal bool IsDebuggerStatementAllowed()
        {
            return _allowDebuggerStatement;
        }
    }
}

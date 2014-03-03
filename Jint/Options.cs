using System.Globalization;
using Jint.Runtime.Interop;

namespace Jint
{
    public class Options
    {
        private bool _discardGlobal;
        private bool _strict;
        private bool _allowDebuggerStatement;
        private bool _allowClr;
        private ITypeConverter _typeConverter = new DefaultTypeConverter();
        private int _maxStatements;
        private CultureInfo _culture = CultureInfo.CurrentCulture;

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

        /// <summary>
        /// Sets a <see cref="ITypeConverter"/> instance to use when converting CLR types
        /// </summary>
        public Options SetTypeConverter(ITypeConverter typeConverter)
        {
            _typeConverter = typeConverter;
            return this;
        }

        public Options AllowClr(bool allowClr = true)
        {
            _allowClr = allowClr;
            return this;
        }

        public Options MaxStatements(int maxStatements = 0)
        {
            _maxStatements = maxStatements;
            return this;
        }

        public Options Culture(CultureInfo cultureInfo)
        {
            _culture = cultureInfo;
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

        internal bool IsClrAllowed()
        {
            return _allowClr;
        }

        internal ITypeConverter GetTypeConverter()
        {
            return _typeConverter;
        }

        internal int GetMaxStatements()
        {
            return _maxStatements;
        }

        internal CultureInfo GetCulture()
        {
            return _culture;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Jint
{
    public class Options
    {
        private bool _discardGlobal;
        private bool _strict;

        private readonly Dictionary<string, Delegate> _delegates = new Dictionary<string, Delegate>();

        /// <summary>
        /// When called, doesn't initialize the global scope.
        /// Can be useful in lightweight scripts for performance reason.
        /// </summary>
        public Options DiscardGlobal(bool discard = true)
        {
            _discardGlobal = discard;
            return this;
        }

        public Options Strict(bool strict = true)
        {
            _strict = strict;
            return this;
        }

        public Options WithDelegate(string name, Delegate del)
        {
            _delegates[name] = del;

            return this;
        }

        internal bool GetDiscardGlobal()
        {
            return _discardGlobal;
        }

        internal IDictionary<string, Delegate> GetDelegates()
        {
            return _delegates;
        }

        internal bool IsStrict()
        {
            return _strict;
        }
    }
}

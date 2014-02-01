namespace Jint
{
    public class Options
    {
        private bool _discardGlobal;
        private bool _strict;

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

        internal bool GetDiscardGlobal()
        {
            return _discardGlobal;
        }

        internal bool IsStrict()
        {
            return _strict;
        }
    }
}

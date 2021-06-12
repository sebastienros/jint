using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    public sealed class Realm
    {

// helps when debugging which nested realm we are in...
#if DEBUG
        private static int globalId = 1;

        public Realm()
        {
            Id = globalId++;
        }

        internal int Id;
#endif

        /// <summary>
        /// The intrinsic values used by code associated with this realm.
        /// </summary>
        public Intrinsics Intrinsics { get; internal set; }

        /// <summary>
        /// The global object for this realm.
        /// </summary>
        public ObjectInstance GlobalObject { get; internal set; }

        /// <summary>
        /// The global environment for this realm.
        /// </summary>
        public GlobalEnvironmentRecord GlobalEnv { get; internal set; }

        /// <summary>
        /// Field reserved for use by hosts that need to associate additional information with a Realm Record.
        /// </summary>
        public object HostDefined { get; set; }
    }
}
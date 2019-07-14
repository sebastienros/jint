namespace Jint.Runtime
{
    internal static class KnownIdentifiers
    {
        private static readonly Identifier _arguments = "arguments";
        private static readonly Identifier _caller = "caller";
        private static readonly Identifier _callee = "callee";
        private static readonly Identifier _constructor = "constructor";
        private static readonly Identifier _eval = "eval";
        private static readonly Identifier _infinity = "Infinity";
        private static readonly Identifier _length = "length";
        private static readonly Identifier _name = "name";
        private static readonly Identifier _prototype = "prototype";
        private static readonly Identifier _size = "size";

        internal static ref readonly Identifier Arguments => ref _arguments;
        internal static ref readonly Identifier Caller => ref _caller;
        internal static ref readonly Identifier Callee => ref _callee;
        internal static ref readonly Identifier Constructor => ref _constructor;
        internal static ref readonly Identifier Eval => ref _eval;
        internal static ref readonly Identifier Infinity => ref _infinity;
        internal static ref readonly Identifier Length => ref _length;
        internal static ref readonly Identifier Name => ref _name;
        internal static ref readonly Identifier Prototype => ref _prototype;
        internal static ref readonly Identifier Size => ref _size;
    }
}
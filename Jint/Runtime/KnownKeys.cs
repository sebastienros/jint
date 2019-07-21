namespace Jint.Runtime
{
    internal static class KnownKeys
    {
        private static readonly Key _arguments = "arguments";
        private static readonly Key _caller = "caller";
        private static readonly Key _callee = "callee";
        private static readonly Key _constructor = "constructor";
        private static readonly Key _eval = "eval";
        private static readonly Key _infinity = "Infinity";
        private static readonly Key _length = "length";
        private static readonly Key _name = "name";
        private static readonly Key _prototype = "prototype";
        private static readonly Key _size = "size";

        internal static ref readonly Key Arguments => ref _arguments;
        internal static ref readonly Key Caller => ref _caller;
        internal static ref readonly Key Callee => ref _callee;
        internal static ref readonly Key Constructor => ref _constructor;
        internal static ref readonly Key Eval => ref _eval;
        internal static ref readonly Key Infinity => ref _infinity;
        internal static ref readonly Key Length => ref _length;
        internal static ref readonly Key Name => ref _name;
        internal static ref readonly Key Prototype => ref _prototype;
        internal static ref readonly Key Size => ref _size;
    }
}
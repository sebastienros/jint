namespace Jint.Runtime
{
    internal static class KnownKeys
    {
        private static readonly Key _arguments = "arguments";

        internal static ref readonly Key Arguments => ref _arguments;
    }
}
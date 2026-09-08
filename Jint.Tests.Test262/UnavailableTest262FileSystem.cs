#nullable enable

using Zio;
using Zio.FileSystems;

namespace Jint.Tests.Test262;

internal sealed class UnavailableTest262FileSystem : MemoryFileSystem
{
    private readonly Exception _failure;
    private int _reported;

    internal UnavailableTest262FileSystem(Exception failure)
    {
        _failure = failure;
        CreateDirectory("/harness");
        CreateDirectory("/test");
    }

    protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share)
    {
        if (path.FullName.StartsWith("/test/", StringComparison.Ordinal))
        {
            var message = $"The pinned Test262 corpus could not be acquired. {_failure}";
            if (Interlocked.CompareExchange(ref _reported, 1, 0) == 0)
            {
                throw new AssertionException(message);
            }

            throw new IgnoreException(
                "The pinned Test262 corpus could not be acquired; the first selected generated case reports the failure.");
        }

        return base.OpenFileImpl(path, mode, access, share);
    }
}

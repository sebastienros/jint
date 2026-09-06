using AngleSharp.Io.Dom;
using Jint.WebApi.Files;

namespace Jint.Browser.Dom.Files;

/// <summary>
/// Mirrors a selected File into AngleSharp's private input model so its value and constraint-validation
/// algorithms see the same selection as the JavaScript FileList.
/// </summary>
internal sealed class AngleSharpFileAdapter : IFile
{
    private const long MinimumUnixMilliseconds = -62_135_596_800_000;
    private const long MaximumUnixMilliseconds = 253_402_300_799_999;

    private readonly JsFile _file;
    private bool _closed;

    internal AngleSharpFileAdapter(JsFile file)
    {
        _file = file;
    }

    public Stream Body => _closed ? Stream.Null : new MemoryStream(_file.Data.ToArray(), writable: false);

    public bool IsClosed => _closed;

    public int Length => (int) Math.Min(_file.Data.Length, int.MaxValue);

    public string Type => _file.MediaType;

    public DateTime LastModified
        => DateTimeOffset.FromUnixTimeMilliseconds(
            Math.Clamp(_file.LastModified, MinimumUnixMilliseconds, MaximumUnixMilliseconds)).UtcDateTime;

    public string Name => _file.Name;

    public void Close() => _closed = true;

    public IBlob Slice(int start = 0, int end = int.MaxValue, string? contentType = null)
    {
        var length = _file.Data.Length;
        var first = start < 0 ? Math.Max(length + start, 0) : Math.Min(start, length);
        var last = end < 0 ? Math.Max(length + end, 0) : Math.Min(end, length);
        var count = Math.Max(last - first, 0);
        return new AngleSharpBlobAdapter(_file.Data.Slice(first, count), contentType ?? string.Empty);
    }

    public void Dispose() => Close();

    private sealed class AngleSharpBlobAdapter : IBlob
    {
        private readonly ReadOnlyMemory<byte> _data;
        private bool _closed;

        internal AngleSharpBlobAdapter(ReadOnlyMemory<byte> data, string type)
        {
            _data = data;
            Type = type;
        }

        public Stream Body => _closed ? Stream.Null : new MemoryStream(_data.ToArray(), writable: false);

        public bool IsClosed => _closed;

        public int Length => (int) Math.Min(_data.Length, int.MaxValue);

        public string Type { get; }

        public void Close() => _closed = true;

        public IBlob Slice(int start = 0, int end = int.MaxValue, string? contentType = null)
        {
            var first = start < 0 ? Math.Max(_data.Length + start, 0) : Math.Min(start, _data.Length);
            var last = end < 0 ? Math.Max(_data.Length + end, 0) : Math.Min(end, _data.Length);
            var count = Math.Max(last - first, 0);
            return new AngleSharpBlobAdapter(_data.Slice(first, count), contentType ?? string.Empty);
        }

        public void Dispose() => Close();
    }
}

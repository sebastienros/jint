#if NET8_0_OR_GREATER
namespace Jint.WebApi.Files;

/// <summary>
/// A <c>File</c> instance: a <see cref="JsBlob"/> that also knows a name and a modification time.
/// <para>
/// https://w3c.github.io/FileAPI/#file-section
/// </para>
/// </summary>
/// <remarks>
/// <c>File</c> inherits <c>Blob</c> in the IDL, so it inherits it here too: <c>size</c>, <c>type</c>,
/// <c>slice</c> and the read methods all resolve on <c>Blob.prototype</c> and their brand check passes on a
/// file. Only <c>name</c> and <c>lastModified</c> are its own.
/// </remarks>
internal sealed class JsFile : JsBlob
{
    internal JsFile(Engine engine, ReadOnlyMemory<byte> data, string type, string name, long lastModified)
        : base(engine, data, type)
    {
        Name = name;
        LastModified = lastModified;
    }

    /// <summary>The file's name, https://w3c.github.io/FileAPI/#dfn-name.</summary>
    internal string Name { get; }

    /// <summary>
    /// Milliseconds since the Unix epoch, https://w3c.github.io/FileAPI/#dfn-lastModified.
    /// </summary>
    /// <remarks>
    /// When the constructor was not given one it is <b>captured at construction</b> from the engine's
    /// <c>ITimeSystem</c> — the same clock <c>Date.now()</c> reads, so a host that installed a fixed clock
    /// gets a deterministic value here too. It never moves afterwards.
    /// </remarks>
    internal long LastModified { get; }
}
#endif

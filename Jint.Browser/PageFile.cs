namespace Jint.Browser;

/// <summary>
/// A file selected into an <c>&lt;input type=file&gt;</c> from memory rather than from a path.
/// </summary>
/// <remarks>
/// <para>
/// It is what the page sees as a <c>File</c>: <see cref="Name"/> is <c>file.name</c> and the file name a
/// submission writes, <see cref="MimeType"/> is <c>file.type</c> and the part's <c>Content-Type</c>, and
/// <see cref="Content"/> is the body. Nothing about it is a handle — the bytes are the file, so a caller
/// that builds one from a stream has already read it, and the page can read it back long afterwards.
/// </para>
/// <para>
/// <see cref="Page.SetInputFilesAsync(string, IReadOnlyList{string})"/> takes host paths instead and reads
/// them, which is the same thing with the reading done for you. Use this overload for content that has no
/// path: a generated CSV, a fixture in a resource, bytes that came off a network.
/// </para>
/// </remarks>
public sealed class PageFile
{
    /// <summary>Creates a file from bytes already in memory.</summary>
    /// <param name="name">The file's name, which the page reads as <c>file.name</c>.</param>
    /// <param name="mimeType">
    /// The type, which the page reads as <c>file.type</c>. The empty string is what the File API gives a
    /// file whose type nothing could determine, and is passed through unchanged.
    /// </param>
    /// <param name="content">The bytes. They are not copied; do not write to the buffer afterwards.</param>
    /// <param name="lastModified">
    /// What the page reads as <c>file.lastModified</c>, or <see langword="null"/> for the host clock's
    /// current time, which is what the <c>File</c> constructor's own default is.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="mimeType"/> is null.</exception>
    public PageFile(string name, string mimeType, ReadOnlyMemory<byte> content, DateTimeOffset? lastModified = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(mimeType);

        Name = name;
        MimeType = mimeType;
        Content = content;
        LastModified = lastModified ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the file's name.</summary>
    public string Name { get; }

    /// <summary>Gets the file's MIME type.</summary>
    public string MimeType { get; }

    /// <summary>Gets the file's bytes.</summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>Gets what the page reads as <c>file.lastModified</c>.</summary>
    public DateTimeOffset LastModified { get; }
}

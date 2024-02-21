using System.Text;

namespace Jint.Tests.Runtime.Domain;

/// <summary>
/// https://encoding.spec.whatwg.org/#textdecoder
/// </summary>
/// <remarks>Public API, do not make internal!</remarks>
public sealed class TextDecoder
{
    public string Decode() => string.Empty;


    public string Decode(byte[] buff) => Encoding.UTF8.GetString(buff);
}

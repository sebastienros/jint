#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.WebApi.Files;

/// <summary>
/// A <c>FormData</c> instance: an ordered list of (name, value) entries, where a value is either a string
/// or a <c>File</c>.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-formdata
/// </para>
/// </summary>
/// <remarks>
/// The list is ordered and admits duplicate names, so it is a <see cref="List{T}"/> and not a dictionary:
/// <c>getAll</c>, the iteration order and <c>set</c>'s "replace the first, remove the rest" all depend on
/// position. Nothing here serializes: turning an entry list into a <c>multipart/form-data</c> body is the
/// business of a request body, and lives with <c>fetch</c> in <c>Jint.WebApi.Fetch.MultipartFormData</c>.
/// </remarks>
internal sealed class JsFormData : ObjectInstance
{
    internal JsFormData(Engine engine) : base(engine, ObjectClass.Object)
    {
    }

    /// <summary>
    /// The entry list, https://xhr.spec.whatwg.org/#concept-formdata-entry-list. Mutated in place by
    /// <c>append</c>, <c>delete</c> and <c>set</c>, and read live by the iterators.
    /// </summary>
    internal List<FormDataEntry> Entries { get; } = [];

    /// <summary>
    /// The index of the first entry named <paramref name="name"/>, or <c>-1</c>.
    /// </summary>
    internal int IndexOf(string name)
    {
        var entries = Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// One entry of a <c>FormData</c>'s entry list.
/// <para>
/// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#entry
/// </para>
/// </summary>
/// <param name="Name">The entry's name, already a scalar value string.</param>
/// <param name="Value">The entry's value: a <see cref="JsString"/> or a <see cref="JsFile"/>, never a bare blob.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FormDataEntry(string Name, JsValue Value);
#endif

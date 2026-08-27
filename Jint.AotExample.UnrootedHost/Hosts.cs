namespace UnrootedAotProbe;

/// <summary>
/// Host types for the probes that measure what a <c>[DynamicallyAccessedMembers]</c> annotation
/// <em>preserves</em>, as opposed to what the engine does at run time.
/// </summary>
/// <remarks>
/// <para>
/// They live in their own assembly for one reason: <c>Jint.AotExample</c> roots itself and <c>Jint</c> with
/// <c>TrimmerRootAssembly</c>, so every host type declared beside <c>Program.cs</c> survives whatever an
/// annotation says and no probe over one can tell the annotation from the root. This assembly is
/// deliberately <b>not</b> rooted, so the only thing standing between these members and the trimmer is the
/// attribute on the entry point each is registered through.
/// </para>
/// <para>
/// Nothing in C# may read a member below. A single call from <c>Program.cs</c> — or an <c>InternalsVisibleTo</c>
/// test, or a debugger-friendly <c>ToString</c> override that touched a property — roots it, and the probe
/// that member is the subject of silently stops measuring anything. That is the same failure the rooting of
/// <c>Jint.AotExample</c> itself creates, one assembly further out.
/// </para>
/// </remarks>
public sealed class PreservedByAnnotation
{
    /// <summary>A public field. Preserved by <c>PublicFields</c>.</summary>
    public string Field = "preserved field";

    /// <summary>A public property. Preserved by <c>PublicProperties</c>.</summary>
    public string Name => "preserved";

    /// <summary>A public method. Preserved by <c>PublicMethods</c>.</summary>
    public string Greet(string who) => "hello " + who;
}

/// <summary>
/// The same shape as <see cref="PreservedByAnnotation"/>, registered through an entry point that carries
/// <c>[RequiresUnreferencedCode]</c> instead — so nothing preserves these members and the trimmer takes them.
/// </summary>
/// <remarks>
/// A distinct type rather than the one above, because a <c>[DynamicallyAccessedMembers]</c> annotation is a
/// whole-program fact about the type it names: registering one instance through <c>SetValue&lt;T&gt;</c>
/// anywhere would preserve the members for every other lane as well, and the negative probe would pass by
/// borrowing the positive one's preservation.
/// </remarks>
public sealed class TrimmedWithoutAnnotation
{
    /// <summary>The member the negative probe reads. Under Native AOT it is not there.</summary>
    public string Name => "not preserved";
}

/// <summary>
/// A host sequence whose array-likeness Jint can only discover through <c>Type.GetInterfaces()</c>, and which
/// nothing in C# ever uses as an <see cref="IReadOnlyList{T}"/>.
/// </summary>
/// <remarks>
/// This is the subject of the <c>Interfaces</c> entry that
/// <see href="https://github.com/sebastienros/jint/issues/3396">#3396</see> added to the shared annotation.
/// The indexer is an ordinary public property and survives on <c>PublicProperties</c> alone, so
/// <c>seq[1]</c> is not the measurement; <c>seq.length</c> is, because that arrives only when
/// <c>ObjectWrapper.ResolveArrayLikeWrapperFactoryType</c> finds <see cref="IReadOnlyList{T}"/> among the
/// interfaces the trimmer left behind.
/// </remarks>
public sealed class PreservedInterfaceSequence : IReadOnlyList<string>
{
    private readonly string[] _items = ["x", "y"];

    /// <inheritdoc />
    public string this[int index] => _items[index];

    /// <inheritdoc />
    public int Count => _items.Length;

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>) _items).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

/// <summary>An interface nothing in this program implements, dispatches on, or names but the class below.</summary>
public interface IHiddenBehindAnInterface
{
    /// <summary>The member the explicit-implementation probe reads.</summary>
    string Hidden { get; }
}

/// <summary>
/// A host type whose only script-visible member is an <b>explicit</b> interface implementation, over an
/// interface used nowhere else in the closed program.
/// </summary>
/// <remarks>
/// It is the shape that isolates the <c>Interfaces</c> entry of the shared annotation rather than riding on
/// it. <c>IReadOnlyList&lt;string&gt;</c> does not isolate anything: Jint implements against it and rooted
/// types beside <c>Program.cs</c> use it, so ILC keeps that interface implementation whether the annotation
/// asks for it or not. Here the interface exists for this one class — and the measured answer is that the
/// member is <b>not</b> reachable under Native AOT even so, because <c>Interfaces</c> asks for the
/// implemented interfaces and not for their members, and <c>TypeResolver</c>'s walk needs
/// <c>iface.GetProperties()</c> to answer.
/// </remarks>
public sealed class ExplicitInterfaceOnly : IHiddenBehindAnInterface
{
    string IHiddenBehindAnInterface.Hidden => "behind an interface";
}

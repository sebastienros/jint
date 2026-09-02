namespace Jint.Browser.Dom;

/// <summary>
/// What every wrapper in this package has in common: the AngleSharp object behind it, and the per-engine
/// binding state that produced it. It exists because the wrappers deliberately do <em>not</em> share a base
/// class — a node is a <c>JsEventTarget</c> so that Jint's tree dispatch can walk it, a collection is an
/// <c>ArrayLikeObject</c> so that <c>for..of</c> and <c>list[i]</c> reach the fast lanes, and neither of
/// those is derivable from the other.
/// </summary>
internal interface IDomWrapper
{
    /// <summary>The AngleSharp object this wrapper projects. Never <see langword="null"/>.</summary>
    object DomTarget { get; }

    /// <summary>The binding state of the engine this wrapper belongs to.</summary>
    DomRealm DomRealm { get; }
}

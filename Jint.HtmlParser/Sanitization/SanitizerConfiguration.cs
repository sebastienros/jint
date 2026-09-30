namespace Jint.HtmlParser.Sanitization;

/// <summary>
/// A <c>SanitizerConfig</c> dictionary and the algorithms HTML §8.6 defines over it: canonicalization, the
/// validity check, the <c>Sanitizer</c> modifier methods, <c>removeUnsafe</c> and <c>get()</c>'s ordering.
/// </summary>
/// <remarks>
/// <para>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-configuration. Every list
/// is <see langword="null"/> when the member does not exist, which is a different state from an empty list
/// throughout the specification — <c>elements</c> absent means "no allow-list", <c>elements</c> empty means
/// "allow nothing".
/// </para>
/// <para>
/// Items arrive already canonical: the host converts the WebIDL unions (a string or a dictionary) to a
/// <see cref="SanitizerName"/> with the right default namespace before anything here sees them. What this
/// type canonicalizes is the dictionary's shape — the defaults each missing member takes.
/// </para>
/// </remarks>
internal sealed class SanitizerConfiguration
{
    internal List<SanitizerElementRule>? Elements { get; set; }
    internal List<SanitizerName>? RemoveElements { get; set; }
    internal List<SanitizerName>? ReplaceWithChildrenElements { get; set; }
    internal List<string>? ProcessingInstructions { get; set; }
    internal List<string>? RemoveProcessingInstructions { get; set; }
    internal List<SanitizerName>? Attributes { get; set; }
    internal List<SanitizerName>? RemoveAttributes { get; set; }
    internal bool? Comments { get; set; }
    internal bool? DataAttributes { get; set; }
    internal bool? JavascriptUrls { get; set; }

    internal SanitizerConfiguration Clone() => new()
    {
        Elements = Elements?.ConvertAll(static e => e.Clone()),
        RemoveElements = Copy(RemoveElements),
        ReplaceWithChildrenElements = Copy(ReplaceWithChildrenElements),
        ProcessingInstructions = Copy(ProcessingInstructions),
        RemoveProcessingInstructions = Copy(RemoveProcessingInstructions),
        Attributes = Copy(Attributes),
        RemoveAttributes = Copy(RemoveAttributes),
        Comments = Comments,
        DataAttributes = DataAttributes,
        JavascriptUrls = JavascriptUrls,
    };

    private static List<T>? Copy<T>(List<T>? list) => list is null ? null : [.. list];

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-canonicalize-the-configuration,
    /// for the members' presence and defaults; the items themselves are canonical on arrival.
    /// </summary>
    internal void Canonicalize(bool permissiveDefaults)
    {
        if (Elements is null && RemoveElements is null) RemoveElements = [];
        if (Attributes is null && RemoveAttributes is null) RemoveAttributes = [];
        if (ProcessingInstructions is null && RemoveProcessingInstructions is null)
        {
            if (permissiveDefaults) RemoveProcessingInstructions = [];
            else ProcessingInstructions = [];
        }

        if (Elements is not null)
        {
            foreach (var element in Elements)
            {
                CanonicalizeRule(element);
            }
        }

        Comments ??= permissiveDefaults;
        if (Attributes is not null) DataAttributes ??= permissiveDefaults;
        JavascriptUrls ??= permissiveDefaults;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#canonicalize-a-sanitizerelementwithattributes
    /// step 3: an element with neither local list gets an empty remove-list.
    /// </summary>
    internal static void CanonicalizeRule(SanitizerElementRule rule)
    {
        if (rule.Attributes is null && rule.RemoveAttributes is null) rule.RemoveAttributes = [];
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizerconfig-valid, over a
    /// canonical configuration.
    /// </summary>
    internal bool IsValid()
    {
        if (Elements is not null && RemoveElements is not null) return false;
        if (ProcessingInstructions is not null && RemoveProcessingInstructions is not null) return false;
        if (Attributes is not null && RemoveAttributes is not null) return false;

        if (Elements is not null)
        {
            if (HasDuplicateNames(Elements)) return false;
        }
        else if (HasDuplicates(RemoveElements)) return false;

        if (HasDuplicates(ReplaceWithChildrenElements)) return false;

        if (ProcessingInstructions is not null)
        {
            if (HasDuplicates(ProcessingInstructions)) return false;
        }
        else if (HasDuplicates(RemoveProcessingInstructions)) return false;

        if (Attributes is not null)
        {
            if (HasDuplicates(Attributes)) return false;
        }
        else if (HasDuplicates(RemoveAttributes)) return false;

        if (ReplaceWithChildrenElements is not null)
        {
            foreach (var element in ReplaceWithChildrenElements)
            {
                if (SanitizerBuiltins.IsNonReplaceable(element)) return false;
                if (Elements is not null ? IndexOf(Elements, element) >= 0 : RemoveElements!.Contains(element)) return false;
            }
        }

        if (Attributes is not null)
        {
            if (Elements is not null)
            {
                foreach (var element in Elements)
                {
                    if (HasDuplicates(element.Attributes) || HasDuplicates(element.RemoveAttributes)) return false;
                    if (Intersects(Attributes, element.Attributes)) return false;
                    if (element.RemoveAttributes is not null)
                    {
                        foreach (var attribute in element.RemoveAttributes)
                        {
                            if (!Attributes.Contains(attribute)) return false;
                        }
                    }

                    if (DataAttributes == true && ContainsCustomData(element.Attributes)) return false;
                }
            }

            if (DataAttributes == true && ContainsCustomData(Attributes)) return false;
        }
        else
        {
            if (Elements is not null)
            {
                foreach (var element in Elements)
                {
                    if (element.Attributes is not null && element.RemoveAttributes is not null) return false;
                    if (HasDuplicates(element.Attributes) || HasDuplicates(element.RemoveAttributes)) return false;
                    if (Intersects(RemoveAttributes!, element.Attributes)) return false;
                    if (Intersects(RemoveAttributes!, element.RemoveAttributes)) return false;
                }
            }

            if (DataAttributes is not null) return false;
        }

        return true;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-get: a copy whose
    /// lists are sorted, so that the order the implementation keeps internally is not observable.
    /// </summary>
    internal SanitizerConfiguration ToSorted()
    {
        var config = Clone();
        if (config.Elements is not null)
        {
            foreach (var element in config.Elements)
            {
                element.Attributes?.Sort(SanitizerName.Compare);
                element.RemoveAttributes?.Sort(SanitizerName.Compare);
            }

            config.Elements.Sort(static (a, b) => SanitizerName.Compare(a.Name, b.Name));
        }
        else
        {
            config.RemoveElements?.Sort(SanitizerName.Compare);
        }

        config.ReplaceWithChildrenElements?.Sort(SanitizerName.Compare);
        if (config.ProcessingInstructions is not null) config.ProcessingInstructions.Sort(string.CompareOrdinal);
        else config.RemoveProcessingInstructions?.Sort(string.CompareOrdinal);
        if (config.Attributes is not null) config.Attributes.Sort(SanitizerName.Compare);
        else config.RemoveAttributes?.Sort(SanitizerName.Compare);
        return config;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-allowelement</summary>
    internal bool AllowElement(SanitizerElementRule element)
    {
        CanonicalizeRule(element);
        if (Elements is not null)
        {
            var modified = Remove(ReplaceWithChildrenElements, element.Name);
            if (Attributes is not null)
            {
                if (element.Attributes is not null)
                {
                    element.Attributes = Difference(Distinct(element.Attributes), Attributes);
                    if (DataAttributes == true) element.Attributes.RemoveAll(static a => a.IsCustomDataAttribute);
                }

                if (element.RemoveAttributes is not null)
                {
                    element.RemoveAttributes = Intersection(Distinct(element.RemoveAttributes), Attributes);
                }
            }
            else
            {
                if (element.Attributes is not null)
                {
                    var attributes = Difference(Distinct(element.Attributes), element.RemoveAttributes ?? []);
                    element.RemoveAttributes = null;
                    element.Attributes = Difference(attributes, RemoveAttributes!);
                }

                if (element.RemoveAttributes is not null)
                {
                    element.RemoveAttributes = Difference(Distinct(element.RemoveAttributes), RemoveAttributes!);
                }
            }

            var index = IndexOf(Elements, element.Name);
            if (index < 0)
            {
                Elements.Add(element);
                return true;
            }

            if (element.IsEquivalentTo(Elements[index])) return modified;
            Elements.RemoveAt(index);
            Elements.Add(element);
            return true;
        }

        if (element.Attributes is not null || element.RemoveAttributes is { Count: > 0 }) return false;
        var changed = Remove(ReplaceWithChildrenElements, element.Name);
        if (!RemoveElements!.Contains(element.Name)) return changed;
        RemoveElements.Remove(element.Name);
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-remove-an-element</summary>
    internal bool RemoveElement(SanitizerName element)
    {
        var modified = Remove(ReplaceWithChildrenElements, element);
        if (Elements is not null)
        {
            var index = IndexOf(Elements, element);
            if (index < 0) return modified;
            Elements.RemoveAt(index);
            return true;
        }

        if (RemoveElements!.Contains(element)) return modified;
        RemoveElements.Add(element);
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-replaceelementwithchildren</summary>
    internal bool ReplaceElementWithChildren(SanitizerName element)
    {
        if (SanitizerBuiltins.IsNonReplaceable(element)) return false;
        var modified = false;
        if (Elements is not null)
        {
            var index = IndexOf(Elements, element);
            if (index >= 0)
            {
                Elements.RemoveAt(index);
                modified = true;
            }
        }

        if (Remove(RemoveElements, element)) modified = true;
        ReplaceWithChildrenElements ??= [];
        if (!ReplaceWithChildrenElements.Contains(element))
        {
            ReplaceWithChildrenElements.Add(element);
            return true;
        }

        return modified;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-allowattribute</summary>
    internal bool AllowAttribute(SanitizerName attribute)
    {
        if (Attributes is not null)
        {
            if (DataAttributes == true && attribute.IsCustomDataAttribute) return false;
            if (Attributes.Contains(attribute)) return false;
            if (Elements is not null)
            {
                foreach (var element in Elements)
                {
                    element.Attributes?.Remove(attribute);
                }
            }

            Attributes.Add(attribute);
            return true;
        }

        return RemoveAttributes!.Remove(attribute);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-remove-an-attribute</summary>
    internal bool RemoveAttribute(SanitizerName attribute)
    {
        if (Attributes is not null)
        {
            var modified = Attributes.Remove(attribute);
            if (Elements is not null)
            {
                foreach (var element in Elements)
                {
                    if (element.Attributes is not null && element.Attributes.Remove(attribute)) modified = true;
                    element.RemoveAttributes?.Remove(attribute);
                }
            }

            return modified;
        }

        if (RemoveAttributes!.Contains(attribute)) return false;
        if (Elements is not null)
        {
            foreach (var element in Elements)
            {
                element.Attributes?.Remove(attribute);
                element.RemoveAttributes?.Remove(attribute);
            }
        }

        RemoveAttributes.Add(attribute);
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-setcomments</summary>
    internal bool SetComments(bool allow)
    {
        if (Comments == allow) return false;
        Comments = allow;
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-setdataattributes</summary>
    internal bool SetDataAttributes(bool allow)
    {
        if (Attributes is null) return false;
        if (DataAttributes == allow) return false;
        if (allow)
        {
            if (Elements is not null)
            {
                foreach (var element in Elements)
                {
                    element.Attributes?.RemoveAll(static a => a.IsCustomDataAttribute);
                }
            }

            Attributes.RemoveAll(static a => a.IsCustomDataAttribute);
        }

        DataAttributes = allow;
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-setjavascripturls</summary>
    internal bool SetJavascriptUrls(bool allow)
    {
        if (JavascriptUrls == allow) return false;
        JavascriptUrls = allow;
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-allowprocessinginstruction</summary>
    internal bool AllowProcessingInstruction(string target)
    {
        if (ProcessingInstructions is not null)
        {
            if (ProcessingInstructions.Contains(target)) return false;
            ProcessingInstructions.Add(target);
            return true;
        }

        return RemoveProcessingInstructions!.Remove(target);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-removeprocessinginstruction</summary>
    internal bool RemoveProcessingInstruction(string target)
    {
        if (ProcessingInstructions is not null) return ProcessingInstructions.Remove(target);
        if (RemoveProcessingInstructions!.Contains(target)) return false;
        RemoveProcessingInstructions.Add(target);
        return true;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-remove-unsafe</summary>
    internal bool RemoveUnsafe()
    {
        var result = false;
        foreach (var element in SanitizerBuiltins.SafeBaselineRemoveElements)
        {
            if (RemoveElement(element)) result = true;
        }

        // The baseline's removeAttributes list is empty; the event handler content attributes are what it
        // behaves as if it held.
        foreach (var name in SanitizerBuiltins.EventHandlerContentAttributes)
        {
            if (RemoveAttribute(SanitizerName.Attribute(name))) result = true;
        }

        if (JavascriptUrls == true)
        {
            JavascriptUrls = false;
            result = true;
        }

        return result;
    }

    /// <summary>
    /// Whether sanitizing with this configuration can change nothing — the canonical form of the empty
    /// <c>{}</c> the <c>Unsafe</c> methods default to — so the walk can be skipped.
    /// </summary>
    internal bool AllowsEverything
        => Elements is null && RemoveElements is { Count: 0 } && ReplaceWithChildrenElements is null or { Count: 0 }
            && ProcessingInstructions is null && RemoveProcessingInstructions is { Count: 0 }
            && Attributes is null && RemoveAttributes is { Count: 0 }
            && Comments == true && JavascriptUrls == true;

    internal static int IndexOf(List<SanitizerElementRule> elements, SanitizerName name)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].Name == name) return i;
        }

        return -1;
    }

    private static bool Remove(List<SanitizerName>? list, SanitizerName item) => list is not null && list.Remove(item);

    private static List<SanitizerName> Distinct(List<SanitizerName> list)
    {
        var result = new List<SanitizerName>(list.Count);
        foreach (var item in list)
        {
            if (!result.Contains(item)) result.Add(item);
        }

        return result;
    }

    private static List<SanitizerName> Difference(List<SanitizerName> a, List<SanitizerName> b)
        => a.FindAll(item => !b.Contains(item));

    private static List<SanitizerName> Intersection(List<SanitizerName> a, List<SanitizerName> b)
        => a.FindAll(item => b.Contains(item));

    private static bool Intersects(List<SanitizerName> a, List<SanitizerName>? b)
    {
        if (b is null) return false;
        foreach (var item in b)
        {
            if (a.Contains(item)) return true;
        }

        return false;
    }

    private static bool ContainsCustomData(List<SanitizerName>? list)
    {
        if (list is null) return false;
        foreach (var item in list)
        {
            if (item.IsCustomDataAttribute) return true;
        }

        return false;
    }

    private static bool HasDuplicates<T>(List<T>? list)
    {
        if (list is null || list.Count < 2) return false;
        var seen = new HashSet<T>();
        foreach (var item in list)
        {
            if (!seen.Add(item)) return true;
        }

        return false;
    }

    private static bool HasDuplicateNames(List<SanitizerElementRule> list)
    {
        if (list.Count < 2) return false;
        var seen = new HashSet<SanitizerName>();
        foreach (var item in list)
        {
            if (!seen.Add(item.Name)) return true;
        }

        return false;
    }
}

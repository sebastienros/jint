#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime;

namespace Jint.WebApi.Performance;

/// <summary>
/// https://w3c.github.io/performance-timeline/#dfn-filter-buffer-by-name-and-type — the one filter both
/// <c>performance</c> and a <c>PerformanceObserverEntryList</c> answer their three getters from.
/// <para>
/// https://w3c.github.io/performance-timeline/#filter-buffer-by-name-and-type
/// </para>
/// </summary>
/// <remarks>
/// It lives beside the two callers rather than on either of them because the specification has exactly one
/// algorithm here: <c>performance.getEntries()</c> runs <i>filter buffer map by name and type</i>, which for
/// a runtime whose whole map is one buffer reduces to this, and
/// <c>PerformanceObserverEntryList.getEntries()</c> runs this over the list the observer was delivered. Two
/// copies would eventually disagree about the sort, which is the half a script can see.
/// </remarks>
internal static class PerformanceEntryFilter
{
    /// <summary>
    /// The matching entries, in chronological order of <c>startTime</c>. A <see langword="null"/> name or
    /// type matches everything, which is how the three getters differ from one another.
    /// </summary>
    internal static JsArray Filter(Realm realm, List<JsPerformanceEntry>? entries, string? name, string? type)
    {
        var matched = new List<JsPerformanceEntry>();

        if (entries is not null)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (name is not null && !string.Equals(entry.EntryName.ToString(), name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (type is not null && !string.Equals(entry.EntryType.ToString(), type, StringComparison.Ordinal))
                {
                    continue;
                }

                matched.Add(entry);
            }

            SortChronologically(matched);
        }

        var values = new List<JsValue>(matched.Count);
        for (var i = 0; i < matched.Count; i++)
        {
            values.Add(matched[i]);
        }

        return realm.Intrinsics.Array.ConstructFast(values);
    }

    /// <summary>
    /// Orders entries by <c>startTime</c>, keeping buffer order among equal ones.
    /// </summary>
    /// <remarks>
    /// Insertion order is already chronological unless a mark was given an explicit <c>startTime</c> — the one
    /// way an entry can be added out of order — so the common case is the scan that finds nothing to do. The
    /// sort itself is stabilized by the buffer index, because <see cref="Array.Sort{T}(T[], Comparison{T})"/>
    /// is introsort and is not stable, and two marks taken in the same clock tick must not swap.
    /// </remarks>
    internal static void SortChronologically(List<JsPerformanceEntry> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (entries[i].StartTime < entries[i - 1].StartTime)
            {
                StableSort(entries);
                return;
            }
        }
    }

    private static void StableSort(List<JsPerformanceEntry> entries)
    {
        var items = new (double StartTime, int Index, JsPerformanceEntry Entry)[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            items[i] = (entries[i].StartTime, i, entries[i]);
        }

        Array.Sort(items, static (a, b) =>
        {
            var comparison = a.StartTime.CompareTo(b.StartTime);
            return comparison != 0 ? comparison : a.Index.CompareTo(b.Index);
        });

        for (var i = 0; i < items.Length; i++)
        {
            entries[i] = items[i].Entry;
        }
    }
}
#endif

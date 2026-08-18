using System.Numerics;
using System.Runtime.InteropServices;

namespace Jint.Benchmark;

/// <summary>
/// Derives a processor-affinity mask that keeps a benchmark process inside a single last-level-cache
/// domain.
///
/// <para>It exists because an unpinned process on a chiplet CPU measures the scheduler as much as it
/// measures the code. On the Ryzen 9 5950X this suite is gated on, the 16 cores are split across two
/// CCDs with a <em>separate</em> 32 MB L3 each (logical CPUs 0-15 and 16-31), so a thread migrating
/// across the boundary throws away its whole last-level working set — and per-core boost binning made
/// the delivered clock range from 3,570 to 4,352 MHz depending on which core a run happened to land
/// on. Neither effect is visible in BenchmarkDotNet's error bar, because both are constant within a
/// process and vary between processes.</para>
///
/// <para>The mask is <em>derived</em>, never hard-coded: the same source has to run on CI and on
/// contributors' machines, and a mask that is right here is worse than no mask at all somewhere else.
/// Detection falls back to <c>null</c> — meaning "let the scheduler decide", exactly as before — on
/// non-Windows, on machines with more than one processor group, on machines too small to pin safely,
/// and on any API failure.</para>
///
/// <para>Two choices inside the chosen domain are deliberate. The domain containing CPU 0 is preferred
/// because on Zen the first CCD carries the CPPC preferred cores, and physical core 0 is then removed
/// from it because Windows lands most DPCs and interrupt work there. The mask stays deliberately wide
/// (14 logical CPUs here rather than one), so the GC, finalizer and tiered-JIT background threads have
/// somewhere to run: pinning to a single core starves them and trades one measurement artifact for a
/// worse one.</para>
/// </summary>
internal static partial class BenchmarkTopology
{
    private const int RelationProcessorCore = 0;
    private const int RelationCache = 2;

    // sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION) on x64: ULONG_PTR ProcessorMask (8 bytes), then
    // LOGICAL_PROCESSOR_RELATIONSHIP (4), then 4 bytes of padding up to the 8-byte alignment the
    // union requires (it contains a ULONGLONG[2]), then the 16-byte union itself.
    private const int EntrySize = 32;
    private const int RelationshipOffset = 8;
    private const int CacheLevelOffset = 16;

    /// <summary>Smallest domain worth pinning to. Below this the runtime's own threads have nowhere to go.</summary>
    private const int MinimumDomainWidth = 8;

    /// <summary>Smallest mask left after excluding core 0. Below this, keep core 0 rather than over-constrain.</summary>
    private const int MinimumMaskWidth = 4;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnLength);

    /// <summary>
    /// Returns the affinity mask a benchmark process should run under, or <c>null</c> when this machine
    /// should be left to the scheduler.
    /// </summary>
    public static nint? ResolveAffinityMask()
    {
        // GetLogicalProcessorInformation only ever describes processor group 0, so a machine large
        // enough to have several groups would get a mask covering an arbitrary subset of itself.
        if (!OperatingSystem.IsWindows() || Environment.ProcessorCount > 64)
        {
            return null;
        }

        if (!TryReadTopology(out var lastLevelDomains, out var physicalCores))
        {
            return null;
        }

        // Prefer the domain holding CPU 0 (CCD0 on Zen, which carries the preferred cores), and fall
        // back to the widest domain on a topology that does not include it.
        var domain = lastLevelDomains.FirstOrDefault(static m => (m & 1) != 0);
        if (domain == 0)
        {
            domain = lastLevelDomains.OrderByDescending(BitOperations.PopCount).FirstOrDefault();
        }

        if (domain == 0 || BitOperations.PopCount(domain) < MinimumDomainWidth)
        {
            return null;
        }

        var core0 = physicalCores.FirstOrDefault(static m => (m & 1) != 0);
        var reduced = domain & ~core0;
        if (BitOperations.PopCount(reduced) >= MinimumMaskWidth)
        {
            domain = reduced;
        }

        return (nint) domain;
    }

    /// <summary>Renders a mask the way BenchmarkDotNet reports it, for the banner and the logs.</summary>
    public static string Describe(nint mask)
    {
        var cpus = new List<int>();
        for (var i = 0; i < 64; i++)
        {
            if (((ulong) mask >> i & 1) != 0)
            {
                cpus.Add(i);
            }
        }

        return cpus.Count == 0 ? "<empty>" : $"CPU {cpus[0]}-{cpus[^1]} ({cpus.Count} logical)";
    }

    private static bool TryReadTopology(out List<ulong> lastLevelDomains, out List<ulong> physicalCores)
    {
        lastLevelDomains = [];
        physicalCores = [];

        uint length = 0;
        // The first call is expected to fail with ERROR_INSUFFICIENT_BUFFER and report the size needed.
        GetLogicalProcessorInformation(IntPtr.Zero, ref length);
        if (length == 0 || length % EntrySize != 0)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal((int) length);
        try
        {
            if (!GetLogicalProcessorInformation(buffer, ref length))
            {
                return false;
            }

            var count = (int) (length / EntrySize);
            for (var i = 0; i < count; i++)
            {
                var entry = IntPtr.Add(buffer, i * EntrySize);
                var mask = (ulong) Marshal.ReadIntPtr(entry);
                switch (Marshal.ReadInt32(entry, RelationshipOffset))
                {
                    case RelationCache when Marshal.ReadByte(entry, CacheLevelOffset) == 3:
                        lastLevelDomains.Add(mask);
                        break;
                    case RelationProcessorCore:
                        physicalCores.Add(mask);
                        break;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return lastLevelDomains.Count > 0;
    }
}

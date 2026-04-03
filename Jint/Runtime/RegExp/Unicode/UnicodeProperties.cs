// Unicode property support for JavaScript regex engine
// Ported from QuickJS libunicode.c
// Original copyright (c) 2017-2018 Fabrice Bellard - MIT License

using System.Runtime.CompilerServices;

namespace Jint.Runtime.RegExp.Unicode;

internal enum CharRangeOp { Union = 0, Inter = 1, Xor = 2, Sub = 3 }

internal enum RunType
{
    U = 0, L = 1, UF = 2, LF = 3, UL = 4, LSU = 5,
    U2L_399_EXT2 = 6, UF_D20 = 7, UF_D1_EXT = 8, U_EXT = 9,
    LF_EXT = 10, UF_EXT2 = 11, LF_EXT2 = 12, UF_EXT3 = 13,
}

[Flags]
internal enum CaseMask { None = 0, U = 1, L = 2, F = 4 }

/// <summary>
/// Unicode property lookups for JS regex \p{...} patterns, case folding for /i,
/// and character class set operations for /v flag.
/// </summary>
internal static class UnicodeProperties
{
    private const int MaxCaseConvLen = 3;

    // ---- CharRange operations ----

    private static void CrCompress(List<uint> pts)
    {
        int len = pts.Count, i = 0, k = 0;
        while (i + 1 < len)
        {
            if (pts[i] == pts[i + 1]) { i += 2; continue; }
            int j = i;
            while (j + 3 < len && pts[j + 1] == pts[j + 2]) j += 2;
            pts[k] = pts[i]; pts[k + 1] = pts[j + 1]; k += 2; i = j + 2;
        }
        if (k < len) pts.RemoveRange(k, len - k);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CrAddInterval(List<uint> pts, uint c1, uint c2) { pts.Add(c1); pts.Add(c2); }

    public static void CrOp(List<uint> result, ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, CharRangeOp op)
    {
        int ai = 0, bi = 0, al = a.Length, bl = b.Length;
        for (; ; )
        {
            uint v;
            if (ai < al && bi < bl)
            {
                if (a[ai] < b[bi]) v = a[ai++];
                else if (a[ai] == b[bi]) { v = a[ai]; ai++; bi++; }
                else v = b[bi++];
            }
            else if (ai < al) v = a[ai++];
            else if (bi < bl) v = b[bi++];
            else break;
            int isIn = op switch
            {
                CharRangeOp.Union => (ai & 1) | (bi & 1),
                CharRangeOp.Inter => (ai & 1) & (bi & 1),
                CharRangeOp.Xor => (ai & 1) ^ (bi & 1),
                CharRangeOp.Sub => (ai & 1) & ((bi & 1) ^ 1),
                _ => 0,
            };
            if (isIn != (result.Count & 1)) result.Add(v);
        }
        CrCompress(result);
    }

    public static void CrOp(List<uint> pts, ReadOnlySpan<uint> b, CharRangeOp op)
    {
        uint[] a = pts.ToArray(); pts.Clear(); CrOp(pts, a, b, op);
    }

    public static void CrUnionInterval(List<uint> pts, uint c1, uint c2)
    {
        Span<uint> b = stackalloc uint[] { c1, c2 + 1 };
        CrOp(pts, b, CharRangeOp.Union);
    }

    public static void CrInvert(List<uint> pts)
    {
        pts.Insert(0, 0); pts.Add(uint.MaxValue); CrCompress(pts);
    }

    private static void CrSortAndRemoveOverlap(List<uint> pts)
    {
        int cnt = pts.Count / 2;
        if (cnt <= 1) return;
        // Use parallel arrays instead of tuples for net462 compatibility
        var starts = new uint[cnt];
        var ends = new uint[cnt];
        var indices = new int[cnt];
        for (int i = 0; i < cnt; i++)
        {
            starts[i] = pts[i * 2];
            ends[i] = pts[i * 2 + 1];
            indices[i] = i;
        }
        Array.Sort(indices, (a, b) => starts[a].CompareTo(starts[b]));
        pts.Clear();
        uint s = starts[indices[0]], e = ends[indices[0]];
        for (int i = 1; i < cnt; i++)
        {
            uint si = starts[indices[i]], ei = ends[indices[i]];
            if (si > e) { pts.Add(s); pts.Add(e); s = si; e = ei; }
            else if (ei > e) e = ei;
        }
        pts.Add(s);
        pts.Add(e);
    }

    // ---- Case conversion ----
    // case_conv_table1 packing: code(17 bits) | len(7 bits) | type(4 bits) | data_hi(4 bits)

    private static int CaseConvEntry(Span<uint> res, uint c, int convType, int idx, uint v)
    {
        bool isLower = convType != 0;
        var type = (RunType) ((v >> 4) & 0xf);
        uint data = ((v & 0xf) << 8) | UnicodeData.CaseConvTable2[idx];
        uint code = v >> 15;
        switch (type)
        {
            case RunType.U:
            case RunType.L:
            case RunType.UF:
            case RunType.LF:
                {
                    int t = (int) type;
                    if (convType == (t & 1) || (t >= (int) RunType.UF && convType == 2))
                        c = c - code + (UnicodeData.CaseConvTable1[(int) data] >> 15);
                    break;
                }
            case RunType.UL:
                {
                    uint a = c - code;
                    if ((a & 1) != (uint) (1 - (isLower ? 1 : 0))) break;
                    c = (a ^ 1) + code; break;
                }
            case RunType.LSU:
                {
                    uint a = c - code; int il = isLower ? 1 : 0;
                    if (a == 1) c = (uint) (c + 2 * il - 1);
                    else if (a == (uint) ((1 - il) * 2)) c = (uint) (c + (2 * il - 1) * 2);
                    break;
                }
            case RunType.U2L_399_EXT2:
                if (!isLower) { res[0] = c - code + UnicodeData.CaseConvExt[(int) data >> 6]; res[1] = 0x399; return 2; }
                c = c - code + UnicodeData.CaseConvExt[(int) data & 0x3f]; break;
            case RunType.UF_D20:
                if (convType == 1) break; c = data + (uint) (convType == 2 ? 0x20 : 0); break;
            case RunType.UF_D1_EXT:
                if (convType == 1) break; c = UnicodeData.CaseConvExt[(int) data] + (uint) (convType == 2 ? 1 : 0); break;
            case RunType.U_EXT:
            case RunType.LF_EXT:
                if ((isLower ? 1 : 0) != ((int) type - (int) RunType.U_EXT)) break;
                c = UnicodeData.CaseConvExt[(int) data]; break;
            case RunType.LF_EXT2:
                if (!isLower) break;
                res[0] = c - code + UnicodeData.CaseConvExt[(int) data >> 6];
                res[1] = UnicodeData.CaseConvExt[(int) data & 0x3f]; return 2;
            case RunType.UF_EXT2:
                if (convType == 1) break;
                res[0] = c - code + UnicodeData.CaseConvExt[(int) data >> 6];
                res[1] = UnicodeData.CaseConvExt[(int) data & 0x3f];
                if (convType == 2) { res[0] = CaseConv1(res[0], 1); res[1] = CaseConv1(res[1], 1); }
                return 2;
            default: // UF_EXT3
                if (convType == 1) break;
                res[0] = UnicodeData.CaseConvExt[(int) data >> 8];
                res[1] = UnicodeData.CaseConvExt[(int) ((data >> 4) & 0xf)];
                res[2] = UnicodeData.CaseConvExt[(int) data & 0xf];
                if (convType == 2) { res[0] = CaseConv1(res[0], 1); res[1] = CaseConv1(res[1], 1); res[2] = CaseConv1(res[2], 1); }
                return 3;
        }
        res[0] = c; return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CaseConv1(uint c, int ct)
    {
        Span<uint> r = stackalloc uint[MaxCaseConvLen]; CaseConv(r, c, ct); return r[0];
    }

    /// <summary>Case-convert code point. convType: 0=upper, 1=lower, 2=casefold.</summary>
    public static int CaseConv(Span<uint> res, uint c, int convType)
    {
        if (c < 128)
        {
            if (convType != 0) { if (c >= 'A' && c <= 'Z') c = c - 'A' + 'a'; }
            else { if (c >= 'a' && c <= 'z') c = c - 'a' + 'A'; }
        }
        else
        {
            ReadOnlySpan<uint> t1 = UnicodeData.CaseConvTable1;
            int lo = 0, hi = t1.Length - 1;
            while (lo <= hi)
            {
                int mid = (int) ((uint) (hi + lo) / 2);
                uint v = t1[mid], cd = v >> 15, ln = (v >> 8) & 0x7f;
                if (c < cd) hi = mid - 1;
                else if (c >= cd + ln) lo = mid + 1;
                else return CaseConvEntry(res, c, convType, mid, v);
            }
        }
        res[0] = c; return 1;
    }

    private static uint CaseFoldingEntry(uint c, int idx, uint v, bool isUnicode)
    {
        Span<uint> res = stackalloc uint[MaxCaseConvLen];
        if (isUnicode)
        {
            int len = CaseConvEntry(res, c, 2, idx, v);
            if (len == 1) c = res[0];
            else { if (c == 0xfb06) c = 0xfb05; else if (c == 0x1fd3) c = 0x390; else if (c == 0x1fe3) c = 0x3b0; }
        }
        else
        {
            if (c < 128) { if (c >= 'a' && c <= 'z') c = c - 'a' + 'A'; }
            else { int len = CaseConvEntry(res, c, 0, idx, v); if (len == 1 && res[0] >= 128) c = res[0]; }
        }
        return c;
    }

    /// <summary>JS regex Canonicalize abstract operation.</summary>
    public static uint Canonicalize(uint c, bool isUnicode)
    {
        if (c < 128)
        {
            if (isUnicode) { if (c >= 'A' && c <= 'Z') c = c - 'A' + 'a'; }
            else { if (c >= 'a' && c <= 'z') c = c - 'a' + 'A'; }
        }
        else
        {
            ReadOnlySpan<uint> t1 = UnicodeData.CaseConvTable1;
            int lo = 0, hi = t1.Length - 1;
            while (lo <= hi)
            {
                int mid = (int) ((uint) (hi + lo) / 2);
                uint v = t1[mid], cd = v >> 15, ln = (v >> 8) & 0x7f;
                if (c < cd) hi = mid - 1;
                else if (c >= cd + ln) lo = mid + 1;
                else return CaseFoldingEntry(c, mid, v, isUnicode);
            }
        }
        return c;
    }

    // ---- UnicodeCase1 ----

    private static int UnicodeCase1(List<uint> cr, CaseMask cm)
    {
        if (cm == CaseMask.None) return 0;
        uint mU = (1u << 0) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 13);
        uint mL = (1u << 1) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 10) | (1 << 12);
        uint mF = (1u << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 10) | (1 << 12) | (1 << 7) | (1 << 8) | (1 << 11) | (1 << 13);
        uint mask = 0;
        if ((cm & CaseMask.U) != CaseMask.None) mask |= mU;
        if ((cm & CaseMask.L) != CaseMask.None) mask |= mL;
        if ((cm & CaseMask.F) != CaseMask.None) mask |= mF;
        ReadOnlySpan<uint> t1 = UnicodeData.CaseConvTable1;
        for (int idx = 0; idx < t1.Length; idx++)
        {
            uint v = t1[idx]; var ty = (RunType) ((v >> 4) & 0xf);
            uint code = v >> 15, len = (v >> 8) & 0x7f;
            if (((mask >> (int) ty) & 1) == 0) continue;
            switch (ty)
            {
                case RunType.UL:
                    if ((cm & CaseMask.U) != CaseMask.None && (cm & (CaseMask.L | CaseMask.F)) != CaseMask.None) goto default;
                    uint off = (cm & CaseMask.U) != CaseMask.None ? 1u : 0u;
                    for (uint i = 0; i < len; i += 2) CrAddInterval(cr, code + off + i, code + off + i + 1);
                    break;
                case RunType.LSU:
                    if ((cm & CaseMask.U) != CaseMask.None && (cm & (CaseMask.L | CaseMask.F)) != CaseMask.None) goto default;
                    if ((cm & CaseMask.U) == CaseMask.None) CrAddInterval(cr, code, code + 1);
                    CrAddInterval(cr, code + 1, code + 2);
                    if ((cm & CaseMask.U) != CaseMask.None) CrAddInterval(cr, code + 2, code + 3);
                    break;
                default: CrAddInterval(cr, code, code + len); break;
            }
        }
        return 0;
    }

    // ---- CrCanonicalize ----

    public static void CrCanonicalize(List<uint> points, bool isUnicode)
    {
        var crMask = new List<uint>(); var crInter = new List<uint>();
        var crResult = new List<uint>(); var crSub = new List<uint>();
        UnicodeCase1(crMask, isUnicode ? CaseMask.F : CaseMask.U);
        CrOp(crInter, crMask.ToArray(), points.ToArray(), CharRangeOp.Inter);
        CrInvert(crMask);
        CrOp(crSub, crMask.ToArray(), points.ToArray(), CharRangeOp.Inter);
        ReadOnlySpan<uint> t1 = UnicodeData.CaseConvTable1;
        uint dS = uint.MaxValue, dE = uint.MaxValue;
        int ti = 0; uint v = t1[ti], tc = v >> 15, tl = (v >> 8) & 0x7f;
        for (int i = 0; i < crInter.Count; i += 2)
            for (uint c = crInter[i]; c < crInter[i + 1]; c++)
            {
                while (!(c >= tc && c < tc + tl)) { ti++; v = t1[ti]; tc = v >> 15; tl = (v >> 8) & 0x7f; }
                uint d = CaseFoldingEntry(c, ti, v, isUnicode);
                if (dS == uint.MaxValue) { dS = d; dE = d + 1; }
                else if (dE == d) dE++;
                else { CrAddInterval(crResult, dS, dE); dS = d; dE = d + 1; }
            }
        if (dS != uint.MaxValue) CrAddInterval(crResult, dS, dE);
        CrSortAndRemoveOverlap(crResult);
        points.Clear();
        CrOp(points, crResult.ToArray(), crSub.ToArray(), CharRangeOp.Union);
    }

    // ---- Name table lookup ----

    private const string NameSep = "\\0";

    private static int FindName(string tbl, string name)
    {
        int pos = 0;
        int idx = 0;
        while (idx < tbl.Length)
        {
            int ee = tbl.IndexOf(NameSep, idx, StringComparison.Ordinal);
            if (ee < 0)
            {
                ee = tbl.Length;
            }
            int aS = idx;
            while (aS < ee)
            {
                int cp = tbl.IndexOf(',', aS, ee - aS);
                int aE = cp >= 0 ? cp : ee;
                if (aE - aS == name.Length &&
                    string.Compare(tbl, aS, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return pos;
                }
                aS = aE + 1;
            }
            pos++;
            idx = ee + NameSep.Length;
        }
        return -1;
    }

    // ---- Unicode Script ----

    public static int UnicodeScript(List<uint> cr, string scriptName, bool isExt)
    {
        int si = FindName(UnicodeData.ScriptNameTable, scriptName);
        if (si < 0) return -2;
        bool isCom = si == UnicodeData.ScriptCommon || si == UnicodeData.ScriptInherited;
        List<uint> cr1 = isExt ? new List<uint>() : cr;
        var cr2 = new List<uint>();
        ReadOnlySpan<byte> tab = UnicodeData.ScriptTable;
        int p = 0; uint c = 0;
        while (p < tab.Length)
        {
            byte b = tab[p++]; int ty = b >> 7, n = b & 0x7f;
            if (n >= 112) { n = ((n - 112) << 16) | (tab[p++] << 8) | tab[p++]; n += 96 + (1 << 12); }
            else if (n >= 96) { n = ((n - 96) << 8) | tab[p++]; n += 96; }
            uint c1 = c + (uint) n + 1;
            if (ty != 0) { int sv = tab[p++]; if (sv == si || si == UnicodeData.ScriptUnknown) CrAddInterval(cr1, c, c1); }
            c = c1;
        }
        if (si == UnicodeData.ScriptUnknown) CrInvert(cr1);
        if (isExt)
        {
            ReadOnlySpan<byte> et = UnicodeData.ScriptExtTable;
            p = 0; c = 0;
            while (p < et.Length)
            {
                byte b = et[p++]; int n;
                if (b < 128) n = b;
                else if (b < 192) { n = ((b - 128) << 8) | et[p++]; n += 128; }
                else { n = ((b - 192) << 16) | (et[p++] << 8) | et[p++]; n += 128 + (1 << 14); }
                uint c1 = c + (uint) n + 1; int vl = et[p++];
                if (isCom) { if (vl != 0) CrAddInterval(cr2, c, c1); }
                else { for (int i = 0; i < vl; i++) if (et[p + i] == si) { CrAddInterval(cr2, c, c1); break; } }
                p += vl; c = c1;
            }
            if (isCom) { CrInvert(cr2); CrOp(cr, cr1.ToArray(), cr2.ToArray(), CharRangeOp.Inter); }
            else CrOp(cr, cr1.ToArray(), cr2.ToArray(), CharRangeOp.Union);
        }
        return 0;
    }

    // ---- Unicode General Category ----

    private static int UnicodeGeneralCategory1(List<uint> cr, uint gcMask)
    {
        ReadOnlySpan<byte> tab = UnicodeData.GcTable;
        int p = 0; uint c = 0;
        while (p < tab.Length)
        {
            byte b = tab[p++]; int n = b >> 5, v = b & 0x1f;
            if (n == 7)
            {
                n = tab[p++];
                if (n >= 192) { n = ((n - 192) << 16) | (tab[p++] << 8) | tab[p++]; n += 7 + 128 + (1 << 14); }
                else if (n >= 128) { n = ((n - 128) << 8) | tab[p++]; n += 7 + 128; }
                else n += 7;
            }
            uint c0 = c; c += (uint) n + 1;
            if (v == 31)
            {
                uint m = gcMask & ((1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl));
                if (m != 0)
                {
                    if (m == ((1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl))) CrAddInterval(cr, c0, c);
                    else { uint o = (gcMask & (1u << UnicodeData.GcLl)) != 0 ? 1u : 0u; for (uint ci = c0 + o; ci < c; ci += 2) CrAddInterval(cr, ci, ci + 1); }
                }
            }
            else if (((gcMask >> v) & 1) != 0) CrAddInterval(cr, c0, c);
        }
        return 0;
    }

    public static int UnicodeGeneralCategory(List<uint> cr, string gcName)
    {
        int gi = FindName(UnicodeData.GcNameTable, gcName);
        if (gi < 0) return -2;
        uint m = gi <= UnicodeData.GcCo ? 1u << gi : UnicodeData.GcMaskTable[gi - UnicodeData.GcLC];
        return UnicodeGeneralCategory1(cr, m);
    }

    // ---- Unicode Binary Property ----

    private static int UnicodeProp1(List<uint> cr, int propIdx)
    {
        ReadOnlySpan<byte> tab = UnicodeData.GetPropTable(propIdx);
        uint c = 0; int bit = 0, p = 0;
        while (p < tab.Length)
        {
            uint c0 = c; byte b = tab[p++];
            if (b < 64) { c += (uint) ((b >> 3) + 1); if (bit != 0) CrAddInterval(cr, c0, c); bit ^= 1; c0 = c; c += (uint) ((b & 7) + 1); }
            else if (b >= 0x80) c += (uint) (b - 0x80 + 1);
            else if (b < 0x60) { c += (uint) ((((b - 0x40) << 8) | tab[p]) + 1); p++; }
            else { c += (uint) ((((b - 0x60) << 16) | (tab[p] << 8) | tab[p + 1]) + 1); p += 2; }
            if (bit != 0) CrAddInterval(cr, c0, c);
            bit ^= 1;
        }
        return 0;
    }

    public static int UnicodeProp(List<uint> cr, string propName)
    {
        int pi = FindName(UnicodeData.PropNameTable, propName);
        if (pi < 0) return -2;
        pi += UnicodeData.PropASCIIHexDigit;
        switch (pi)
        {
            case UnicodeData.PropASCII: CrAddInterval(cr, 0, 0x80); return 0;
            case UnicodeData.PropAny: CrAddInterval(cr, 0, 0x110000); return 0;
            case UnicodeData.PropAssigned: UnicodeGeneralCategory1(cr, 1u << UnicodeData.GcCn); CrInvert(cr); return 0;
            case UnicodeData.PropMath:
                return PropOps(cr, PopGc, 1u << UnicodeData.GcSm, PopProp, (uint) UnicodeData.PropOtherMath, PopUnion);
            case UnicodeData.PropLowercase:
                return PropOps(cr, PopGc, 1u << UnicodeData.GcLl, PopProp, (uint) UnicodeData.PropOtherLowercase, PopUnion);
            case UnicodeData.PropUppercase:
                return PropOps(cr, PopGc, 1u << UnicodeData.GcLu, PopProp, (uint) UnicodeData.PropOtherUppercase, PopUnion);
            case UnicodeData.PropCased:
                return PropOps(cr, PopGc, (1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl) | (1u << UnicodeData.GcLt),
                    PopProp, (uint) UnicodeData.PropOtherUppercase, PopUnion,
                    PopProp, (uint) UnicodeData.PropOtherLowercase, PopUnion);
            case UnicodeData.PropAlphabetic:
                return PropOps(cr, PopGc,
                    (1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl) | (1u << UnicodeData.GcLt) |
                    (1u << UnicodeData.GcLm) | (1u << UnicodeData.GcLo) | (1u << UnicodeData.GcNl),
                    PopProp, (uint) UnicodeData.PropOtherUppercase, PopUnion,
                    PopProp, (uint) UnicodeData.PropOtherLowercase, PopUnion,
                    PopProp, (uint) UnicodeData.PropOtherAlphabetic, PopUnion);
            case UnicodeData.PropGraphemeBase:
                return PropOps(cr, PopGc,
                    (1u << UnicodeData.GcCc) | (1u << UnicodeData.GcCf) | (1u << UnicodeData.GcCs) |
                    (1u << UnicodeData.GcCo) | (1u << UnicodeData.GcCn) | (1u << UnicodeData.GcZl) |
                    (1u << UnicodeData.GcZp) | (1u << UnicodeData.GcMe) | (1u << UnicodeData.GcMn),
                    PopProp, (uint) UnicodeData.PropOtherGraphemeExtend, PopUnion, PopInvert);
            case UnicodeData.PropGraphemeExtend:
                return PropOps(cr, PopGc, (1u << UnicodeData.GcMe) | (1u << UnicodeData.GcMn),
                    PopProp, (uint) UnicodeData.PropOtherGraphemeExtend, PopUnion);
            case UnicodeData.PropXIDStart:
                return PropOps(cr, PopGc,
                    (1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl) | (1u << UnicodeData.GcLt) |
                    (1u << UnicodeData.GcLm) | (1u << UnicodeData.GcLo) | (1u << UnicodeData.GcNl),
                    PopProp, (uint) UnicodeData.PropOtherIDStart, PopUnion,
                    PopProp, (uint) UnicodeData.PropPatternSyntax,
                    PopProp, (uint) UnicodeData.PropPatternWhiteSpace, PopUnion,
                    PopProp, (uint) UnicodeData.PropXIDStart1, PopUnion,
                    PopInvert, PopInter);
            case UnicodeData.PropXIDContinue:
                return PropOps(cr, PopGc,
                    (1u << UnicodeData.GcLu) | (1u << UnicodeData.GcLl) | (1u << UnicodeData.GcLt) |
                    (1u << UnicodeData.GcLm) | (1u << UnicodeData.GcLo) | (1u << UnicodeData.GcNl) |
                    (1u << UnicodeData.GcMn) | (1u << UnicodeData.GcMc) | (1u << UnicodeData.GcNd) |
                    (1u << UnicodeData.GcPc),
                    PopProp, (uint) UnicodeData.PropOtherIDStart, PopUnion,
                    PopProp, (uint) UnicodeData.PropOtherIDContinue, PopUnion,
                    PopProp, (uint) UnicodeData.PropPatternSyntax,
                    PopProp, (uint) UnicodeData.PropPatternWhiteSpace, PopUnion,
                    PopProp, (uint) UnicodeData.PropXIDContinue1, PopUnion,
                    PopInvert, PopInter);
            case UnicodeData.PropChangesWhenUppercased:
                return UnicodeCase1(cr, CaseMask.U);
            case UnicodeData.PropChangesWhenLowercased:
                return UnicodeCase1(cr, CaseMask.L);
            case UnicodeData.PropChangesWhenCasemapped:
                return UnicodeCase1(cr, CaseMask.U | CaseMask.L | CaseMask.F);
            case UnicodeData.PropChangesWhenTitlecased:
                return PropOps(cr, PopCase, (uint) CaseMask.U,
                    PopProp, (uint) UnicodeData.PropChangesWhenTitlecased1, PopXor);
            case UnicodeData.PropChangesWhenCasefolded:
                return PropOps(cr, PopCase, (uint) CaseMask.F,
                    PopProp, (uint) UnicodeData.PropChangesWhenCasefolded1, PopXor);
            case UnicodeData.PropChangesWhenNFKCCasefolded:
                return PropOps(cr, PopCase, (uint) CaseMask.F,
                    PopProp, (uint) UnicodeData.PropChangesWhenNFKCCasefolded1, PopXor);
            case UnicodeData.PropIDContinue:
                return PropOps(cr, PopProp, (uint) UnicodeData.PropIDStart,
                    PopProp, (uint) UnicodeData.PropIDContinue1, PopXor);
            default:
                if (pi >= UnicodeData.PropTableCount) return -2;
                return UnicodeProp1(cr, pi);
        }
    }

    // ---- Property ops stack machine ----

    private const uint PopGc = 0;
    private const uint PopProp = 1;
    private const uint PopCase = 2;
    private const uint PopUnion = 3;
    private const uint PopInter = 4;
    private const uint PopXor = 5;
    private const uint PopInvert = 6;

    private static int PropOps(List<uint> cr, params uint[] ops)
    {
        var stk = new List<List<uint>>();
        int i = 0;
        while (i < ops.Length)
        {
            uint op = ops[i++];
            switch (op)
            {
                case PopGc:
                    {
                        var s = new List<uint>();
                        if (UnicodeGeneralCategory1(s, ops[i++]) < 0)
                        {
                            return -1;
                        }
                        stk.Add(s);
                        break;
                    }
                case PopProp:
                    {
                        var s = new List<uint>();
                        if (UnicodeProp1(s, (int) ops[i++]) < 0)
                        {
                            return -1;
                        }
                        stk.Add(s);
                        break;
                    }
                case PopCase:
                    {
                        var s = new List<uint>();
                        if (UnicodeCase1(s, (CaseMask) ops[i++]) < 0)
                        {
                            return -1;
                        }
                        stk.Add(s);
                        break;
                    }
                case PopUnion:
                case PopInter:
                case PopXor:
                    {
                        var b2 = stk[stk.Count - 1];
                        stk.RemoveAt(stk.Count - 1);
                        var a2 = stk[stk.Count - 1];
                        stk.RemoveAt(stk.Count - 1);
                        var r = new List<uint>();
                        CrOp(r, a2.ToArray(), b2.ToArray(), (CharRangeOp) (op - PopUnion));
                        stk.Add(r);
                        break;
                    }
                case PopInvert:
                    {
                        CrInvert(stk[stk.Count - 1]);
                        break;
                    }
            }
        }
        if (stk.Count == 1)
        {
            cr.AddRange(stk[0]);
            return 0;
        }
        return -1;
    }

    // ---- Sequence properties ----

    public static int UnicodeSequenceProp(string propName, Action<uint[], int> cb, List<uint> cr)
    {
        int si = FindName(UnicodeData.SequencePropNameTable, propName);
        return si < 0 ? -2 : SeqProp1(si, cb, cr);
    }

    private static int SeqProp1(int si, Action<uint[], int> cb, List<uint> cr)
    {
        var seq = new uint[16];
        switch (si)
        {
            case UnicodeData.SeqPropBasicEmoji:
                if (UnicodeProp1(cr, UnicodeData.PropBasicEmoji1) < 0) return -1;
                for (int i = 0; i < cr.Count; i += 2) for (uint c = cr[i]; c < cr[i + 1]; c++) { seq[0] = c; cb(seq, 1); }
                cr.Clear();
                if (UnicodeProp1(cr, UnicodeData.PropBasicEmoji2) < 0) return -1;
                for (int i = 0; i < cr.Count; i += 2) for (uint c = cr[i]; c < cr[i + 1]; c++) { seq[0] = c; seq[1] = 0xfe0f; cb(seq, 2); }
                break;
            case UnicodeData.SeqPropRGIEmojiModifierSequence:
                if (UnicodeProp1(cr, UnicodeData.PropEmojiModifierBase) < 0) return -1;
                for (int i = 0; i < cr.Count; i += 2) for (uint c = cr[i]; c < cr[i + 1]; c++) for (int j = 0; j < 5; j++) { seq[0] = c; seq[1] = (uint) (0x1f3fb + j); cb(seq, 2); }
                break;
            case UnicodeData.SeqPropRGIEmojiFlagSequence:
                if (UnicodeProp1(cr, UnicodeData.PropRGIEmojiFlagSequence) < 0) return -1;
                for (int i = 0; i < cr.Count; i += 2) for (uint c = cr[i]; c < cr[i + 1]; c++) { seq[0] = 0x1F1E6 + c / 26; seq[1] = 0x1F1E6 + c % 26; cb(seq, 2); }
                break;
            case UnicodeData.SeqPropRGIEmojiZWJSequence:
                {
                    ReadOnlySpan<byte> tab = UnicodeData.RgiEmojiZwjSequence;
                    int p = 0;
                    while (p < tab.Length)
                    {
                        int entryLen = tab[p++];
                        int k = 0;
                        int mod = 0;
                        int mc = 0;
                        int[] mp = new int[2];
                        int hp = -1;
                        for (int j = 0; j < entryLen; j++)
                        {
                            int cd = tab[p++] | (tab[p++] << 8);
                            int pres = cd >> 15;
                            int m1 = (cd >> 13) & 3;
                            cd &= 0x1FFF;
                            uint cv = cd < 0x1000 ? (uint) (cd + 0x2000) : (uint) (0x1F000 + cd - 0x1000);
                            if (cv == 0x1F9B0)
                            {
                                hp = k;
                            }
                            seq[k++] = cv;
                            if (m1 != 0)
                            {
                                mod = m1;
                                if (mc < 2)
                                {
                                    mp[mc] = k;
                                }
                                mc++;
                                seq[k++] = 0;
                            }
                            if (pres != 0)
                            {
                                seq[k++] = 0xFE0F;
                            }
                            if (j < entryLen - 1)
                            {
                                seq[k++] = 0x200D;
                            }
                        }

                        int nm = mod switch { 1 => 5, 2 => 25, 3 => 20, _ => 1 };
                        int nh = hp >= 0 ? 4 : 1;
                        for (int hi = 0; hi < nh; hi++)
                        {
                            for (int mi = 0; mi < nm; mi++)
                            {
                                if (hp >= 0)
                                {
                                    seq[hp] = (uint) (0x1F9B0 + hi);
                                }
                                if (mod == 1)
                                {
                                    seq[mp[0]] = (uint) (0x1F3FB + mi);
                                }
                                else if (mod >= 2)
                                {
                                    int i0 = mi / 5;
                                    int i1 = mi % 5;
                                    if (mod == 3 && i0 >= i1)
                                    {
                                        i0++;
                                    }
                                    seq[mp[0]] = (uint) (0x1F3FB + i0);
                                    if (mc > 1)
                                    {
                                        seq[mp[1]] = (uint) (0x1F3FB + i1);
                                    }
                                }
                                cb(seq, k);
                            }
                        }
                    }
                    break;
                }
            case UnicodeData.SeqPropRGIEmojiTagSequence:
                {
                    ReadOnlySpan<byte> tab = UnicodeData.RgiEmojiTagSequence;
                    int p = 0;
                    while (p < tab.Length)
                    {
                        int j = 0;
                        seq[j++] = 0x1F3F4;
                        for (; ; )
                        {
                            int cv = tab[p++];
                            if (cv == 0)
                            {
                                break;
                            }
                            seq[j++] = (uint) (0xE0000 + cv);
                        }
                        seq[j++] = 0xE007F;
                        cb(seq, j);
                    }
                    break;
                }
            case UnicodeData.SeqPropEmojiKeycapSequence:
                {
                    if (UnicodeProp1(cr, UnicodeData.PropEmojiKeycapSequence) < 0)
                    {
                        return -1;
                    }
                    for (int i = 0; i < cr.Count; i += 2)
                    {
                        for (uint c = cr[i]; c < cr[i + 1]; c++)
                        {
                            seq[0] = c;
                            seq[1] = 0xFE0F;
                            seq[2] = 0x20E3;
                            cb(seq, 3);
                        }
                    }
                    break;
                }
            case UnicodeData.SeqPropRGIEmoji:
                {
                    for (int i = UnicodeData.SeqPropBasicEmoji; i <= UnicodeData.SeqPropRGIEmojiZWJSequence; i++)
                    {
                        int r = SeqProp1(i, cb, cr);
                        if (r < 0)
                        {
                            return r;
                        }
                        cr.Clear();
                    }
                    break;
                }
            default:
                return -2;
        }
        return 0;
    }
}

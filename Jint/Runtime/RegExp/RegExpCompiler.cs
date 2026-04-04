// Ported from QuickJS libregexp.c
//
// Copyright (c) 2017-2018 Fabrice Bellard
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.Runtime.RegExp.Unicode;

namespace Jint.Runtime.RegExp;

// Section: CharRange helper

/// <summary>
/// Manages a sorted list of [start, end) code point intervals stored as flat uint values.
/// Port of the C CharRange struct and cr_* functions from libunicode.
/// </summary>
internal sealed class CharRange
{
    /// <summary>Points sorted by increasing value. Length is always even (start/end pairs).</summary>
    public readonly List<uint> Points = new();

    public int Length => Points.Count;

    public void Clear()
    {
        Points.Clear();
    }

    public void AddPoint(uint v)
    {
        Points.Add(v);
    }

    public void AddInterval(uint c1, uint c2)
    {
        Points.Add(c1);
        Points.Add(c2);
    }

    public void CopyFrom(CharRange other)
    {
        Points.Clear();
        Points.AddRange(other.Points);
    }

    /// <summary>
    /// Union a single character interval [c1, c2+1). Port of cr_union_interval.
    /// </summary>
    public void UnionInterval(uint c1, uint c2)
    {
        uint[] bPt = [c1, c2 + 1];
        Op1(bPt, CharRangeOp.Union);
    }

    /// <summary>
    /// Invert the character range (complement). Port of cr_invert.
    /// </summary>
    public void Invert()
    {
        var pts = Points;
        int len = pts.Count;

        if (len == 0)
        {
            pts.Add(0);
            pts.Add(CharRangeHelpers.CharRangeMax);
            return;
        }

        if (pts[0] == 0)
        {
            pts.RemoveAt(0);
        }
        else
        {
            pts.Insert(0, 0);
        }

        if (pts.Count > 0 && pts[^1] == CharRangeHelpers.CharRangeMax)
        {
            pts.RemoveAt(pts.Count - 1);
        }
        else
        {
            pts.Add(CharRangeHelpers.CharRangeMax);
        }
    }

    /// <summary>
    /// Apply set operation: a = a op b. Port of cr_op1.
    /// </summary>
    public void Op1(ReadOnlySpan<uint> bPt, CharRangeOp op)
    {
        var aPt = Points.ToArray();
        var result = new List<uint>();
        CharRangeHelpers.Op(result, aPt, bPt, op);
        Points.Clear();
        Points.AddRange(result);
    }

    /// <summary>
    /// Apply set operation: a = a op b (where b is another CharRange).
    /// </summary>
    public void Op1(CharRange b, CharRangeOp op)
    {
        Op1(b.Points.ToArray().AsSpan(), op);
    }

    /// <summary>
    /// Canonicalize the range for case-insensitive matching.
    /// Port of cr_regexp_canonicalize.
    /// </summary>
    public void RegexpCanonicalize(bool isUnicode)
    {
        UnicodeProperties.CrCanonicalize(Points, isUnicode);
    }
}

// Section: CharRange set operations

internal static class CharRangeHelpers
{
    /// <summary>
    /// Range extends beyond the maximum Unicode code point.
    /// In the C code this corresponds to ranges up to UINT32_MAX.
    /// </summary>
    public const uint CharRangeMax = 0x110000;

    /// <summary>
    /// Port of cr_op: compute result = a op b (union, inter, xor, sub).
    /// Both a and b must be sorted non-overlapping interval arrays with [start, end) pairs.
    /// </summary>
    public static void Op(List<uint> result, ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, CharRangeOp op)
    {
        int ai = 0, bi = 0;
        int aLen = a.Length, bLen = b.Length;
        bool inA = false, inB = false;
        bool prevInResult = false;

        // Merge all boundary points from both ranges
        while (ai < aLen || bi < bLen)
        {
            uint va = ai < aLen ? a[ai] : uint.MaxValue;
            uint vb = bi < bLen ? b[bi] : uint.MaxValue;
            uint v;

            if (va <= vb)
            {
                v = va;
                ai++;
                inA = !inA;
            }
            else
            {
                v = vb;
                bi++;
                inB = !inB;
            }

            // Handle ties
            if (va == vb && bi <= bLen && ai <= aLen && va != uint.MaxValue)
            {
                if (bi < bLen || (bi == bLen && vb == va))
                {
                    // If both had the same value, we already consumed 'a',
                    // now consume 'b' too
                    if (va == vb && bi > 0 && b[bi - 1] == va)
                    {
                        // Already handled
                    }
                    else if (va == vb)
                    {
                        bi++;
                        inB = !inB;
                    }
                }
            }

            bool inResult = op switch
            {
                CharRangeOp.Union => inA || inB,
                CharRangeOp.Inter => inA && inB,
                CharRangeOp.Xor => inA ^ inB,
                CharRangeOp.Sub => inA && !inB,
                _ => false,
            };

            if (inResult != prevInResult)
            {
                result.Add(v);
                prevInResult = inResult;
            }
        }
    }
}

// Section: REStringList

/// <summary>
/// Represents a set of characters (via CharRange) plus multi-codepoint strings
/// (via a HashSet). Used for unicode-sets (v-flag) character class operations.
/// Port of the C REStringList struct.
/// </summary>
internal sealed class REStringList
{
    public readonly CharRange Cr = new();
    private readonly HashSet<string> _strings = new(StringComparer.Ordinal);

    public int StringCount => _strings.Count;

    public void Clear()
    {
        Cr.Clear();
        _strings.Clear();
    }

    /// <summary>
    /// Add a code point sequence. Single code points go into the char range;
    /// multi-codepoint sequences go into the string hash set.
    /// Port of re_string_add.
    /// </summary>
    public void Add(ReadOnlySpan<uint> buf)
    {
        if (buf.Length == 1)
        {
            Cr.UnionInterval(buf[0], buf[0]);
        }
        else
        {
            _strings.Add(CodePointsToString(buf));
        }
    }

    /// <summary>a = a op b. Port of re_string_list_op.</summary>
    public void Op(REStringList b, CharRangeOp op)
    {
        Cr.Op1(b.Cr, op);

        switch (op)
        {
            case CharRangeOp.Union:
                foreach (var s in b._strings)
                    _strings.Add(s);
                break;

            case CharRangeOp.Inter:
                _strings.IntersectWith(b._strings);
                break;

            case CharRangeOp.Sub:
                _strings.ExceptWith(b._strings);
                break;

            default:
                throw new InvalidOperationException("Unsupported string list operation");
        }
    }

    /// <summary>
    /// Canonicalize all strings and char ranges for case-insensitive matching.
    /// Port of re_string_list_canonicalize.
    /// </summary>
    public void Canonicalize(bool isUnicode)
    {
        Cr.RegexpCanonicalize(isUnicode);

        if (_strings.Count != 0)
        {
            var original = new string[_strings.Count];
            _strings.CopyTo(original);
            _strings.Clear();

            foreach (var s in original)
            {
                var cps = StringToCodePoints(s);
                for (int j = 0; j < cps.Length; j++)
                {
                    cps[j] = UnicodeProperties.Canonicalize(cps[j], isUnicode);
                }
                Add(cps);
            }
        }
    }

    /// <summary>
    /// Get all multi-char strings sorted by descending length (longest first).
    /// Used for emitting match alternatives.
    /// </summary>
    public List<uint[]> GetStringsSortedByLengthDesc()
    {
        var list = new List<uint[]>();
        foreach (var s in _strings)
        {
            var cps = StringToCodePoints(s);
            if (cps.Length > 0)
                list.Add(cps);
        }
        // Sort by length descending (longest first for greedy matching)
        list.Sort((a, b) => b.Length.CompareTo(a.Length));
        return list;
    }

    public bool HasEmptyString()
    {
        return _strings.Contains("");
    }

    private static string CodePointsToString(ReadOnlySpan<uint> buf)
    {
        var sb = new System.Text.StringBuilder(buf.Length);
        foreach (uint cp in buf)
        {
            if (cp <= 0xFFFF)
                sb.Append((char) cp);
            else
                sb.Append(char.ConvertFromUtf32((int) cp));
        }
        return sb.ToString();
    }

    private static uint[] StringToCodePoints(string s)
    {
        var list = new List<uint>();
        for (int i = 0; i < s.Length; i++)
        {
            uint cp;
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                cp = (uint) char.ConvertToUtf32(s[i], s[i + 1]);
                i++;
            }
            else
            {
                cp = s[i];
            }
            list.Add(cp);
        }
        return list.ToArray();
    }
}

// Section: Character range enum for built-in classes

internal enum CharRangeEnum : uint
{
    Digit = 0,      // \d
    NotDigit = 1,    // \D
    Space = 2,       // \s
    NotSpace = 3,    // \S
    Word = 4,        // \w
    NotWord = 5,     // \W
}

// Section: Group name entry

[StructLayout(LayoutKind.Auto)]
internal readonly record struct GroupNameEntry(string? Name, byte Scope);

// Section: Compiler entry point

/// <summary>
/// Compiles JavaScript regex patterns to bytecode.
/// Faithfully ported from QuickJS libregexp.c (lre_compile and supporting functions).
/// </summary>
internal static class RegExpCompiler
{
    private const int CaptureCountMax = 255;
    private const int RegisterCountMax = 255;
    private const uint ClassRangeBase = 0x40000000;

    /// <summary>
    /// Trailer length after the group name including the trailing '\0'.
    /// Matches LRE_GROUP_NAME_TRAILER_LEN in the C code.
    /// </summary>
    private const int GroupNameTrailerLen = 2;

    // Section: Built-in character class ranges (same data as C code)

    private static ReadOnlySpan<ushort> CharRangeD =>
    [
        1,
        0x0030, 0x0039 + 1,
    ];

    private static ReadOnlySpan<ushort> CharRangeS =>
    [
        10,
        0x0009, 0x000D + 1,
        0x0020, 0x0020 + 1,
        0x00A0, 0x00A0 + 1,
        0x1680, 0x1680 + 1,
        0x2000, 0x200A + 1,
        0x2028, 0x2029 + 1,
        0x202F, 0x202F + 1,
        0x205F, 0x205F + 1,
        0x3000, 0x3000 + 1,
        0xFEFF, 0xFEFF + 1,
    ];

    private static ReadOnlySpan<ushort> CharRangeW =>
    [
        4,
        0x0030, 0x0039 + 1,
        0x0041, 0x005A + 1,
        0x005F, 0x005F + 1,
        0x0061, 0x007A + 1,
    ];

    // Section: Public API

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    /// <summary>
    /// Compile a JavaScript regular expression pattern into bytecode.
    /// </summary>
    /// <param name="pattern">The regex pattern string (without delimiters).</param>
    /// <param name="flags">The regex flags.</param>
    /// <param name="cancellationToken">Optional cancellation token for timeout/abort.</param>
    /// <returns>The compiled bytecode including the 8-byte header.</returns>
    /// <exception cref="RegExpSyntaxException">Thrown on syntax errors in the pattern.</exception>
    public static byte[] Compile(string pattern, RegExpFlags flags, CancellationToken cancellationToken = default)
    {
        var s = new REParseState(pattern, flags, cancellationToken);
        return s.Compile();
    }

    // Section: Parse state

    /// <summary>
    /// Internal parse state for the regex compiler. Corresponds to REParseState in the C code.
    /// </summary>
    private sealed class REParseState
    {
        // Bytecode buffer (replaces DynBuf)
        private readonly List<byte> _byteCode = new();

        // Pattern input
        private readonly string _pattern;
        private int _pos;
        private readonly int _patternEnd;

        // Flags
        private readonly RegExpFlags _reFlags;
        internal readonly bool IsUnicode;
        internal readonly bool UnicodeSets;
        internal bool IgnoreCase;
        internal bool MultiLine;
        internal bool DotAll;

        // Capture and group tracking
        internal byte GroupNameScope;
        internal int CaptureCount;
        internal int TotalCaptureCount;
        internal int HasNamedCaptures; // -1 = don't know, 0 = no, 1 = yes
        internal readonly List<GroupNameEntry> GroupNames = new();

        // Cancellation and stack overflow protection
        private readonly CancellationToken _cancellationToken;
        private int _recursionDepth;
        private const int MaxRecursionDepth = 256;

        internal REParseState(string pattern, RegExpFlags flags, CancellationToken cancellationToken)
        {
            _pattern = pattern;
            _pos = 0;
            _patternEnd = pattern.Length;
            _reFlags = flags;
            IsUnicode = (flags & (RegExpFlags.Unicode | RegExpFlags.UnicodeSets)) != RegExpFlags.None;
            UnicodeSets = (flags & RegExpFlags.UnicodeSets) != RegExpFlags.None;
            IgnoreCase = (flags & RegExpFlags.IgnoreCase) != RegExpFlags.None;
            MultiLine = (flags & RegExpFlags.Multiline) != RegExpFlags.None;
            DotAll = (flags & RegExpFlags.DotAll) != RegExpFlags.None;
            CaptureCount = 1;
            TotalCaptureCount = -1;
            HasNamedCaptures = -1;
            _cancellationToken = cancellationToken;
        }

        // Section: Compile entry point (port of lre_compile)

        internal byte[] Compile()
        {
            bool isSticky = (_reFlags & RegExpFlags.Sticky) != RegExpFlags.None;

            // Write 8-byte header placeholder
            EmitU16((ushort) _reFlags);   // offset 0: flags
            EmitByte(0);                  // offset 2: capture count (filled later)
            EmitByte(0);                  // offset 3: register count (filled later)
            EmitU32(0);                   // offset 4: bytecode length (filled later)

            if (!isSticky)
            {
                // Iterate through all positions (equivalent to .*?(...))
                // We do it without an explicit loop so that lock-step thread
                // execution will be possible in an optimized implementation.
                EmitOpU32(RegExpOpcode.SplitGotoFirst, 1 + 5);
                EmitOp(RegExpOpcode.Any);
                EmitOpU32(RegExpOpcode.Goto, unchecked((uint) (-(5 + 1 + 5))));
            }
            EmitOpU8(RegExpOpcode.SaveStart, 0);

            ParseDisjunction(false);

            EmitOpU8(RegExpOpcode.SaveEnd, 0);
            EmitOp(RegExpOpcode.Match);

            if (_pos != _patternEnd)
            {
                throw new RegExpSyntaxException("extraneous characters at the end");
            }

            int registerCount = ComputeRegisterCount();
            if (registerCount < 0)
            {
                throw new RegExpSyntaxException("too many imbricated quantifiers");
            }

            _byteCode[RegExpHeader.OffsetCaptureCount] = (byte) CaptureCount;
            _byteCode[RegExpHeader.OffsetRegisterCount] = (byte) registerCount;
            PutU32(RegExpHeader.OffsetBytecodeLen, (uint) (_byteCode.Count - RegExpHeader.Length));

            // Add the named groups if needed
            bool hasRealNames = false;
            foreach (var gn in GroupNames)
            {
                if (gn.Name is { Length: > 0 })
                {
                    hasRealNames = true;
                    break;
                }
            }

            if (hasRealNames)
            {
                SerializeGroupNames();
                ushort currentFlags = ReadU16(RegExpHeader.OffsetFlags);
                PutU16(RegExpHeader.OffsetFlags, (ushort) (currentFlags | (ushort) RegExpFlags.NamedGroups));
            }
            else
            {
                SerializeGroupNames();
            }

            return _byteCode.ToArray();
        }

        // Section: Bytecode emission helpers

        private void EmitByte(byte b)
        {
            _byteCode.Add(b);
        }

        private void EmitOp(RegExpOpcode op)
        {
            _byteCode.Add((byte) op);
        }

        /// <summary>Emit opcode + u32 value. Returns the offset of the u32.</summary>
        private int EmitOpU32(RegExpOpcode op, uint val)
        {
            _byteCode.Add((byte) op);
            int pos = _byteCode.Count;
            EmitU32(val);
            return pos;
        }

        /// <summary>Emit goto-style opcode with relative offset. Returns offset of the u32.</summary>
        private int EmitGoto(RegExpOpcode op, int target)
        {
            _byteCode.Add((byte) op);
            int pos = _byteCode.Count;
            int rel = target - (pos + 4);
            EmitU32((uint) rel);
            return pos;
        }

        /// <summary>Emit goto with a u8 arg then relative offset. Returns offset of the u32.</summary>
        private int EmitGotoU8(RegExpOpcode op, byte arg, int target)
        {
            _byteCode.Add((byte) op);
            _byteCode.Add(arg);
            int pos = _byteCode.Count;
            int rel = target - (pos + 4);
            EmitU32((uint) rel);
            return pos;
        }

        /// <summary>
        /// Emit goto with u8 arg, u32 arg, then relative offset.
        /// Returns offset of the u32 goto.
        /// </summary>
        private int EmitGotoU8U32(RegExpOpcode op, byte arg0, uint arg1, int target)
        {
            _byteCode.Add((byte) op);
            _byteCode.Add(arg0);
            EmitU32(arg1);
            int pos = _byteCode.Count;
            int rel = target - (pos + 4);
            EmitU32((uint) rel);
            return pos;
        }

        private void EmitOpU8(RegExpOpcode op, byte val)
        {
            _byteCode.Add((byte) op);
            _byteCode.Add(val);
        }

        private void EmitOpU16(RegExpOpcode op, ushort val)
        {
            _byteCode.Add((byte) op);
            EmitU16(val);
        }

        /// <summary>
        /// Emit a character match opcode (Char/Char32, with or without case-insensitive variant).
        /// Port of re_emit_char.
        /// </summary>
        private void EmitChar(uint c)
        {
            if (c <= 0xFFFF)
                EmitOpU16(IgnoreCase ? RegExpOpcode.CharI : RegExpOpcode.Char, (ushort) c);
            else
                EmitOpU32(IgnoreCase ? RegExpOpcode.Char32I : RegExpOpcode.Char32, c);
        }

        private void EmitU16(ushort val)
        {
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buf, val);
            _byteCode.Add(buf[0]);
            _byteCode.Add(buf[1]);
        }

        private void EmitU32(uint val)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, val);
            _byteCode.Add(buf[0]);
            _byteCode.Add(buf[1]);
            _byteCode.Add(buf[2]);
            _byteCode.Add(buf[3]);
        }

        private void PutU32(int offset, uint val)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, val);
            _byteCode[offset] = buf[0];
            _byteCode[offset + 1] = buf[1];
            _byteCode[offset + 2] = buf[2];
            _byteCode[offset + 3] = buf[3];
        }

        private uint ReadU32(int offset)
        {
            Span<byte> buf =
            [
                _byteCode[offset], _byteCode[offset + 1],
                _byteCode[offset + 2], _byteCode[offset + 3]
            ];
            return BinaryPrimitives.ReadUInt32LittleEndian(buf);
        }

        private void PutU16(int offset, ushort val)
        {
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buf, val);
            _byteCode[offset] = buf[0];
            _byteCode[offset + 1] = buf[1];
        }

        private ushort ReadU16(int offset)
        {
            Span<byte> buf = [_byteCode[offset], _byteCode[offset + 1]];
            return BinaryPrimitives.ReadUInt16LittleEndian(buf);
        }

        /// <summary>Insert 'len' zero bytes at 'pos'. Port of dbuf_insert.</summary>
        private void ByteCodeInsert(int pos, int len)
        {
            // Insert zeroed bytes at the given position
            for (int i = 0; i < len; i++)
            {
                _byteCode.Insert(pos, 0);
            }
        }

        // Section: Pattern input helpers

        /// <summary>Read one code point, handling surrogates in unicode mode.</summary>
        private int ReadChar()
        {
            if (_pos >= _patternEnd) return -1;
            char c = _pattern[_pos++];
            if (IsUnicode && char.IsHighSurrogate(c) &&
                _pos < _patternEnd && char.IsLowSurrogate(_pattern[_pos]))
            {
                return char.ConvertToUtf32(c, _pattern[_pos++]);
            }
            return c;
        }

        private void CheckStackOverflow()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (++_recursionDepth > MaxRecursionDepth)
            {
                throw new RegExpSyntaxException("stack overflow");
            }
        }

        private void DecrementRecursionDepth()
        {
            _recursionDepth--;
        }

        // Section: Digit parsing (port of parse_digits)

        /// <summary>
        /// Parse decimal digits starting at current position.
        /// If allowOverflow, clamps to int.MaxValue; otherwise returns -1 on overflow.
        /// </summary>
        private int ParseDigits(bool allowOverflow)
        {
            long v = 0;
            while (_pos < _patternEnd)
            {
                int c = _pattern[_pos];
                if (c < '0' || c > '9') break;
                v = v * 10 + (c - '0');
                if (v >= int.MaxValue)
                {
                    if (allowOverflow)
                        v = int.MaxValue;
                    else
                        return -1;
                }
                _pos++;
            }
            return (int) v;
        }

        /// <summary>
        /// Parse decimal digits at a given position (does not modify _pos).
        /// </summary>
        private static int ParseDigitsAt(string pattern, ref int pos, int end, bool allowOverflow)
        {
            long v = 0;
            while (pos < end)
            {
                int c = pattern[pos];
                if (c < '0' || c > '9') break;
                v = v * 10 + (c - '0');
                if (v >= int.MaxValue)
                {
                    if (allowOverflow)
                        v = int.MaxValue;
                    else
                        return -1;
                }
                pos++;
            }
            return (int) v;
        }

        // Section: Expect character (port of re_parse_expect)

        private void ParseExpect(char expected)
        {
            if (_pos >= _patternEnd || _pattern[_pos] != expected)
            {
                throw new RegExpSyntaxException($"expecting '{expected}'");
            }
            _pos++;
        }

        // Section: Hex parsing (port of from_hex)

        private static int FromHex(int c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return -1;
        }

        // Section: Surrogate helpers

        private static bool IsHighSurrogate(uint c) => (c >> 10) == (0xD800 >> 10);
        private static bool IsLowSurrogate(uint c) => (c >> 10) == (0xDC00 >> 10);
        private static uint FromSurrogatePair(uint hi, uint lo) => 0x10000 + 0x400 * (hi - 0xD800) + (lo - 0xDC00);

        // Section: Escape sequence parsing (port of lre_parse_escape)

        /// <summary>
        /// Parse an escape sequence. _pos points after the '\'.
        /// allowUtf16: 0=no UTF-16, 1=UTF-16 allowed, 2=UTF-16 + surrogate pair conversion.
        /// Returns the character, -1 (malformed), or -2 (unrecognized).
        /// </summary>
        private int ParseEscape(int allowUtf16)
        {
            if (_pos >= _patternEnd) return -1;
            int c = _pattern[_pos++];

            switch (c)
            {
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return 0x0B;

                case 'x':
                    {
                        if (_pos >= _patternEnd) return -1;
                        int h0 = FromHex(_pattern[_pos++]);
                        if (h0 < 0) return -1;
                        if (_pos >= _patternEnd) return -1;
                        int h1 = FromHex(_pattern[_pos++]);
                        if (h1 < 0) return -1;
                        return (h0 << 4) | h1;
                    }

                case 'u':
                    {
                        if (_pos < _patternEnd && _pattern[_pos] == '{' && allowUtf16 != 0)
                        {
                            _pos++; // skip '{'
                            uint cv = 0;
                            bool hasDigit = false;
                            for (; ; )
                            {
                                if (_pos >= _patternEnd) return -1;
                                int h = FromHex(_pattern[_pos]);
                                if (h < 0)
                                {
                                    if (_pattern[_pos] == '}' && hasDigit) break;
                                    return -1;
                                }
                                _pos++;
                                hasDigit = true;
                                cv = (cv << 4) | (uint) h;
                                if (cv > 0x10FFFF) return -1;
                            }
                            if (_pos >= _patternEnd || _pattern[_pos] != '}') return -1;
                            _pos++; // skip '}'
                            return (int) cv;
                        }
                        else
                        {
                            uint cv = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                if (_pos >= _patternEnd) return -1;
                                int h = FromHex(_pattern[_pos++]);
                                if (h < 0) return -1;
                                cv = (cv << 4) | (uint) h;
                            }

                            // Check for surrogate pair in unicode mode
                            if (IsHighSurrogate(cv) && allowUtf16 == 2 &&
                                _pos + 1 < _patternEnd &&
                                _pattern[_pos] == '\\' && _pattern[_pos + 1] == 'u')
                            {
                                int savedPos = _pos;
                                _pos += 2; // skip \u
                                uint c1 = 0;
                                bool ok = true;
                                for (int i = 0; i < 4; i++)
                                {
                                    if (_pos >= _patternEnd) { ok = false; break; }
                                    int h = FromHex(_pattern[_pos++]);
                                    if (h < 0) { ok = false; break; }
                                    c1 = (c1 << 4) | (uint) h;
                                }
                                if (ok && IsLowSurrogate(c1))
                                {
                                    return (int) FromSurrogatePair(cv, c1);
                                }
                                _pos = savedPos;
                            }
                            return (int) cv;
                        }
                    }

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                    {
                        uint cv = (uint) (c - '0');
                        if (allowUtf16 == 2)
                        {
                            // Only accept \0 not followed by a digit
                            if (cv != 0 || (_pos < _patternEnd && IsAsciiDigit(_pattern[_pos])))
                                return -1;
                        }
                        else
                        {
                            // Parse legacy octal
                            if (_pos < _patternEnd)
                            {
                                uint v = (uint) (_pattern[_pos] - '0');
                                if (v <= 7)
                                {
                                    cv = (cv << 3) | v;
                                    _pos++;
                                    if (cv < 32 && _pos < _patternEnd)
                                    {
                                        v = (uint) (_pattern[_pos] - '0');
                                        if (v <= 7)
                                        {
                                            cv = (cv << 3) | v;
                                            _pos++;
                                        }
                                    }
                                }
                            }
                        }
                        return (int) cv;
                    }

                default:
                    return -2;
            }
        }

        // Section: Unicode property char check

        private static bool IsUnicodePropertyChar(int c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   c == '_';
        }

        // Section: Unicode property parsing (port of parse_unicode_property)

        /// <summary>
        /// Parse \p{...} or \P{...} unicode property escape.
        /// </summary>
        private void ParseUnicodeProperty(REStringList cr, bool isInv, bool allowSequenceProp)
        {
            if (_pos >= _patternEnd || _pattern[_pos] != '{')
                throw new RegExpSyntaxException("expecting '{' after \\p");
            _pos++; // skip '{'

            // Parse property name
            int nameStart = _pos;
            while (_pos < _patternEnd && IsUnicodePropertyChar(_pattern[_pos]))
                _pos++;
            string name = _pattern.Substring(nameStart, _pos - nameStart);

            // Parse optional value after '='
            string value = "";
            if (_pos < _patternEnd && _pattern[_pos] == '=')
            {
                _pos++;
                int valStart = _pos;
                while (_pos < _patternEnd && IsUnicodePropertyChar(_pattern[_pos]))
                    _pos++;
                value = _pattern.Substring(valStart, _pos - valStart);
            }

            if (_pos >= _patternEnd || _pattern[_pos] != '}')
                throw new RegExpSyntaxException("expecting '}'");
            _pos++; // skip '}'

            int ret;
            if (name is "Script" or "sc")
            {
                ret = UnicodeProperties.UnicodeScript(cr.Cr.Points, value, false);
                if (ret != 0)
                {
                    if (ret == -2) throw new RegExpSyntaxException("unknown unicode script");
                    throw new RegExpSyntaxException("out of memory");
                }
            }
            else if (name is "Script_Extensions" or "scx")
            {
                ret = UnicodeProperties.UnicodeScript(cr.Cr.Points, value, true);
                if (ret != 0)
                {
                    if (ret == -2) throw new RegExpSyntaxException("unknown unicode script");
                    throw new RegExpSyntaxException("out of memory");
                }
            }
            else if (name is "General_Category" or "gc")
            {
                ret = UnicodeProperties.UnicodeGeneralCategory(cr.Cr.Points, value);
                if (ret != 0)
                {
                    if (ret == -2) throw new RegExpSyntaxException("unknown unicode general category");
                    throw new RegExpSyntaxException("out of memory");
                }
            }
            else if (value.Length == 0)
            {
                // Try general category first, then binary property, then sequence property
                ret = UnicodeProperties.UnicodeGeneralCategory(cr.Cr.Points, name);
                if (ret == -1) throw new RegExpSyntaxException("out of memory");
                if (ret < 0)
                {
                    ret = UnicodeProperties.UnicodeProp(cr.Cr.Points, name);
                    if (ret == -1) throw new RegExpSyntaxException("out of memory");
                }
                if (ret < 0 && !isInv && allowSequenceProp)
                {
                    // Use a separate temp list for SeqProp1's internal work.
                    // SeqProp1 uses the list as a scratch buffer (UnicodeProp1 + Clear),
                    // so it must NOT be cr.Cr.Points which accumulates our output.
                    var tempBuf = new List<uint>();
                    ret = UnicodeProperties.UnicodeSequenceProp(name, (seq, len) =>
                    {
                        cr.Add(seq.AsSpan(0, len).ToArray());
                    }, tempBuf);
                    if (ret == -1) throw new RegExpSyntaxException("out of memory");
                }
                if (ret < 0) throw new RegExpSyntaxException("unknown unicode property name");
            }
            else
            {
                throw new RegExpSyntaxException("unknown unicode property name");
            }

            // The ordering of case folding and inversion differs with unicode_sets.
            if (IgnoreCase && UnicodeSets)
                cr.Canonicalize(IsUnicode);
            if (isInv)
                cr.Cr.Invert();
            if (IgnoreCase && !UnicodeSets)
                cr.Canonicalize(IsUnicode);
        }

        // Section: Class string disjunction (\q{...}) (port of parse_class_string_disjunction)

        private void ParseClassStringDisjunction(REStringList cr)
        {
            if (_pos >= _patternEnd || _pattern[_pos] != '{')
                throw new RegExpSyntaxException("expecting '{' after \\q");
            _pos++; // skip '{'

            var codePoints = new List<uint>();

            for (; ; )
            {
                codePoints.Clear();
                while (_pos < _patternEnd && _pattern[_pos] != '}' && _pattern[_pos] != '|')
                {
                    int c = GetClassAtom(null, true);
                    if (c < 0) throw new RegExpSyntaxException("unexpected end");
                    codePoints.Add((uint) c);
                }
                cr.Add(codePoints.ToArray());

                if (_pos < _patternEnd && _pattern[_pos] == '}') break;
                _pos++; // skip '|'
            }

            if (IgnoreCase) cr.Canonicalize(true);
            _pos++; // skip '}'
        }

        // Section: Init built-in char range (port of cr_init_char_range)

        private static void InitCharRange(REStringList cr, uint c)
        {
            bool invert = (c & 1) != 0;
            ReadOnlySpan<ushort> table = (c >> 1) switch
            {
                0 => CharRangeD,
                1 => CharRangeS,
                2 => CharRangeW,
                _ => throw new InvalidOperationException("Invalid char range class"),
            };

            int len = table[0];
            cr.Cr.Clear();
            for (int i = 0; i < len * 2; i++)
            {
                cr.Cr.AddPoint(table[1 + i]);
            }
            if (invert) cr.Cr.Invert();
        }

        // Section: Class atom parsing (port of get_class_atom)

        /// <summary>
        /// Parse a single atom inside a character class or as a standalone atom.
        /// Returns the code point value, or CLASS_RANGE_BASE + enum for built-in classes, or -1 on error.
        /// If cr is non-null, class ranges (like \d, \p{...}) populate it.
        /// </summary>
        private int GetClassAtom(REStringList? cr, bool inclass)
        {
            if (_pos >= _patternEnd)
                throw new RegExpSyntaxException("unexpected end");

            int c = _pattern[_pos];

            switch (c)
            {
                case '\\':
                    {
                        _pos++;
                        if (_pos >= _patternEnd)
                            throw new RegExpSyntaxException("unexpected end");
                        c = _pattern[_pos++];
                        switch (c)
                        {
                            case 'd':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.Digit);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.Digit);
                            case 'D':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.NotDigit);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.NotDigit);
                            case 's':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.Space);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.Space);
                            case 'S':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.NotSpace);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.NotSpace);
                            case 'w':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.Word);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.Word);
                            case 'W':
                                if (cr == null) goto default_escape;
                                InitCharRange(cr, (uint) CharRangeEnum.NotWord);
                                return (int) (ClassRangeBase + (uint) CharRangeEnum.NotWord);

                            case 'c':
                                {
                                    if (_pos >= _patternEnd)
                                    {
                                        if (IsUnicode)
                                            throw new RegExpSyntaxException("invalid escape sequence in regular expression");
                                        _pos--;
                                        return '\\';
                                    }
                                    int cc = _pattern[_pos];
                                    if ((cc >= 'a' && cc <= 'z') ||
                                        (cc >= 'A' && cc <= 'Z') ||
                                        (((cc >= '0' && cc <= '9') || cc == '_') &&
                                         inclass && !IsUnicode))   // Annex B.1.4
                                    {
                                        c = cc & 0x1F;
                                        _pos++;
                                    }
                                    else if (IsUnicode)
                                    {
                                        throw new RegExpSyntaxException("invalid escape sequence in regular expression");
                                    }
                                    else
                                    {
                                        // Return '\' and 'c'
                                        _pos--;
                                        c = '\\';
                                    }
                                    break;
                                }

                            case '-':
                                if (!inclass && IsUnicode)
                                    throw new RegExpSyntaxException("invalid escape sequence in regular expression");
                                break;

                            case '^':
                            case '$':
                            case '\\':
                            case '.':
                            case '*':
                            case '+':
                            case '?':
                            case '(':
                            case ')':
                            case '[':
                            case ']':
                            case '{':
                            case '}':
                            case '|':
                            case '/':
                                // Always valid to escape these characters
                                break;

                            case 'p':
                            case 'P':
                                if (IsUnicode && cr != null)
                                {
                                    ParseUnicodeProperty(cr, c == 'P', UnicodeSets);
                                    return (int) ClassRangeBase;
                                }
                                goto default_escape;

                            case 'q':
                                if (UnicodeSets && cr != null && inclass)
                                {
                                    ParseClassStringDisjunction(cr);
                                    return (int) ClassRangeBase;
                                }
                                goto default_escape;

                            default:
default_escape:
                                {
                                    _pos--; // back up to char after '\'
                                    int savedPos = _pos;
                                    int ret = ParseEscape(IsUnicode ? 2 : 0);
                                    if (ret >= 0)
                                    {
                                        return ret;
                                    }
                                    else
                                    {
                                        _pos = savedPos;
                                        if (IsUnicode)
                                        {
                                            throw new RegExpSyntaxException("invalid escape sequence in regular expression");
                                        }
                                        // Non-unicode: just ignore the '\'
                                        c = _pattern[_pos];
                                        _pos++;
                                        goto normal_char_return;
                                    }
                                }
                        }
                        break;
                    }

                case '&':
                case '!':
                case '#':
                case '$':
                case '%':
                case '*':
                case '+':
                case ',':
                case '.':
                case ':':
                case ';':
                case '<':
                case '=':
                case '>':
                case '?':
                case '@':
                case '^':
                case '`':
                case '~':
                    if (UnicodeSets && _pos + 1 < _patternEnd && _pattern[_pos + 1] == c)
                    {
                        // Forbidden double characters in unicode-sets mode
                        throw new RegExpSyntaxException("invalid class set operation in regular expression");
                    }
                    goto default;

                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case '/':
                case '-':
                case '|':
                    if (UnicodeSets)
                    {
                        throw new RegExpSyntaxException("invalid character in class in regular expression");
                    }
                    goto default;

                default:
                    {
                        // Normal char: read one code point
                        char ch = _pattern[_pos];
                        if (char.IsHighSurrogate(ch) && _pos + 1 < _patternEnd && char.IsLowSurrogate(_pattern[_pos + 1]))
                        {
                            c = char.ConvertToUtf32(ch, _pattern[_pos + 1]);
                            _pos += 2;
                            if (c > 0xFFFF && !IsUnicode)
                            {
                                throw new RegExpSyntaxException("malformed unicode char");
                            }
                        }
                        else
                        {
                            c = ch;
                            _pos++;
                        }
                        break;
                    }
            }
normal_char_return:
            return c;
        }

        // Section: Emit character range (port of re_emit_range)

        private void EmitRange(CharRange cr)
        {
            int len = cr.Length / 2;
            if (len >= 65535)
                throw new RegExpSyntaxException("too many ranges");
            if (len == 0)
            {
                // Empty range: emit a char that can never match
                EmitOpU32(RegExpOpcode.Char32, 0xFFFFFFFF);
            }
            else
            {
                uint high = cr.Points[cr.Length - 1];
                if (high == uint.MaxValue)
                    high = cr.Points[cr.Length - 2];

                if (high <= 0xFFFF)
                {
                    // Use 16-bit ranges (0xFFFF = infinity convention)
                    EmitOpU16(IgnoreCase ? RegExpOpcode.RangeI : RegExpOpcode.Range, (ushort) len);
                    for (int i = 0; i < cr.Length; i += 2)
                    {
                        EmitU16((ushort) cr.Points[i]);
                        uint h = cr.Points[i + 1] - 1;
                        if (h == uint.MaxValue - 1) h = 0xFFFF;
                        EmitU16((ushort) h);
                    }
                }
                else
                {
                    // Use 32-bit ranges
                    EmitOpU16(IgnoreCase ? RegExpOpcode.Range32I : RegExpOpcode.Range32, (ushort) len);
                    for (int i = 0; i < cr.Length; i += 2)
                    {
                        EmitU32(cr.Points[i]);
                        EmitU32(cr.Points[i + 1] - 1);
                    }
                }
            }
        }

        // Section: Emit string list (port of re_emit_string_list)

        /// <summary>
        /// Emit bytecode for matching a REStringList (char ranges + multi-char strings).
        /// For v-flag (unicode-sets) character classes.
        /// </summary>
        private void EmitStringList(REStringList sl)
        {
            if (sl.StringCount == 0)
            {
                // Simple case: only character ranges
                EmitRange(sl.Cr);
            }
            else
            {
                // At least one multi-char string present: match the longest ones first
                var strings = sl.GetStringsSortedByLengthDesc();
                bool hasEmptyString = sl.HasEmptyString();
                int n = strings.Count;

                int lastMatchPos = -1;
                for (int i = 0; i < n; i++)
                {
                    var str = strings[i];
                    bool isLast = !hasEmptyString && sl.Cr.Length == 0 && i == (n - 1);

                    int splitPos = 0;
                    if (!isLast)
                        splitPos = EmitOpU32(RegExpOpcode.SplitNextFirst, 0);

                    foreach (uint cp in str)
                        EmitChar(cp);

                    if (!isLast)
                    {
                        lastMatchPos = EmitOpU32(RegExpOpcode.Goto, (uint) lastMatchPos);
                        PutU32(splitPos, (uint) (_byteCode.Count - (splitPos + 4)));
                    }
                }

                if (sl.Cr.Length != 0)
                {
                    bool isLast = !hasEmptyString;
                    int splitPos = 0;
                    if (!isLast)
                        splitPos = EmitOpU32(RegExpOpcode.SplitNextFirst, 0);
                    EmitRange(sl.Cr);
                    if (!isLast)
                        PutU32(splitPos, (uint) (_byteCode.Count - (splitPos + 4)));
                }

                // Patch the 'goto match' chain
                while (lastMatchPos != -1)
                {
                    int nextPos = (int) ReadU32(lastMatchPos);
                    PutU32(lastMatchPos, (uint) (_byteCode.Count - (lastMatchPos + 4)));
                    lastMatchPos = nextPos;
                }
            }
        }

        // Section: Nested class parsing (port of re_parse_nested_class)

        /// <summary>
        /// Parse a class set operand for unicode-sets (v-flag) operations.
        /// Port of re_parse_class_set_operand.
        /// </summary>
        private void ParseClassSetOperand(REStringList cr)
        {
            if (_pos < _patternEnd && _pattern[_pos] == '[')
            {
                ParseNestedClass(cr);
            }
            else
            {
                var tmpCr = new REStringList();
                int c1 = GetClassAtom(tmpCr, true);
                if (c1 < 0)
                    throw new RegExpSyntaxException("unexpected end");
                if (c1 < (int) ClassRangeBase)
                {
                    // Single character: create a range
                    cr.Cr.Clear();
                    if (IgnoreCase)
                        c1 = (int) UnicodeProperties.Canonicalize((uint) c1, IsUnicode);
                    cr.Cr.UnionInterval((uint) c1, (uint) c1);
                }
                else
                {
                    // Class range result: copy over
                    cr.Cr.CopyFrom(tmpCr.Cr);
                    // Also copy any multi-char strings
                    cr.Op(tmpCr, CharRangeOp.Union);
                }
            }
        }

        /// <summary>
        /// Parse a (possibly nested) character class [...].
        /// Handles union, intersection (ampersand-ampersand), and subtraction (--) for unicode-sets.
        /// Port of re_parse_nested_class.
        /// </summary>
        private void ParseNestedClass(REStringList cr)
        {
            CheckStackOverflow();
            try
            {
                cr.Clear();
                _pos++; // skip '['

                bool invert = false;
                if (_pos < _patternEnd && _pattern[_pos] == '^')
                {
                    _pos++;
                    invert = true;
                }

                bool isFirst = true;
                while (_pos < _patternEnd && _pattern[_pos] != ']')
                {
                    var cr1 = new REStringList();
                    int c1;
                    bool rangeHandled = false;

                    if (_pos < _patternEnd && _pattern[_pos] == '[' && UnicodeSets)
                    {
                        ParseNestedClass(cr1);
                        cr.Op(cr1, CharRangeOp.Union);
                        goto after_class_handling;
                    }

                    c1 = GetClassAtom(cr1, true);
                    if (c1 < 0)
                        throw new RegExpSyntaxException("unexpected end");

                    // Check for range (c1-c2)
                    if (_pos < _patternEnd && _pattern[_pos] == '-' &&
                        (_pos + 1 >= _patternEnd || _pattern[_pos + 1] != ']'))
                    {
                        bool fallToClassAtom = false;

                        // Check for '--' in unicode_sets mode with first operand
                        if (_pos + 1 < _patternEnd && _pattern[_pos + 1] == '-' && UnicodeSets && isFirst)
                        {
                            fallToClassAtom = true;
                        }
                        else if (c1 >= (int) ClassRangeBase)
                        {
                            if (IsUnicode)
                                throw new RegExpSyntaxException("invalid class range");
                            // Annex B: match '-' character
                            fallToClassAtom = true;
                        }

                        if (!fallToClassAtom)
                        {
                            int savedPos = _pos;
                            _pos++; // skip '-'
                            var cr1b = new REStringList();
                            int c2 = GetClassAtom(cr1b, true);
                            if (c2 < 0)
                                throw new RegExpSyntaxException("unexpected end");
                            if (c2 >= (int) ClassRangeBase)
                            {
                                if (IsUnicode)
                                    throw new RegExpSyntaxException("invalid class range");
                                _pos = savedPos;
                                fallToClassAtom = true;
                            }

                            if (!fallToClassAtom)
                            {
                                if (c2 < c1)
                                    throw new RegExpSyntaxException("invalid class range");

                                if (IgnoreCase)
                                {
                                    var cr2 = new CharRange();
                                    cr2.AddInterval((uint) c1, (uint) c2 + 1);
                                    cr2.RegexpCanonicalize(IsUnicode);
                                    cr.Cr.Op1(cr2, CharRangeOp.Union);
                                }
                                else
                                {
                                    cr.Cr.UnionInterval((uint) c1, (uint) c2);
                                }
                                rangeHandled = true;
                                isFirst = false;
                            }
                        }

                        if (fallToClassAtom)
                        {
                            // Emit class atom: union the single char into cr
                            if (c1 >= 0 && c1 < (int) ClassRangeBase)
                            {
                                if (IgnoreCase)
                                {
                                    var crTmp = new CharRange();
                                    crTmp.AddInterval((uint) c1, (uint) c1 + 1);
                                    crTmp.RegexpCanonicalize(IsUnicode);
                                    cr.Cr.Op1(crTmp, CharRangeOp.Union);
                                }
                                else
                                {
                                    cr.Cr.UnionInterval((uint) c1, (uint) c1);
                                }
                            }
                            else
                            {
                                // Multi-char class or special class - union ranges AND strings
                                cr.Op(cr1, CharRangeOp.Union);
                            }
                        }
                    }

                    // If no range was handled, union the single class atom into cr
                    if (!rangeHandled)
                    {
                        if (c1 >= 0 && c1 < (int) ClassRangeBase)
                        {
                            if (IgnoreCase)
                            {
                                var crTmp = new CharRange();
                                crTmp.AddInterval((uint) c1, (uint) c1 + 1);
                                crTmp.RegexpCanonicalize(IsUnicode);
                                cr.Cr.Op1(crTmp, CharRangeOp.Union);
                            }
                            else
                            {
                                cr.Cr.UnionInterval((uint) c1, (uint) c1);
                            }
                        }
                        else
                        {
                            // Property escape, \d, \w, \q{}, \p{} etc. - union ranges AND strings
                            cr.Op(cr1, CharRangeOp.Union);
                        }
                    }

after_class_handling:
// Handle unicode-sets operators
                    if (UnicodeSets && isFirst)
                    {
                        if (_pos + 1 < _patternEnd && _pattern[_pos] == '&' &&
                            _pattern[_pos + 1] == '&' &&
                            (_pos + 2 >= _patternEnd || _pattern[_pos + 2] != '&'))
                        {
                            // Handle '&&' intersection
                            for (; ; )
                            {
                                if (_pos >= _patternEnd || _pattern[_pos] == ']') break;
                                if (_pattern[_pos] == '&' && _pos + 1 < _patternEnd &&
                                    _pattern[_pos + 1] == '&' &&
                                    (_pos + 2 >= _patternEnd || _pattern[_pos + 2] != '&'))
                                {
                                    _pos += 2;
                                }
                                else
                                {
                                    throw new RegExpSyntaxException("invalid operation in regular expression");
                                }
                                var operand = new REStringList();
                                ParseClassSetOperand(operand);
                                cr.Op(operand, CharRangeOp.Inter);
                            }
                        }
                        else if (_pos + 1 < _patternEnd && _pattern[_pos] == '-' &&
                                 _pattern[_pos + 1] == '-')
                        {
                            // Handle '--' subtraction
                            for (; ; )
                            {
                                if (_pos >= _patternEnd || _pattern[_pos] == ']') break;
                                if (_pattern[_pos] == '-' && _pos + 1 < _patternEnd &&
                                    _pattern[_pos + 1] == '-')
                                {
                                    _pos += 2;
                                }
                                else
                                {
                                    throw new RegExpSyntaxException("invalid operation in regular expression");
                                }
                                var operand = new REStringList();
                                ParseClassSetOperand(operand);
                                cr.Op(operand, CharRangeOp.Sub);
                            }
                        }
                    }
                    isFirst = false;
                }

                if (_pos >= _patternEnd)
                    throw new RegExpSyntaxException("unterminated character class");
                _pos++; // skip ']'

                if (invert)
                {
                    if (cr.StringCount != 0)
                    {
                        throw new RegExpSyntaxException("negated character class with strings in regular expression");
                    }
                    cr.Cr.Invert();
                }
            }
            finally
            {
                DecrementRecursionDepth();
            }
        }

        // Section: Character class entry point (port of re_parse_char_class)

        private void ParseCharClass()
        {
            var cr = new REStringList();
            ParseNestedClass(cr);
            EmitStringList(cr);
        }

        // Section: Check advance and capture init (port of re_need_check_adv_and_capture_init)

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct NeedCheckResult(bool NeedCheckAdv, bool NeedCaptureInit);

        /// <summary>
        /// Determine if the bytecode atom always advances the character pointer
        /// and whether captures need initialization.
        /// </summary>
        private NeedCheckResult NeedCheckAdvAndCaptureInit(int start, int end)
        {
            bool needCheckAdv = true;
            bool needCaptureInit = false;
            int pos = start;

            while (pos < end)
            {
                var opcode = (RegExpOpcode) _byteCode[pos];
                int len = RegExpOpcodeInfo.GetSize(opcode);

                switch (opcode)
                {
                    case RegExpOpcode.Range:
                    case RegExpOpcode.RangeI:
                        len += ReadU16(pos + 1) * 4;
                        needCheckAdv = false;
                        break;
                    case RegExpOpcode.Range32:
                    case RegExpOpcode.Range32I:
                        len += ReadU16(pos + 1) * 8;
                        needCheckAdv = false;
                        break;
                    case RegExpOpcode.Char:
                    case RegExpOpcode.CharI:
                    case RegExpOpcode.Char32:
                    case RegExpOpcode.Char32I:
                    case RegExpOpcode.Dot:
                    case RegExpOpcode.Any:
                    case RegExpOpcode.Space:
                    case RegExpOpcode.NotSpace:
                        needCheckAdv = false;
                        break;
                    case RegExpOpcode.LineStart:
                    case RegExpOpcode.LineStartM:
                    case RegExpOpcode.LineEnd:
                    case RegExpOpcode.LineEndM:
                    case RegExpOpcode.SetI32:
                    case RegExpOpcode.SetCharPos:
                    case RegExpOpcode.WordBoundary:
                    case RegExpOpcode.WordBoundaryI:
                    case RegExpOpcode.NotWordBoundary:
                    case RegExpOpcode.NotWordBoundaryI:
                    case RegExpOpcode.Prev:
                    case RegExpOpcode.SaveStart:
                    case RegExpOpcode.SaveEnd:
                    case RegExpOpcode.SaveReset:
                        break;
                    case RegExpOpcode.BackReference:
                    case RegExpOpcode.BackReferenceI:
                    case RegExpOpcode.BackwardBackReference:
                    case RegExpOpcode.BackwardBackReferenceI:
                        len += _byteCode[pos + 1];
                        needCaptureInit = true;
                        break;
                    default:
                        needCaptureInit = true;
                        goto done;
                }
                pos += len;
            }
done:
            return new NeedCheckResult(needCheckAdv, needCaptureInit);
        }

        // Section: Group name parsing (port of re_parse_group_name)

        /// <summary>
        /// Parse a group name. _pos points to the first char after '&lt;'.
        /// Returns the group name string, or throws RegExpSyntaxException.
        /// </summary>
        private string ParseGroupName()
        {
            var sb = new System.Text.StringBuilder();
            bool isFirst = true;

            while (_pos < _patternEnd)
            {
                int c;
                if (_pattern[_pos] == '\\')
                {
                    _pos++;
                    if (_pos >= _patternEnd || _pattern[_pos] != 'u')
                        throw new RegExpSyntaxException("invalid group name");
                    c = ParseEscape(2); // Accept surrogate pairs
                    if (c < 0)
                        throw new RegExpSyntaxException("invalid group name");
                }
                else if (_pattern[_pos] == '>')
                {
                    break;
                }
                else
                {
                    c = ReadGroupNameCodePoint();
                }

                if (c > 0x10FFFF)
                    throw new RegExpSyntaxException("invalid group name");

                if (isFirst)
                {
                    if (!IsIdentFirst(c))
                        throw new RegExpSyntaxException("invalid group name");
                    isFirst = false;
                }
                else
                {
                    if (!IsIdentNext(c))
                        throw new RegExpSyntaxException("invalid group name");
                }

                if (c <= 0xFFFF)
                    sb.Append((char) c);
                else
                    sb.Append(char.ConvertFromUtf32(c));
            }

            if (sb.Length == 0)
                throw new RegExpSyntaxException("invalid group name");
            if (_pos >= _patternEnd || _pattern[_pos] != '>')
                throw new RegExpSyntaxException("invalid group name");
            _pos++; // skip '>'

            return sb.ToString();
        }

        private int ReadGroupNameCodePoint()
        {
            if (_pos >= _patternEnd) return -1;
            char ch = _pattern[_pos++];
            if (char.IsHighSurrogate(ch) && _pos < _patternEnd && char.IsLowSurrogate(_pattern[_pos]))
            {
                return char.ConvertToUtf32(ch, _pattern[_pos++]);
            }
            return ch;
        }

        // Section: Identifier character checks (port of lre_js_is_ident_first / lre_js_is_ident_next)

        private static bool IsIdentFirst(int c)
        {
            if (c < 128)
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '$';
            }
            // Use .NET Unicode category for non-ASCII
            if (c > 0xFFFF)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(c), 0);
                return cat is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                    or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                    or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
            }
            return CharUnicodeInfo.GetUnicodeCategory((char) c) is
                UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
        }

        private static bool IsIdentNext(int c)
        {
            if (c < 128)
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                       (c >= '0' && c <= '9') || c == '_' || c == '$';
            }
            // ZWNJ and ZWJ are accepted in identifiers
            if (c is >= 0x200C and <= 0x200D) return true;
            if (c > 0xFFFF)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(c), 0);
                return cat is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                    or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                    or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber
                    or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
            }
            return CharUnicodeInfo.GetUnicodeCategory((char) c) is
                UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber
                or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
        }

        // Section: Capture counting (port of re_parse_captures)

        /// <summary>
        /// If captureName is null: return the total number of captures + 1.
        /// Otherwise, return the number of groups matching that name.
        /// If emitGroupIndex, emit the matching capture indexes into bytecode.
        /// </summary>
        private int ParseCaptures(out int hasNamedCaptures, string? captureName, bool emitGroupIndex)
        {
            int captureIndex = 1;
            int n = 0;
            hasNamedCaptures = 0;

            int p = 0;
            while (p < _patternEnd)
            {
                char ch = _pattern[p];
                switch (ch)
                {
                    case '(':
                        if (p + 1 < _patternEnd && _pattern[p + 1] == '?')
                        {
                            if (p + 3 < _patternEnd && _pattern[p + 2] == '<' &&
                                _pattern[p + 3] != '=' && _pattern[p + 3] != '!')
                            {
                                hasNamedCaptures = 1;
                                if (captureName != null)
                                {
                                    // Find the end of the group name
                                    int nameStart = p + 3;
                                    int nameEnd = nameStart;
                                    while (nameEnd < _patternEnd && _pattern[nameEnd] != '>')
                                    {
                                        if (_pattern[nameEnd] == '\\') nameEnd++;
                                        nameEnd++;
                                    }
                                    if (nameEnd < _patternEnd)
                                    {
                                        int savedPos = _pos;
                                        _pos = nameStart;
                                        try
                                        {
                                            string name = ParseGroupName();
                                            if (string.Equals(name, captureName, StringComparison.Ordinal))
                                            {
                                                if (emitGroupIndex)
                                                    _byteCode.Add((byte) captureIndex);
                                                n++;
                                            }
                                        }
                                        catch
                                        {
                                            // Ignore parse errors during capture counting
                                        }
                                        _pos = savedPos;
                                        p = nameEnd + 1;
                                    }
                                }
                                captureIndex++;
                                if (captureIndex >= CaptureCountMax) goto done;
                            }
                            // Non-capturing groups like (?:...) don't increment
                        }
                        else
                        {
                            captureIndex++;
                            if (captureIndex >= CaptureCountMax) goto done;
                        }
                        break;

                    case '\\':
                        p++;
                        break;

                    case '[':
                        // Skip to end of character class
                        p++;
                        if (p < _patternEnd && _pattern[p] == ']') p++;
                        while (p < _patternEnd && _pattern[p] != ']')
                        {
                            if (_pattern[p] == '\\') p++;
                            p++;
                        }
                        break;
                }
                p++;
            }
done:
            return captureName != null ? n : captureIndex;
        }

        private int CountCaptures()
        {
            if (TotalCaptureCount < 0)
            {
                TotalCaptureCount = ParseCaptures(out int hnc, null, false);
                HasNamedCaptures = hnc;
            }
            return TotalCaptureCount;
        }

        private bool HasNamedCapturesDetermined()
        {
            if (HasNamedCaptures < 0)
                CountCaptures();
            return HasNamedCaptures != 0;
        }

        // Section: Group name lookup (port of find_group_name)

        /// <summary>
        /// Find a group name in the already-parsed group names list.
        /// Returns the count of matching groups. If emitGroupIndex, emits capture indexes.
        /// </summary>
        private int FindGroupName(string name, bool emitGroupIndex)
        {
            int captureIndex = 1;
            int n = 0;
            foreach (var entry in GroupNames)
            {
                if (entry.Name != null && string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    if (emitGroupIndex)
                        _byteCode.Add((byte) captureIndex);
                    n++;
                }
                captureIndex++;
            }
            return n;
        }

        /// <summary>
        /// Check if a group name is a duplicate within the same scope.
        /// Port of is_duplicate_group_name.
        /// </summary>
        private bool IsDuplicateGroupName(string name, int scope)
        {
            foreach (var entry in GroupNames)
            {
                if (entry.Name != null && string.Equals(entry.Name, name, StringComparison.Ordinal) && entry.Scope == scope)
                    return true;
            }
            return false;
        }

        // Section: Modifier parsing (port of re_parse_modifiers)

        private int ParseModifiers()
        {
            int mask = 0;
            while (_pos < _patternEnd)
            {
                int val;
                char c = _pattern[_pos];
                if (c == 'i')
                    val = (int) RegExpFlags.IgnoreCase;
                else if (c == 'm')
                    val = (int) RegExpFlags.Multiline;
                else if (c == 's')
                    val = (int) RegExpFlags.DotAll;
                else
                    break;
                if ((mask & val) != 0)
                    throw new RegExpSyntaxException($"duplicate modifier: '{c}'");
                mask |= val;
                _pos++;
            }
            return mask;
        }

        private static bool UpdateModifier(bool val, int addMask, int removeMask, int mask)
        {
            if ((addMask & mask) != 0) val = true;
            if ((removeMask & mask) != 0) val = false;
            return val;
        }

        // Section: Main parser - re_parse_term

        private void ParseTerm(bool isBackwardDir)
        {
            int lastAtomStart = -1;
            int lastCaptureCount = 0;

            if (_pos >= _patternEnd) return;

            int c = _pattern[_pos];

            switch (c)
            {
                case '^':
                    _pos++;
                    EmitOp(MultiLine ? RegExpOpcode.LineStartM : RegExpOpcode.LineStart);
                    break;

                case '$':
                    _pos++;
                    EmitOp(MultiLine ? RegExpOpcode.LineEndM : RegExpOpcode.LineEnd);
                    break;

                case '.':
                    _pos++;
                    lastAtomStart = _byteCode.Count;
                    lastCaptureCount = CaptureCount;
                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                    EmitOp(DotAll ? RegExpOpcode.Any : RegExpOpcode.Dot);
                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                    break;

                case '{':
                    if (IsUnicode)
                    {
                        throw new RegExpSyntaxException("syntax error");
                    }
                    else if (_pos + 1 >= _patternEnd || !IsAsciiDigit(_pattern[_pos + 1]))
                    {
                        // Annex B: accept '{' not followed by digits as normal atom
                        goto parse_class_atom;
                    }
                    else
                    {
                        // Annex B: error if it looks like a repetition count
                        int p1 = _pos + 1;
                        ParseDigitsAt(_pattern, ref p1, _patternEnd, true);
                        if (p1 < _patternEnd && _pattern[p1] == ',')
                        {
                            p1++;
                            if (p1 < _patternEnd && IsAsciiDigit(_pattern[p1]))
                                ParseDigitsAt(_pattern, ref p1, _patternEnd, true);
                        }
                        if (p1 >= _patternEnd || _pattern[p1] != '}')
                            goto parse_class_atom;
                    }
                    // Fall through to "nothing to repeat"
                    goto case '*';

                case '*':
                case '+':
                case '?':
                    throw new RegExpSyntaxException("nothing to repeat");

                case '(':
                    if (_pos + 1 < _patternEnd && _pattern[_pos + 1] == '?')
                    {
                        if (_pos + 2 < _patternEnd && _pattern[_pos + 2] == ':')
                        {
                            // Non-capturing group (?:...)
                            _pos += 3;
                            lastAtomStart = _byteCode.Count;
                            lastCaptureCount = CaptureCount;
                            ParseDisjunction(isBackwardDir);
                            ParseExpect(')');
                        }
                        else if (_pos + 2 < _patternEnd &&
                                 (_pattern[_pos + 2] is 'i' or 'm' or 's' or '-'))
                        {
                            // Modifiers group (?ims-ims:...)
                            _pos += 2;
                            int addMask = ParseModifiers();
                            int removeMask = 0;
                            if (_pos < _patternEnd && _pattern[_pos] == '-')
                            {
                                _pos++;
                                removeMask = ParseModifiers();
                            }
                            if ((addMask == 0 && removeMask == 0) || (addMask & removeMask) != 0)
                                throw new RegExpSyntaxException("invalid modifiers");
                            ParseExpect(':');

                            bool savedIgnoreCase = IgnoreCase;
                            bool savedMultiLine = MultiLine;
                            bool savedDotAll = DotAll;

                            IgnoreCase = UpdateModifier(IgnoreCase, addMask, removeMask, (int) RegExpFlags.IgnoreCase);
                            MultiLine = UpdateModifier(MultiLine, addMask, removeMask, (int) RegExpFlags.Multiline);
                            DotAll = UpdateModifier(DotAll, addMask, removeMask, (int) RegExpFlags.DotAll);

                            lastAtomStart = _byteCode.Count;
                            lastCaptureCount = CaptureCount;
                            ParseDisjunction(isBackwardDir);
                            ParseExpect(')');

                            IgnoreCase = savedIgnoreCase;
                            MultiLine = savedMultiLine;
                            DotAll = savedDotAll;
                        }
                        else if (_pos + 2 < _patternEnd && _pattern[_pos + 2] is '=' or '!')
                        {
                            // Lookahead (?=...) or (?!...)
                            bool isNeg = _pattern[_pos + 2] == '!';
                            _pos += 3;
                            EmitLookahead(isNeg, false, isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                        }
                        else if (_pos + 3 < _patternEnd && _pattern[_pos + 2] == '<' &&
                                 _pattern[_pos + 3] is '=' or '!')
                        {
                            // Lookbehind (?<=...) or (?<!...)
                            bool isNeg = _pattern[_pos + 3] == '!';
                            _pos += 4;
                            EmitLookahead(isNeg, true, isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                        }
                        else if (_pos + 2 < _patternEnd && _pattern[_pos + 2] == '<')
                        {
                            // Named capture (?<name>...)
                            _pos += 3;
                            string groupName = ParseGroupName();
                            if (IsDuplicateGroupName(groupName, GroupNameScope))
                                throw new RegExpSyntaxException("duplicate group name");
                            GroupNames.Add(new GroupNameEntry(groupName, GroupNameScope));
                            HasNamedCaptures = 1;
                            EmitCapture(isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                        }
                        else
                        {
                            throw new RegExpSyntaxException("invalid group");
                        }
                    }
                    else
                    {
                        // Unnamed capture (...)
                        _pos++; // skip '('
                        GroupNames.Add(new GroupNameEntry(null, 0));
                        EmitCapture(isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                    }
                    break;

                case '\\':
                    if (_pos + 1 >= _patternEnd) goto parse_class_atom;
                    switch (_pattern[_pos + 1])
                    {
                        case 'b':
                        case 'B':
                            {
                                bool isBig = _pattern[_pos + 1] == 'B';
                                if (isBig)
                                    EmitOp(IgnoreCase && IsUnicode ? RegExpOpcode.NotWordBoundaryI : RegExpOpcode.NotWordBoundary);
                                else
                                    EmitOp(IgnoreCase && IsUnicode ? RegExpOpcode.WordBoundaryI : RegExpOpcode.WordBoundary);
                                _pos += 2;
                                break;
                            }

                        case 'k':
                            {
                                int kPos = _pos;
                                ParseBackReferenceByName(isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                                if (_pos == kPos)
                                {
                                    // Annex B fallback: treat \k as literal 'k'
                                    _pos += 2;
                                    lastAtomStart = _byteCode.Count;
                                    lastCaptureCount = CaptureCount;
                                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                                    EmitChar('k');
                                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                                }
                                break;
                            }

                        case '0':
                            {
                                _pos += 2;
                                c = 0;
                                if (IsUnicode)
                                {
                                    if (_pos < _patternEnd && IsAsciiDigit(_pattern[_pos]))
                                        throw new RegExpSyntaxException("invalid decimal escape in regular expression");
                                }
                                else
                                {
                                    // Annex B: accept legacy octal
                                    if (_pos < _patternEnd && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                                    {
                                        c = _pattern[_pos++] - '0';
                                        if (_pos < _patternEnd && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                                            c = (c << 3) + _pattern[_pos++] - '0';
                                    }
                                }
                                EmitNormalChar(c, null, isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                                break;
                            }

                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            {
                                _pos++; // skip '\'
                                int qPos = _pos;
                                c = ParseDigits(false);
                                if (c < 0 || (c >= CaptureCount && c >= CountCaptures()))
                                {
                                    if (!IsUnicode)
                                    {
                                        // Annex B: accept legacy octal
                                        _pos = qPos;
                                        if (_pos < _patternEnd && _pattern[_pos] <= '7')
                                        {
                                            c = 0;
                                            if (_pattern[_pos] <= '3')
                                                c = _pattern[_pos++] - '0';
                                            if (_pos < _patternEnd && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                                            {
                                                c = (c << 3) + _pattern[_pos++] - '0';
                                                if (_pos < _patternEnd && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                                                    c = (c << 3) + _pattern[_pos++] - '0';
                                            }
                                        }
                                        else
                                        {
                                            c = _pattern[_pos++];
                                        }
                                        EmitNormalChar(c, null, isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                                        break;
                                    }
                                    throw new RegExpSyntaxException("back reference out of range in regular expression");
                                }

                                lastAtomStart = _byteCode.Count;
                                lastCaptureCount = CaptureCount;
                                var brOp = (RegExpOpcode) ((int) RegExpOpcode.BackReference +
                                                          2 * (isBackwardDir ? 1 : 0) + (IgnoreCase ? 1 : 0));
                                EmitOpU8(brOp, 1);
                                _byteCode.Add((byte) c);
                                break;
                            }

                        default:
                            goto parse_class_atom;
                    }
                    break;

                case '[':
                    lastAtomStart = _byteCode.Count;
                    lastCaptureCount = CaptureCount;
                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                    ParseCharClass();
                    if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
                    break;

                case ']':
                case '}':
                    if (IsUnicode)
                        throw new RegExpSyntaxException("syntax error");
                    goto parse_class_atom;

                default:
parse_class_atom:
                    {
                        var crLocal = new REStringList();
                        c = GetClassAtom(crLocal, false);
                        if (c < 0) throw new RegExpSyntaxException("unexpected end");
                        EmitNormalChar(c, crLocal, isBackwardDir, ref lastAtomStart, ref lastCaptureCount);
                        break;
                    }
            }

            // Section: Quantifier parsing
            ParseQuantifier(lastAtomStart, lastCaptureCount, isBackwardDir);
        }

        /// <summary>
        /// Emit a normal character or class atom (extracted from the 'normal_char' label in the C code).
        /// </summary>
        private void EmitNormalChar(int c, REStringList? crLocal, bool isBackwardDir,
                                    ref int lastAtomStart, ref int lastCaptureCount)
        {
            lastAtomStart = _byteCode.Count;
            lastCaptureCount = CaptureCount;
            if (isBackwardDir) EmitOp(RegExpOpcode.Prev);

            if (c >= (int) ClassRangeBase)
            {
                if (c == (int) (ClassRangeBase + (uint) CharRangeEnum.Space))
                    EmitOp(RegExpOpcode.Space);
                else if (c == (int) (ClassRangeBase + (uint) CharRangeEnum.NotSpace))
                    EmitOp(RegExpOpcode.NotSpace);
                else if (crLocal != null)
                    EmitStringList(crLocal);
            }
            else
            {
                if (IgnoreCase)
                    c = (int) UnicodeProperties.Canonicalize((uint) c, IsUnicode);
                EmitChar((uint) c);
            }

            if (isBackwardDir) EmitOp(RegExpOpcode.Prev);
        }

        // Section: Lookahead emission helper

        private void EmitLookahead(bool isNeg, bool isBackwardLookahead, bool isBackwardDir,
                                    ref int lastAtomStart, ref int lastCaptureCount)
        {
            // Annex B allows lookahead to be used as an atom for quantifiers
            if (!IsUnicode && !isBackwardLookahead)
            {
                lastAtomStart = _byteCode.Count;
                lastCaptureCount = CaptureCount;
            }

            var lookaheadOp = isNeg ? RegExpOpcode.NegativeLookahead : RegExpOpcode.Lookahead;
            int pos = EmitOpU32(lookaheadOp, 0);
            ParseDisjunction(isBackwardLookahead);
            ParseExpect(')');
            var matchOp = isNeg ? RegExpOpcode.NegativeLookaheadMatch : RegExpOpcode.LookaheadMatch;
            EmitOp(matchOp);
            // Patch the jump target
            PutU32(pos, (uint) (_byteCode.Count - (pos + 4)));
        }

        // Section: Capture emission helper

        private void EmitCapture(bool isBackwardDir, ref int lastAtomStart, ref int lastCaptureCount)
        {
            if (CaptureCount >= CaptureCountMax)
                throw new RegExpSyntaxException("too many captures");
            lastAtomStart = _byteCode.Count;
            lastCaptureCount = CaptureCount;
            int captureIndex = CaptureCount++;

            var saveStartOp = isBackwardDir ? RegExpOpcode.SaveEnd : RegExpOpcode.SaveStart;
            EmitOpU8(saveStartOp, (byte) captureIndex);

            ParseDisjunction(isBackwardDir);

            var saveEndOp = isBackwardDir ? RegExpOpcode.SaveStart : RegExpOpcode.SaveEnd;
            EmitOpU8(saveEndOp, (byte) captureIndex);

            ParseExpect(')');
        }

        // Section: Named backreference parsing (port of \k<name> handling)

        private void ParseBackReferenceByName(bool isBackwardDir,
                                               ref int lastAtomStart, ref int lastCaptureCount)
        {
            if (_pos + 2 >= _patternEnd || _pattern[_pos + 2] != '<')
            {
                if (IsUnicode || HasNamedCapturesDetermined())
                    throw new RegExpSyntaxException("expecting group name");
                return; // Will be handled by parse_class_atom in the caller
            }

            int savedPos = _pos;
            _pos += 3; // skip '\k<'
            string refName;
            try
            {
                refName = ParseGroupName();
            }
            catch
            {
                if (IsUnicode || HasNamedCapturesDetermined())
                    throw new RegExpSyntaxException("invalid group name");
                _pos = savedPos;
                return;
            }

            bool isForward = false;
            int n = FindGroupName(refName, false);
            if (n == 0)
            {
                // No capture name parsed yet; try forward reference
                n = ParseCaptures(out _, refName, false);
                if (n == 0)
                {
                    if (IsUnicode || HasNamedCapturesDetermined())
                        throw new RegExpSyntaxException("group name not defined");
                    _pos = savedPos;
                    return;
                }
                isForward = true;
            }

            lastAtomStart = _byteCode.Count;
            lastCaptureCount = CaptureCount;

            var backRefOp = (RegExpOpcode) ((int) RegExpOpcode.BackReference +
                                           2 * (isBackwardDir ? 1 : 0) + (IgnoreCase ? 1 : 0));
            EmitOpU8(backRefOp, (byte) n);
            if (isForward)
                ParseCaptures(out _, refName, true);
            else
                FindGroupName(refName, true);
        }

        // Section: Quantifier parsing (extracted from re_parse_term)

        private void ParseQuantifier(int lastAtomStart, int lastCaptureCount, bool isBackwardDir)
        {
            if (lastAtomStart < 0 || _pos >= _patternEnd) return;

            int c = _pattern[_pos];
            int quantMin, quantMax;

            switch (c)
            {
                case '*':
                    _pos++;
                    quantMin = 0;
                    quantMax = int.MaxValue;
                    break;
                case '+':
                    _pos++;
                    quantMin = 1;
                    quantMax = int.MaxValue;
                    break;
                case '?':
                    _pos++;
                    quantMin = 0;
                    quantMax = 1;
                    break;
                case '{':
                    {
                        int p1 = _pos;
                        if (_pos + 1 >= _patternEnd || !IsAsciiDigit(_pattern[_pos + 1]))
                        {
                            if (IsUnicode)
                                throw new RegExpSyntaxException("invalid repetition count");
                            return;
                        }
                        _pos++;
                        quantMin = ParseDigits(true);
                        quantMax = quantMin;
                        if (_pos < _patternEnd && _pattern[_pos] == ',')
                        {
                            _pos++;
                            if (_pos < _patternEnd && IsAsciiDigit(_pattern[_pos]))
                            {
                                quantMax = ParseDigits(true);
                                if (quantMax < quantMin)
                                    throw new RegExpSyntaxException("invalid repetition count");
                            }
                            else
                            {
                                quantMax = int.MaxValue;
                            }
                        }
                        if (_pos >= _patternEnd || _pattern[_pos] != '}')
                        {
                            if (!IsUnicode)
                            {
                                // Annex B: normal atom if invalid '{' syntax
                                _pos = p1;
                                return;
                            }
                        }
                        ParseExpect('}');
                        break;
                    }
                default:
                    return;
            }

            // Check for lazy modifier
            bool greedy = true;
            if (_pos < _patternEnd && _pattern[_pos] == '?')
            {
                _pos++;
                greedy = false;
            }

            var checkResult = NeedCheckAdvAndCaptureInit(lastAtomStart, _byteCode.Count);
            bool needCaptureInit = checkResult.NeedCaptureInit;
            bool addZeroAdvanceCheck = checkResult.NeedCheckAdv;

            // Reset captures at each iteration if needed
            if (needCaptureInit && lastCaptureCount != CaptureCount)
            {
                ByteCodeInsert(lastAtomStart, 3);
                _byteCode[lastAtomStart] = (byte) RegExpOpcode.SaveReset;
                _byteCode[lastAtomStart + 1] = (byte) lastCaptureCount;
                _byteCode[lastAtomStart + 2] = (byte) (CaptureCount - 1);
            }

            int len = _byteCode.Count - lastAtomStart;

            if (quantMin == 0)
            {
                // Reset captures if atom is not executed (but only for quant_min==0)
                if (!needCaptureInit && lastCaptureCount != CaptureCount)
                {
                    ByteCodeInsert(lastAtomStart, 3);
                    _byteCode[lastAtomStart] = (byte) RegExpOpcode.SaveReset;
                    _byteCode[lastAtomStart + 1] = (byte) lastCaptureCount;
                    _byteCode[lastAtomStart + 2] = (byte) (CaptureCount - 1);
                    lastAtomStart += 3;
                }

                if (quantMax == 0)
                {
                    // Remove the atom
                    _byteCode.RemoveRange(lastAtomStart, _byteCode.Count - lastAtomStart);
                }
                else if (quantMax == 1 || quantMax == int.MaxValue)
                {
                    bool hasGoto = (quantMax == int.MaxValue);
                    int insertLen = 5 + (addZeroAdvanceCheck ? 2 : 0);
                    ByteCodeInsert(lastAtomStart, insertLen);

                    // When goto target is forward (to continuation), greedy means try atom (next) first
                    _byteCode[lastAtomStart] = (byte) (greedy
                        ? RegExpOpcode.SplitNextFirst
                        : RegExpOpcode.SplitGotoFirst);
                    int jumpDist = len + (hasGoto ? 5 : 0) + (addZeroAdvanceCheck ? 4 : 0);
                    PutU32(lastAtomStart + 1, (uint) jumpDist);

                    if (addZeroAdvanceCheck)
                    {
                        _byteCode[lastAtomStart + 5] = (byte) RegExpOpcode.SetCharPos;
                        _byteCode[lastAtomStart + 6] = 0;
                        EmitOpU8(RegExpOpcode.CheckAdvance, 0);
                    }
                    if (hasGoto)
                        EmitGoto(RegExpOpcode.Goto, lastAtomStart);
                }
                else
                {
                    // Finite quantifier with min=0, max>1
                    int insertLen = 11 + (addZeroAdvanceCheck ? 2 : 0);
                    ByteCodeInsert(lastAtomStart, insertLen);

                    int insPos = lastAtomStart;
                    // When goto target is forward (to continuation), greedy means try atom (next) first
                    _byteCode[insPos] = (byte) (greedy
                        ? RegExpOpcode.SplitNextFirst
                        : RegExpOpcode.SplitGotoFirst);
                    PutU32(insPos + 1, (uint) (6 + (addZeroAdvanceCheck ? 2 : 0) + len + 10));
                    insPos += 5;

                    _byteCode[insPos++] = (byte) RegExpOpcode.SetI32;
                    _byteCode[insPos++] = 0;
                    PutU32(insPos, (uint) quantMax);
                    insPos += 4;
                    lastAtomStart = insPos;

                    if (addZeroAdvanceCheck)
                    {
                        _byteCode[insPos++] = (byte) RegExpOpcode.SetCharPos;
                        _byteCode[insPos] = 0;
                    }

                    var loopOp = addZeroAdvanceCheck
                        ? (greedy ? RegExpOpcode.LoopCheckAdvSplitGotoFirst : RegExpOpcode.LoopCheckAdvSplitNextFirst)
                        : (greedy ? RegExpOpcode.LoopSplitGotoFirst : RegExpOpcode.LoopSplitNextFirst);
                    EmitGotoU8U32(loopOp, 0, (uint) quantMax, lastAtomStart);
                }
            }
            else if (quantMin == 1 && quantMax == int.MaxValue && !addZeroAdvanceCheck)
            {
                // a+ or a+? - simple loop back
                var splitOp = greedy ? RegExpOpcode.SplitGotoFirst : RegExpOpcode.SplitNextFirst;
                EmitGoto(splitOp, lastAtomStart);
            }
            else
            {
                // General case: {n} or {n,m}
                if (quantMin == quantMax)
                    addZeroAdvanceCheck = false;

                int insertLen = 6 + (addZeroAdvanceCheck ? 2 : 0);
                ByteCodeInsert(lastAtomStart, insertLen);

                int insPos = lastAtomStart;
                _byteCode[insPos++] = (byte) RegExpOpcode.SetI32;
                _byteCode[insPos++] = 0;
                PutU32(insPos, (uint) quantMax);
                insPos += 4;
                lastAtomStart = insPos;

                if (addZeroAdvanceCheck)
                {
                    _byteCode[insPos++] = (byte) RegExpOpcode.SetCharPos;
                    _byteCode[insPos] = 0;
                }

                if (quantMin == quantMax)
                {
                    // Simple loop: exactly N times
                    EmitGotoU8(RegExpOpcode.Loop, 0, lastAtomStart);
                }
                else
                {
                    var loopOp = addZeroAdvanceCheck
                        ? (greedy ? RegExpOpcode.LoopCheckAdvSplitGotoFirst : RegExpOpcode.LoopCheckAdvSplitNextFirst)
                        : (greedy ? RegExpOpcode.LoopSplitGotoFirst : RegExpOpcode.LoopSplitNextFirst);
                    EmitGotoU8U32(loopOp, 0, (uint) (quantMax - quantMin), lastAtomStart);
                }
            }
        }

        // Section: Parse alternative (port of re_parse_alternative)

        private void ParseAlternative(bool isBackwardDir)
        {
            int start = _byteCode.Count;

            while (_pos < _patternEnd)
            {
                int ch = _pattern[_pos];
                if (ch == '|' || ch == ')') break;

                int termStart = _byteCode.Count;
                ParseTerm(isBackwardDir);

                if (isBackwardDir)
                {
                    // Reverse the order of the terms (for lookbehind)
                    int end = _byteCode.Count;
                    int termSize = end - termStart;
                    if (termSize > 0 && termStart > start)
                    {
                        var termBytes = new byte[termSize];
                        for (int i = 0; i < termSize; i++)
                            termBytes[i] = _byteCode[termStart + i];
                        _byteCode.RemoveRange(termStart, termSize);
                        _byteCode.InsertRange(start, termBytes);
                    }
                }
            }
        }

        // Section: Parse disjunction (port of re_parse_disjunction)

        private void ParseDisjunction(bool isBackwardDir)
        {
            CheckStackOverflow();
            try
            {
                int start = _byteCode.Count;
                ParseAlternative(isBackwardDir);

                while (_pos < _patternEnd && _pattern[_pos] == '|')
                {
                    _pos++; // skip '|'

                    int len = _byteCode.Count - start;

                    // Insert a split before the first alternative
                    ByteCodeInsert(start, 5);
                    _byteCode[start] = (byte) RegExpOpcode.SplitNextFirst;
                    PutU32(start + 1, (uint) (len + 5));

                    int gotoPos = EmitOpU32(RegExpOpcode.Goto, 0);

                    GroupNameScope++;

                    ParseAlternative(isBackwardDir);

                    // Patch the goto
                    int newLen = _byteCode.Count - (gotoPos + 4);
                    PutU32(gotoPos, (uint) newLen);
                }
            }
            finally
            {
                DecrementRecursionDepth();
            }
        }

        // Section: Register count computation (port of compute_register_count)

        /// <summary>
        /// Allocate the registers as a stack. The control flow is recursive so
        /// the analysis can be linear. Returns max stack size or -1 on error.
        /// </summary>
        private int ComputeRegisterCount()
        {
            int stackSize = 0;
            int stackSizeMax = 0;
            int pos = RegExpHeader.Length;
            int bcLen = _byteCode.Count;

            while (pos < bcLen)
            {
                var opcode = (RegExpOpcode) _byteCode[pos];
                int len = RegExpOpcodeInfo.GetSize(opcode);

                switch (opcode)
                {
                    case RegExpOpcode.SetI32:
                    case RegExpOpcode.SetCharPos:
                        _byteCode[pos + 1] = (byte) stackSize;
                        stackSize++;
                        if (stackSize > stackSizeMax)
                        {
                            if (stackSize > RegisterCountMax)
                                return -1;
                            stackSizeMax = stackSize;
                        }
                        break;

                    case RegExpOpcode.CheckAdvance:
                    case RegExpOpcode.Loop:
                    case RegExpOpcode.LoopSplitGotoFirst:
                    case RegExpOpcode.LoopSplitNextFirst:
                        stackSize--;
                        _byteCode[pos + 1] = (byte) stackSize;
                        break;

                    case RegExpOpcode.LoopCheckAdvSplitGotoFirst:
                    case RegExpOpcode.LoopCheckAdvSplitNextFirst:
                        stackSize -= 2;
                        _byteCode[pos + 1] = (byte) stackSize;
                        break;

                    case RegExpOpcode.Range:
                    case RegExpOpcode.RangeI:
                        len += ReadU16(pos + 1) * 4;
                        break;

                    case RegExpOpcode.Range32:
                    case RegExpOpcode.Range32I:
                        len += ReadU16(pos + 1) * 8;
                        break;

                    case RegExpOpcode.BackReference:
                    case RegExpOpcode.BackReferenceI:
                    case RegExpOpcode.BackwardBackReference:
                    case RegExpOpcode.BackwardBackReferenceI:
                        len += _byteCode[pos + 1];
                        break;
                }
                pos += len;
            }
            return stackSizeMax;
        }

        // Section: Group name serialization

        /// <summary>
        /// Serialize group names to the end of the bytecode buffer.
        /// Format: null-terminated name + scope byte for each capture group (1-based).
        /// </summary>
        private void SerializeGroupNames()
        {
            foreach (var entry in GroupNames)
            {
                if (entry.Name is { Length: > 0 })
                {
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.Name);
                    _byteCode.AddRange(nameBytes);
                }
                _byteCode.Add(0); // null terminator
                _byteCode.Add(entry.Scope);
            }
        }
    }
}

// IDE0055: formatting rules conflict with try/finally wrapping a pre-existing ~600 line while loop
#pragma warning disable IDE0055

// Ported from QuickJS libregexp.c - Bytecode Interpreter
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

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Jint.Runtime.RegExp.Unicode;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Bytecode interpreter for QuickJS-compiled regular expressions.
/// Executes a backtracking NFA over a UTF-16 input string.
/// </summary>
internal static class RegExpInterpreter
{
    /// <summary>Unset capture position sentinel (mirrors NULL in the C code).</summary>
    private const int Unset = -1;

    /// <summary>Number of opcodes between cancellation checks.</summary>
    private const int InterruptCounterInit = 10000;

    /// <summary>Initial backtracking stack capacity (in int elements).</summary>
    private const int InitialStackCapacity = 128;

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Execute regex bytecode against input string.
    /// Returns capture positions array on match, null on no match.
    /// Capture positions: even indices = start char index, odd = end char index.
    /// Value of -1 means the capture group did not participate.
    /// </summary>
    public static int[]? Execute(
        ReadOnlySpan<byte> bytecode,
        string input,
        int startIndex,
        CancellationToken cancellationToken = default)
    {
        var flags = GetFlags(bytecode);
        int captureCount = bytecode[RegExpHeader.OffsetCaptureCount];
        int registerCount = bytecode[RegExpHeader.OffsetRegisterCount];
        bool isUnicode = (flags & (RegExpFlags.Unicode | RegExpFlags.UnicodeSets)) != RegExpFlags.None;

        int allocCount = captureCount * 2 + registerCount;

        // Capture array: indices 0..captureCount*2-1 are capture start/end pairs,
        // indices captureCount*2..allocCount-1 are registers (loop counters, char positions).
        int[]? capturePooled = null;
        Span<int> capture = allocCount <= 64
            ? stackalloc int[allocCount]
            : (capturePooled = ArrayPool<int>.Shared.Rent(allocCount)).AsSpan(0, allocCount);

        capture.Fill(Unset);

        int bytecodeLen = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(RegExpHeader.OffsetBytecodeLen));
        var bc = bytecode.Slice(RegExpHeader.Length, bytecodeLen);

        // Adjust startIndex for surrogate pairs in Unicode mode:
        // if startIndex lands on the low surrogate of a pair, back up to the high surrogate.
        int cindex = startIndex;
        if (cindex > 0 && cindex < input.Length && isUnicode)
        {
            if (char.IsLowSurrogate(input[cindex]) && char.IsHighSurrogate(input[cindex - 1]))
            {
                cindex--;
            }
        }

        try
        {
            int ret = ExecBacktrack(bc, input, capture, cindex, captureCount, isUnicode, cancellationToken);

            int[]? result = null;
            if (ret == 1)
            {
                result = new int[captureCount * 2];
                capture.Slice(0, captureCount * 2).CopyTo(result);
            }

            return result;
        }
        finally
        {
            if (capturePooled is not null)
            {
                ArrayPool<int>.Shared.Return(capturePooled);
            }
        }
    }

    /// <summary>Get capture count from bytecode header.</summary>
    public static int GetCaptureCount(ReadOnlySpan<byte> bytecode)
    {
        return bytecode[RegExpHeader.OffsetCaptureCount];
    }

    /// <summary>Get flags from bytecode header.</summary>
    public static RegExpFlags GetFlags(ReadOnlySpan<byte> bytecode)
    {
        return (RegExpFlags) BinaryPrimitives.ReadUInt16LittleEndian(
            bytecode.Slice(RegExpHeader.OffsetFlags));
    }

    /// <summary>
    /// Get group names from bytecode (stored after the bytecode body).
    /// Returns null if no named groups. Otherwise returns an array of length captureCount
    /// where index 0 is always null (group 0 = entire match) and subsequent entries are the
    /// names for groups 1..captureCount-1. A null entry means the group is unnamed.
    /// </summary>
    public static string?[]? GetGroupNames(ReadOnlySpan<byte> bytecode)
    {
        var flags = GetFlags(bytecode);
        if ((flags & RegExpFlags.NamedGroups) == RegExpFlags.None)
        {
            return null;
        }

        int captureCount = bytecode[RegExpHeader.OffsetCaptureCount];
        int bytecodeLen = BinaryPrimitives.ReadInt32LittleEndian(
            bytecode.Slice(RegExpHeader.OffsetBytecodeLen));
        int offset = RegExpHeader.Length + bytecodeLen;

        var names = new string?[captureCount];
        // Group 0 (entire match) has no name. Groups 1..captureCount-1 follow sequentially.
        for (int i = 1; i < captureCount; i++)
        {
            if (offset >= bytecode.Length)
            {
                break;
            }

            // Each name is a NUL-terminated UTF-8 string.
            int end = offset;
            while (end < bytecode.Length && bytecode[end] != 0)
            {
                end++;
            }

            if (end > offset)
            {
#if NETSTANDARD2_0 || NET462
                names[i] = Encoding.UTF8.GetString(bytecode.Slice(offset, end - offset).ToArray());
#else
                names[i] = Encoding.UTF8.GetString(bytecode.Slice(offset, end - offset));
#endif
            }

            // Skip past NUL terminator + 1 byte scope trailer (LRE_GROUP_NAME_TRAILER_LEN = 2 total)
            offset = end + 1;
            if (offset < bytecode.Length)
            {
                offset++; // skip the trailer byte (group index)
            }
        }

        return names;
    }

    // -----------------------------------------------------------------------
    //  Backtracking state types (mirrors REExecStateEnum in the C code)
    // -----------------------------------------------------------------------

    private enum ExecStateType
    {
        Split = 0,
        Lookahead = 1,
        NegativeLookahead = 2,
    }

    // -----------------------------------------------------------------------
    //  Backtracking stack helpers
    //
    //  The stack stores frames of 3 ints:
    //    [sp-3] = saved pc (bytecode offset)
    //    [sp-2] = saved cindex (string position)
    //    [sp-1] = packed (bp_offset << 2 | state_type)
    //
    //  Between frames, capture-save pairs are stored:
    //    [sp-2] = capture index
    //    [sp-1] = old capture value
    // -----------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PackBpType(int bpOffset, ExecStateType type)
    {
        return (bpOffset << 2) | (int) type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UnpackBp(int packed) => packed >>> 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExecStateType UnpackType(int packed) => (ExecStateType) (packed & 3);

    // -----------------------------------------------------------------------
    //  Character access helpers (UTF-16 with optional surrogate pair handling)
    // -----------------------------------------------------------------------

    /// <summary>Read one code point at cindex, advance cindex past it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetChar(string input, ref int cindex, int end, bool isUnicode)
    {
        int c = input[cindex++];
        if (isUnicode && char.IsHighSurrogate((char) c) && cindex < end && char.IsLowSurrogate(input[cindex]))
        {
            c = char.ConvertToUtf32((char) c, input[cindex++]);
        }
        return c;
    }

    /// <summary>Peek one code point at cindex without advancing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PeekChar(string input, int cindex, int end, bool isUnicode)
    {
        int c = input[cindex];
        if (isUnicode && char.IsHighSurrogate((char) c) && cindex + 1 < end && char.IsLowSurrogate(input[cindex + 1]))
        {
            c = char.ConvertToUtf32((char) c, input[cindex + 1]);
        }
        return c;
    }

    /// <summary>Peek one code point immediately before cindex (looking backward).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PeekPrevChar(string input, int cindex, bool isUnicode)
    {
        int c = input[cindex - 1];
        if (isUnicode && char.IsLowSurrogate((char) c) && cindex >= 2 && char.IsHighSurrogate(input[cindex - 2]))
        {
            c = char.ConvertToUtf32(input[cindex - 2], (char) c);
        }
        return c;
    }

    /// <summary>Read one code point going backward: decrement cindex, return the code point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPrevChar(string input, ref int cindex, bool isUnicode)
    {
        cindex--;
        int c = input[cindex];
        if (isUnicode && char.IsLowSurrogate((char) c) && cindex >= 1 && char.IsHighSurrogate(input[cindex - 1]))
        {
            cindex--;
            c = char.ConvertToUtf32(input[cindex], (char) c);
        }
        return c;
    }

    /// <summary>Move cindex backward by one code point without returning it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrevChar(string input, ref int cindex, bool isUnicode)
    {
        cindex--;
        if (isUnicode && cindex > 0 && char.IsLowSurrogate(input[cindex]) && char.IsHighSurrogate(input[cindex - 1]))
        {
            cindex--;
        }
    }

    // -----------------------------------------------------------------------
    //  Unicode / character classification helpers
    // -----------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineTerminator(int c)
    {
        return c == '\n' || c == '\r' || c == 0x2028 || c == 0x2029;
    }

    /// <summary>
    /// ECMAScript WhiteSpace + LineTerminator test (matches \s character class).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSpace(int c)
    {
        if ((uint) c <= 0xFF)
        {
            // Fast path: tab(9)..cr(13), space(32), nbsp(160)
            return c is (>= 0x09 and <= 0x0D) or 0x20 or 0xA0;
        }

        return IsSpaceNonAscii(c);
    }

    private static bool IsSpaceNonAscii(int c)
    {
        return c is 0x1680
            or (>= 0x2000 and <= 0x200A)
            or 0x2028 or 0x2029
            or 0x202F
            or 0x205F
            or 0x3000
            or 0xFEFF;
    }

    /// <summary>
    /// Test if c is a word character ([A-Za-z0-9_]) for \b / \w matching.
    /// Only checks the ASCII-like range; the caller handles special Unicode cases
    /// (0x017F and 0x212A) for case-insensitive word boundaries separately.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(int c)
    {
        if ((uint) c < 128)
        {
            return c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
        }
        return false;
    }

    /// <summary>
    /// JavaScript regex case-folding (lre_canonicalize equivalent).
    /// Unicode mode: simple case fold (upper -> lower for ASCII, CaseFolding for rest).
    /// Non-Unicode mode: legacy behavior (lower -> upper for ASCII, toUpperCase for rest if single char and >= 128).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Canonicalize(int c, bool isUnicode)
    {
        if (c < 128)
        {
            if (isUnicode)
            {
                if (c >= 'A' && c <= 'Z')
                {
                    return c - 'A' + 'a';
                }
            }
            else
            {
                if (c >= 'a' && c <= 'z')
                {
                    return c - 'a' + 'A';
                }
            }
            return c;
        }

        return CanonicalizeSlow(c, isUnicode);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CanonicalizeSlow(int c, bool isUnicode)
    {
        Span<uint> res = stackalloc uint[3];
        if (isUnicode)
        {
            // Simple case fold (convType=2)
            int len = UnicodeProperties.CaseConv(res, (uint) c, 2);
            if (len == 1)
            {
                return (int) res[0];
            }
            // Multi-character case fold special cases
            return c switch
            {
                0xFB06 => 0xFB05,
                0x1FD3 => 0x0390,
                0x1FE3 => 0x03B0,
                _ => c,
            };
        }
        else
        {
            // Legacy non-unicode: to upper case if single char result >= 128
            int len = UnicodeProperties.CaseConv(res, (uint) c, 0);
            if (len == 1 && res[0] >= 128)
            {
                return (int) res[0];
            }
            return c;
        }
    }

    // -----------------------------------------------------------------------
    //  Bytecode reading helpers
    // -----------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadU16(ReadOnlySpan<byte> bc, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bc.Slice(offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadI32(ReadOnlySpan<byte> bc, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bc.Slice(offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadU32(ReadOnlySpan<byte> bc, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bc.Slice(offset));
    }

    // -----------------------------------------------------------------------
    //  Range matching helpers (binary search over sorted interval pairs)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Binary search over n sorted 16-bit (low, high) pairs starting at bc[pc].
    /// Returns true if c falls within any range. 0xFFFF in the last high means +infinity.
    /// </summary>
    private static bool MatchRange16(ReadOnlySpan<byte> bc, int pc, int n, uint c)
    {
        uint idxMin = 0;
        uint low = ReadU16(bc, pc);
        if (c < low)
        {
            return false;
        }
        uint idxMax = (uint) (n - 1);
        uint high = ReadU16(bc, pc + (int) idxMax * 4 + 2);
        // 0xFFFF in the last high value means +infinity
        if (c >= 0xFFFF && high == 0xFFFF)
        {
            return true;
        }
        if (c > high)
        {
            return false;
        }
        while (idxMin <= idxMax)
        {
            uint idx = (idxMin + idxMax) / 2;
            low = ReadU16(bc, pc + (int) idx * 4);
            high = ReadU16(bc, pc + (int) idx * 4 + 2);
            if (c < low)
            {
                if (idx == 0)
                {
                    return false;
                }
                idxMax = idx - 1;
            }
            else if (c > high)
            {
                idxMin = idx + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Binary search over n sorted 32-bit (low, high) pairs starting at bc[pc].
    /// Returns true if c falls within any range.
    /// </summary>
    private static bool MatchRange32(ReadOnlySpan<byte> bc, int pc, int n, uint c)
    {
        uint idxMin = 0;
        uint low = ReadU32(bc, pc);
        if (c < low)
        {
            return false;
        }
        uint idxMax = (uint) (n - 1);
        uint high = ReadU32(bc, pc + (int) idxMax * 8 + 4);
        if (c > high)
        {
            return false;
        }
        while (idxMin <= idxMax)
        {
            uint idx = (idxMin + idxMax) / 2;
            low = ReadU32(bc, pc + (int) idx * 8);
            high = ReadU32(bc, pc + (int) idx * 8 + 4);
            if (c < low)
            {
                if (idx == 0)
                {
                    return false;
                }
                idxMax = idx - 1;
            }
            else if (c > high)
            {
                idxMin = idx + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    // -----------------------------------------------------------------------
    //  Stack management
    // -----------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureStackSpace(ref int[] stackBuf, ref int[]? stackPooled, int sp, int needed)
    {
        if (sp + needed > stackBuf.Length)
        {
            GrowStack(ref stackBuf, ref stackPooled, sp, sp + needed);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowStack(ref int[] stackBuf, ref int[]? stackPooled, int usedCount, int minCapacity)
    {
        int newSize = stackBuf.Length * 3 / 2;
        if (newSize < minCapacity)
        {
            newSize = minCapacity;
        }

        int[] newBuf = ArrayPool<int>.Shared.Rent(newSize);
        stackBuf.AsSpan(0, usedCount).CopyTo(newBuf);

        if (stackPooled is not null)
        {
            ArrayPool<int>.Shared.Return(stackPooled);
        }

        stackBuf = newBuf;
        stackPooled = newBuf;
    }

    /// <summary>
    /// Push a backtracking frame: saves the full capture+register array (snapshot approach).
    /// This matches QuickJS's push_state which does memcpy of the entire capture array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushFrame(
        ref int[] stackBuf,
        ref int[]? stackPooled,
        ref int sp,
        ref int bp,
        Span<int> capture,
        int allocCount,
        int savedPc,
        int savedCindex,
        ExecStateType stateType)
    {
        int frameSize = 3 + allocCount;
        EnsureStackSpace(ref stackBuf, ref stackPooled, sp, frameSize);
        // Header
        stackBuf[sp] = savedPc;
        stackBuf[sp + 1] = savedCindex;
        stackBuf[sp + 2] = PackBpType(bp, stateType);
        // Full snapshot of captures + registers
        for (int i = 0; i < allocCount; i++)
        {
            stackBuf[sp + 3 + i] = capture[i];
        }
        sp += frameSize;
        bp = sp;
    }

    /// <summary>
    /// Pop a backtracking frame: restores the full capture+register array from snapshot.
    /// Returns the frame's state type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExecStateType PopFrame(
        int[] stackBuf,
        ref int sp,
        ref int bp,
        Span<int> capture,
        int allocCount,
        out int restoredPc,
        out int restoredCindex)
    {
        int frameSize = 3 + allocCount;
        sp -= frameSize;
        restoredPc = stackBuf[sp];
        restoredCindex = stackBuf[sp + 1];
        int packed = stackBuf[sp + 2];
        var stateType = UnpackType(packed);
        bp = UnpackBp(packed);
        // Restore full snapshot
        for (int i = 0; i < allocCount; i++)
        {
            capture[i] = stackBuf[sp + 3 + i];
        }
        return stateType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnStack(int[]? stackPooled)
    {
        if (stackPooled is not null)
        {
            ArrayPool<int>.Shared.Return(stackPooled);
        }
    }

    // -----------------------------------------------------------------------
    //  Main backtracking interpreter (lre_exec_backtrack equivalent)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Execute the backtracking NFA interpreter.
    /// Returns 1 on match, 0 on no match.
    /// Throws <see cref="OperationCanceledException"/> on timeout/cancellation.
    /// </summary>
    private static int ExecBacktrack(
        ReadOnlySpan<byte> bc,
        string input,
        Span<int> capture,
        int cindex,
        int captureCount,
        bool isUnicode,
        CancellationToken cancellationToken)
    {
        int inputEnd = input.Length;
        int allocCount = capture.Length; // captures + registers

        // Frame size: 3 (header: pc, cindex, packedBp) + allocCount (full snapshot)
        int frameSize = 3 + allocCount;

        // Backtracking stack (pooled)
        int[]? stackPooled = ArrayPool<int>.Shared.Rent(InitialStackCapacity);
        int[] stackBuf = stackPooled;
        int sp = 0;  // stack pointer (next free index)
        int bp = 0;  // backtrack pointer (start of current frame's snapshot area, unused in new design but kept for frame nesting)

        int pc = 0;  // program counter (index into bc)
        int interruptCounter = InterruptCounterInit;

        try {
        while (true)
        {
            var opcode = (RegExpOpcode) bc[pc++];

            switch (opcode)
            {
                // ---------------------------------------------------------
                //  Match / terminal
                // ---------------------------------------------------------
                case RegExpOpcode.Match:
                    {
                        return 1;
                    }

                // ---------------------------------------------------------
                //  Character matching
                // ---------------------------------------------------------
                case RegExpOpcode.Char32:
                case RegExpOpcode.Char32I:
                    {
                        uint val = ReadU32(bc, pc);
                        pc += 4;
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (opcode == RegExpOpcode.Char32I)
                        {
                            c = Canonicalize(c, isUnicode);
                        }
                        if (val != (uint) c)
                        {
                            goto noMatch;
                        }
                        break;
                    }

                case RegExpOpcode.Char:
                case RegExpOpcode.CharI:
                    {
                        uint val = ReadU16(bc, pc);
                        pc += 2;
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (opcode == RegExpOpcode.CharI)
                        {
                            c = Canonicalize(c, isUnicode);
                        }
                        if (val != (uint) c)
                        {
                            goto noMatch;
                        }
                        break;
                    }

                case RegExpOpcode.Dot:
                    {
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (IsLineTerminator(c))
                        {
                            goto noMatch;
                        }
                        break;
                    }

                case RegExpOpcode.Any:
                    {
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        GetChar(input, ref cindex, inputEnd, isUnicode);
                        break;
                    }

                // ---------------------------------------------------------
                //  Character classes: \s, \S
                // ---------------------------------------------------------
                case RegExpOpcode.Space:
                    {
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (!IsSpace(c))
                        {
                            goto noMatch;
                        }
                        break;
                    }

                case RegExpOpcode.NotSpace:
                    {
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (IsSpace(c))
                        {
                            goto noMatch;
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Anchors: ^, $
                // ---------------------------------------------------------
                case RegExpOpcode.LineStart:
                case RegExpOpcode.LineStartM:
                    {
                        if (cindex == 0)
                        {
                            break;
                        }
                        if (opcode == RegExpOpcode.LineStart)
                        {
                            goto noMatch;
                        }
                        // Multiline: match after line terminator
                        int c = PeekPrevChar(input, cindex, isUnicode);
                        if (!IsLineTerminator(c))
                        {
                            goto noMatch;
                        }
                        break;
                    }

                case RegExpOpcode.LineEnd:
                case RegExpOpcode.LineEndM:
                    {
                        if (cindex >= inputEnd)
                        {
                            break;
                        }
                        if (opcode == RegExpOpcode.LineEnd)
                        {
                            goto noMatch;
                        }
                        // Multiline: match before line terminator
                        int c = PeekChar(input, cindex, inputEnd, isUnicode);
                        if (!IsLineTerminator(c))
                        {
                            goto noMatch;
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Control flow: goto, split
                // ---------------------------------------------------------
                case RegExpOpcode.Goto:
                    {
                        int val = ReadI32(bc, pc);
                        pc += 4 + val;
                        if (--interruptCounter <= 0)
                        {
                            interruptCounter = InterruptCounterInit;
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        break;
                    }

                case RegExpOpcode.SplitGotoFirst:
                case RegExpOpcode.SplitNextFirst:
                    {
                        int val = ReadI32(bc, pc);
                        pc += 4;

                        int pc1;
                        if (opcode == RegExpOpcode.SplitNextFirst)
                        {
                            pc1 = pc + val;
                        }
                        else
                        {
                            pc1 = pc;
                            pc = pc + val;
                        }

                        PushFrame(ref stackBuf, ref stackPooled, ref sp, ref bp,
                            capture, allocCount, pc1, cindex, ExecStateType.Split);
                        break;
                    }

                // ---------------------------------------------------------
                //  Lookahead assertions
                // ---------------------------------------------------------
                case RegExpOpcode.Lookahead:
                case RegExpOpcode.NegativeLookahead:
                    {
                        int val = ReadI32(bc, pc);
                        pc += 4;

                        var stateType = opcode == RegExpOpcode.Lookahead
                            ? ExecStateType.Lookahead
                            : ExecStateType.NegativeLookahead;
                        PushFrame(ref stackBuf, ref stackPooled, ref sp, ref bp,
                            capture, allocCount, pc + val, cindex, stateType);
                        break;
                    }

                case RegExpOpcode.LookaheadMatch:
                    {
                        // Positive lookahead succeeded. Pop frames until the Lookahead frame.
                        // Keep current captures (modified by the lookahead body).
                        // Only restore pc and cindex from the Lookahead frame.
                        while (true)
                        {
                            sp -= frameSize;
                            int packed = stackBuf[sp + 2];
                            var type = UnpackType(packed);
                            if (type == ExecStateType.Lookahead)
                            {
                                pc = stackBuf[sp];
                                cindex = stackBuf[sp + 1];
                                bp = UnpackBp(packed);
                                break;
                            }
                        }
                        break;
                    }

                case RegExpOpcode.NegativeLookaheadMatch:
                    {
                        // Negative lookahead body matched — meaning the assertion fails.
                        // Pop frames until the NegativeLookahead frame, restoring captures
                        // from the NegativeLookahead frame's snapshot.
                        while (true)
                        {
                            sp -= frameSize;
                            int packed = stackBuf[sp + 2];
                            var type = UnpackType(packed);
                            if (type == ExecStateType.NegativeLookahead)
                            {
                                for (int i = 0; i < allocCount; i++)
                                {
                                    capture[i] = stackBuf[sp + 3 + i];
                                }
                                bp = UnpackBp(packed);
                                break;
                            }
                        }
                        goto noMatch;
                    }

                // ---------------------------------------------------------
                //  Capture groups
                // ---------------------------------------------------------
                case RegExpOpcode.SaveStart:
                case RegExpOpcode.SaveEnd:
                    {
                        int val = bc[pc++];
                        int idx = 2 * val + (opcode - RegExpOpcode.SaveStart);
                        capture[idx] = cindex;
                        break;
                    }

                case RegExpOpcode.SaveReset:
                    {
                        int val = bc[pc];
                        int val2 = bc[pc + 1];
                        pc += 2;
                        while (val <= val2)
                        {
                            capture[2 * val] = Unset;
                            capture[2 * val + 1] = Unset;
                            val++;
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Registers (loop counters and char positions)
                // ---------------------------------------------------------
                case RegExpOpcode.SetI32:
                    {
                        int idx = 2 * captureCount + bc[pc];
                        int val = ReadI32(bc, pc + 1);
                        pc += 5;
                        capture[idx] = val;
                        break;
                    }

                case RegExpOpcode.Loop:
                    {
                        int idx = 2 * captureCount + bc[pc];
                        int val = ReadI32(bc, pc + 1);
                        pc += 5;

                        int val2 = capture[idx] - 1;
                        capture[idx] = val2;
                        if (val2 != 0)
                        {
                            pc += val;
                            if (--interruptCounter <= 0)
                            {
                                interruptCounter = InterruptCounterInit;
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                        break;
                    }

                case RegExpOpcode.LoopSplitGotoFirst:
                case RegExpOpcode.LoopSplitNextFirst:
                case RegExpOpcode.LoopCheckAdvSplitGotoFirst:
                case RegExpOpcode.LoopCheckAdvSplitNextFirst:
                    {
                        int idx = 2 * captureCount + bc[pc];
                        uint limit = ReadU32(bc, pc + 1);
                        int val = ReadI32(bc, pc + 5);
                        pc += 9;

                        // Decrement the counter
                        int val2 = capture[idx] - 1;
                        capture[idx] = val2;

                        if ((uint) val2 > limit)
                        {
                            // Counter still above limit -> unconditional loop iteration
                            pc += val;
                            if (--interruptCounter <= 0)
                            {
                                interruptCounter = InterruptCounterInit;
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                        else
                        {
                            if ((opcode == RegExpOpcode.LoopCheckAdvSplitGotoFirst ||
                                 opcode == RegExpOpcode.LoopCheckAdvSplitNextFirst) &&
                                capture[idx + 1] == cindex &&
                                (uint) val2 != limit)
                            {
                                goto noMatch;
                            }

                            if (val2 != 0)
                            {
                                int pc1;
                                if (opcode == RegExpOpcode.LoopSplitNextFirst ||
                                    opcode == RegExpOpcode.LoopCheckAdvSplitNextFirst)
                                {
                                    pc1 = pc + val;
                                }
                                else
                                {
                                    pc1 = pc;
                                    pc = pc + val;
                                }

                                PushFrame(ref stackBuf, ref stackPooled, ref sp, ref bp,
                                    capture, allocCount, pc1, cindex, ExecStateType.Split);
                            }
                        }
                        break;
                    }

                case RegExpOpcode.SetCharPos:
                    {
                        int idx = 2 * captureCount + bc[pc++];
                        capture[idx] = cindex;
                        break;
                    }

                case RegExpOpcode.CheckAdvance:
                    {
                        int idx = 2 * captureCount + bc[pc++];
                        if (capture[idx] == cindex)
                        {
                            goto noMatch;
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Word boundaries: \b, \B
                // ---------------------------------------------------------
                case RegExpOpcode.WordBoundary:
                case RegExpOpcode.WordBoundaryI:
                case RegExpOpcode.NotWordBoundary:
                case RegExpOpcode.NotWordBoundaryI:
                    {
                        bool ignoreCase = opcode == RegExpOpcode.WordBoundaryI ||
                                          opcode == RegExpOpcode.NotWordBoundaryI;
                        bool isBoundary = opcode == RegExpOpcode.WordBoundary ||
                                          opcode == RegExpOpcode.WordBoundaryI;

                        // Character before current position
                        bool v1;
                        if (cindex == 0)
                        {
                            v1 = false;
                        }
                        else
                        {
                            int c = PeekPrevChar(input, cindex, isUnicode);
                            v1 = c < 256
                                ? IsWordChar(c)
                                : ignoreCase && (c == 0x017F || c == 0x212A);
                        }

                        // Character at current position
                        bool v2;
                        if (cindex >= inputEnd)
                        {
                            v2 = false;
                        }
                        else
                        {
                            int c = PeekChar(input, cindex, inputEnd, isUnicode);
                            v2 = c < 256
                                ? IsWordChar(c)
                                : ignoreCase && (c == 0x017F || c == 0x212A);
                        }

                        // XOR with isBoundary to get: boundary -> v1^v2 must be true;
                        // not-boundary -> v1^v2 must be false.
                        if (v1 ^ v2 ^ isBoundary)
                        {
                            goto noMatch;
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Back references: \1..\N
                // ---------------------------------------------------------
                case RegExpOpcode.BackReference:
                case RegExpOpcode.BackReferenceI:
                case RegExpOpcode.BackwardBackReference:
                case RegExpOpcode.BackwardBackReferenceI:
                    {
                        int n = bc[pc++];
                        int pc1 = pc;
                        pc += n;

                        for (int i = 0; i < n; i++)
                        {
                            int val = bc[pc1 + i];
                            if (val >= captureCount)
                            {
                                goto noMatch;
                            }

                            int cptr1Start = capture[2 * val];
                            int cptr1End = capture[2 * val + 1];

                            // Use the first non-empty capture
                            if (cptr1Start != Unset && cptr1End != Unset)
                            {
                                if (opcode == RegExpOpcode.BackReference ||
                                    opcode == RegExpOpcode.BackReferenceI)
                                {
                                    // Forward match
                                    int cptr1 = cptr1Start;
                                    while (cptr1 < cptr1End)
                                    {
                                        if (cindex >= inputEnd)
                                        {
                                            goto noMatch;
                                        }
                                        int c1 = GetChar(input, ref cptr1, cptr1End, isUnicode);
                                        int c2 = GetChar(input, ref cindex, inputEnd, isUnicode);
                                        if (opcode == RegExpOpcode.BackReferenceI)
                                        {
                                            c1 = Canonicalize(c1, isUnicode);
                                            c2 = Canonicalize(c2, isUnicode);
                                        }
                                        if (c1 != c2)
                                        {
                                            goto noMatch;
                                        }
                                    }
                                }
                                else
                                {
                                    // Backward match (for lookbehind)
                                    int cptr1 = cptr1End;
                                    while (cptr1 > cptr1Start)
                                    {
                                        if (cindex == 0)
                                        {
                                            goto noMatch;
                                        }
                                        int c1 = GetPrevChar(input, ref cptr1, isUnicode);
                                        int c2 = GetPrevChar(input, ref cindex, isUnicode);
                                        if (opcode == RegExpOpcode.BackwardBackReferenceI)
                                        {
                                            c1 = Canonicalize(c1, isUnicode);
                                            c2 = Canonicalize(c2, isUnicode);
                                        }
                                        if (c1 != c2)
                                        {
                                            goto noMatch;
                                        }
                                    }
                                }
                                break; // Found the first non-empty capture, stop searching
                            }
                        }
                        break;
                    }

                // ---------------------------------------------------------
                //  Character ranges (16-bit pairs)
                // ---------------------------------------------------------
                case RegExpOpcode.Range:
                case RegExpOpcode.RangeI:
                    {
                        int n = ReadU16(bc, pc); // n must be >= 1
                        pc += 2;
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (opcode == RegExpOpcode.RangeI)
                        {
                            c = Canonicalize(c, isUnicode);
                        }

                        if (!MatchRange16(bc, pc, n, (uint) c))
                        {
                            goto noMatch;
                        }
                        pc += 4 * n;
                        break;
                    }

                // ---------------------------------------------------------
                //  Character ranges (32-bit pairs)
                // ---------------------------------------------------------
                case RegExpOpcode.Range32:
                case RegExpOpcode.Range32I:
                    {
                        int n = ReadU16(bc, pc); // n must be >= 1
                        pc += 2;
                        if (cindex >= inputEnd)
                        {
                            goto noMatch;
                        }
                        int c = GetChar(input, ref cindex, inputEnd, isUnicode);
                        if (opcode == RegExpOpcode.Range32I)
                        {
                            c = Canonicalize(c, isUnicode);
                        }

                        if (!MatchRange32(bc, pc, n, (uint) c))
                        {
                            goto noMatch;
                        }
                        pc += 8 * n;
                        break;
                    }

                // ---------------------------------------------------------
                //  Prev (go backward one code point, for lookbehind)
                // ---------------------------------------------------------
                case RegExpOpcode.Prev:
                    {
                        if (cindex == 0)
                        {
                            goto noMatch;
                        }
                        PrevChar(input, ref cindex, isUnicode);
                        break;
                    }

                default:
                    ThrowInvalidOpcode(opcode);
                    break;
            }
            continue;

// =============================================================
//  Backtracking: current path failed
// =============================================================
noMatch:
            while (true)
            {
                if (sp == 0)
                {
                    // No more backtracking frames - overall failure.
                    return 0;
                }

                // Pop frame with full capture+register restore
                var stateType = PopFrame(stackBuf, ref sp, ref bp, capture, allocCount,
                    out pc, out cindex);

                // For Lookahead frames we keep unwinding (a lookahead failure
                // means the inner expression failed, not an alternative to try).
                // For Split and NegativeLookahead we break to resume execution.
                if (stateType != ExecStateType.Lookahead)
                {
                    break;
                }
            }

            // Poll for cancellation after backtracking
            if (--interruptCounter <= 0)
            {
                interruptCounter = InterruptCounterInit;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        } finally { ReturnStack(stackPooled); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOpcode(RegExpOpcode opcode)
    {
        throw new InvalidOperationException($"Unknown regex opcode: {opcode} ({(int) opcode})");
    }
}

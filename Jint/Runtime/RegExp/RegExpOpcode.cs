// Ported from QuickJS libregexp-opcode.h
// Copyright (c) 2017-2018 Fabrice Bellard - MIT License

namespace Jint.Runtime.RegExp;

/// <summary>
/// Regular expression bytecode opcodes. Each opcode has a fixed base size
/// (some are variable-length due to trailing range data).
/// </summary>
internal enum RegExpOpcode : byte
{
    Invalid = 0,     // size: 1 - never used
    Char,            // size: 3 - match 16-bit character
    CharI,           // size: 3 - match 16-bit character, case-insensitive
    Char32,          // size: 5 - match 32-bit code point
    Char32I,         // size: 5 - match 32-bit code point, case-insensitive
    Dot,             // size: 1 - match any char except line terminators
    Any,             // size: 1 - match any char including line terminators
    Space,           // size: 1 - match whitespace
    NotSpace,        // size: 1 - match non-whitespace (must come after Space)
    LineStart,       // size: 1 - ^
    LineStartM,      // size: 1 - ^ with multiline flag
    LineEnd,         // size: 1 - $
    LineEndM,        // size: 1 - $ with multiline flag
    Goto,            // size: 5 - unconditional jump (relative offset as int32)
    SplitGotoFirst,  // size: 5 - NFA branch: try goto first, then next
    SplitNextFirst,  // size: 5 - NFA branch: try next first, then goto
    Match,           // size: 1 - successful match
    LookaheadMatch,  // size: 1 - successful lookahead match
    NegativeLookaheadMatch, // size: 1 - successful negative lookahead match (must come after)
    SaveStart,       // size: 2 - save start position of capture group
    SaveEnd,         // size: 2 - save end position of capture group (must come after SaveStart)
    SaveReset,       // size: 3 - reset capture group positions
    Loop,            // size: 6 - decrement top of stack and goto if != 0
    LoopSplitGotoFirst,       // size: 10 - loop then split (greedy)
    LoopSplitNextFirst,       // size: 10 - loop then split (lazy)
    LoopCheckAdvSplitGotoFirst, // size: 10 - loop, check advance, then split (greedy)
    LoopCheckAdvSplitNextFirst, // size: 10 - loop, check advance, then split (lazy)
    SetI32,          // size: 6 - store immediate value to register
    WordBoundary,    // size: 1 - \b
    WordBoundaryI,   // size: 1 - \b case-insensitive
    NotWordBoundary, // size: 1 - \B
    NotWordBoundaryI,// size: 1 - \B case-insensitive
    BackReference,   // size: 2 - \N backreference (variable length match)
    BackReferenceI,  // size: 2 - \N case-insensitive (must come after)
    BackwardBackReference,  // size: 2 - backward \N for lookbehind (must come after)
    BackwardBackReferenceI, // size: 2 - backward \N case-insensitive (must come after)
    Range,           // size: 3+ - character range (variable length, 16-bit pairs)
    RangeI,          // size: 3+ - character range, case-insensitive
    Range32,         // size: 3+ - character range (variable length, 32-bit pairs)
    Range32I,        // size: 3+ - character range, case-insensitive
    Lookahead,       // size: 5 - positive lookahead (?=...)
    NegativeLookahead, // size: 5 - negative lookahead (?!...) (must come after)
    SetCharPos,      // size: 2 - store character position to register
    CheckAdvance,    // size: 2 - check register differs from current position
    Prev,            // size: 1 - go to previous character (for lookbehind)

    Count
}

/// <summary>
/// Base sizes for each opcode (excluding variable-length range data).
/// </summary>
internal static class RegExpOpcodeInfo
{
    private static ReadOnlySpan<byte> Sizes =>
    [
        1,  // Invalid
        3,  // Char
        3,  // CharI
        5,  // Char32
        5,  // Char32I
        1,  // Dot
        1,  // Any
        1,  // Space
        1,  // NotSpace
        1,  // LineStart
        1,  // LineStartM
        1,  // LineEnd
        1,  // LineEndM
        5,  // Goto
        5,  // SplitGotoFirst
        5,  // SplitNextFirst
        1,  // Match
        1,  // LookaheadMatch
        1,  // NegativeLookaheadMatch
        2,  // SaveStart
        2,  // SaveEnd
        3,  // SaveReset
        6,  // Loop
        10, // LoopSplitGotoFirst
        10, // LoopSplitNextFirst
        10, // LoopCheckAdvSplitGotoFirst
        10, // LoopCheckAdvSplitNextFirst
        6,  // SetI32
        1,  // WordBoundary
        1,  // WordBoundaryI
        1,  // NotWordBoundary
        1,  // NotWordBoundaryI
        2,  // BackReference
        2,  // BackReferenceI
        2,  // BackwardBackReference
        2,  // BackwardBackReferenceI
        3,  // Range
        3,  // RangeI
        3,  // Range32
        3,  // Range32I
        5,  // Lookahead
        5,  // NegativeLookahead
        2,  // SetCharPos
        2,  // CheckAdvance
        1,  // Prev
    ];

    public static int GetSize(RegExpOpcode op) => Sizes[(int) op];
}

/// <summary>
/// Regex flags stored in bytecode header.
/// </summary>
[Flags]
internal enum RegExpFlags : ushort
{
    None = 0,
    Global = 1 << 0,
    IgnoreCase = 1 << 1,
    Multiline = 1 << 2,
    DotAll = 1 << 3,
    Unicode = 1 << 4,
    Sticky = 1 << 5,
    Indices = 1 << 6,
    NamedGroups = 1 << 7,
    UnicodeSets = 1 << 8,
}

/// <summary>
/// Bytecode header layout constants.
/// </summary>
internal static class RegExpHeader
{
    public const int OffsetFlags = 0;          // uint16
    public const int OffsetCaptureCount = 2;   // byte
    public const int OffsetRegisterCount = 3;  // byte
    public const int OffsetBytecodeLen = 4;    // int32 (little-endian)
    public const int Length = 8;
}

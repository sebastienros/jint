/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.Native.Number.Dtoa;

[StructLayout(LayoutKind.Auto)]
internal ref struct DtoaBuilder
{
    // allocate buffer for generated digits + extra notation + padding zeroes
    internal readonly Span<char> _chars;
    internal int Length;

    public DtoaBuilder(Span<char> initialBuffer)
    {
        _chars = initialBuffer;
    }

    internal void Append(char c)
    {
        _chars[Length++] = c;
    }

    internal void DecreaseLast()
    {
        _chars[Length - 1]--;
    }

    public void Reset()
    {
        Length = 0;
        _chars.Clear();
    }

    public char this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chars[i];
        set
        {
            _chars[i] = value;
            Length = System.Math.Max(Length, i + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> Slice(int start, int length) => _chars.Slice(start, length);

    public override string ToString() => "[chars:" + _chars.Slice(0, Length).ToString() + "]";
}

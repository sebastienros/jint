/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace Jint.Native.Number.Dtoa
{
    internal class DtoaBuilder
    {
        // allocate buffer for generated digits + extra notation + padding zeroes
        internal readonly char[] _chars;
        internal int Length;

        public DtoaBuilder(int size)
        {
            _chars = new char[size];
        }

        public DtoaBuilder() : this(FastDtoa.KFastDtoaMaximalLength + 8)
        {
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
            System.Array.Clear(_chars, 0, _chars.Length);
        }

        public char this[int i]
        {
            get => _chars[i];
            set
            {
                _chars[i] = value;
                Length = System.Math.Max(Length, i + 1);
            }
        }

        public override string ToString()
        {
            return "[chars:" + new string(_chars, 0, Length) + "]";
        }
    }
}
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace Jint.Native.Number.Dtoa
{
    public class FastDtoaBuilder
    {

        // allocate buffer for generated digits + extra notation + padding zeroes
        private readonly char[] _chars = new char[FastDtoa.KFastDtoaMaximalLength + 8];
        internal int End = 0;
        internal int Point;
        private bool _formatted;

        internal void Append(char c)
        {
            _chars[End++] = c;
        }

        internal void DecreaseLast()
        {
            _chars[End - 1]--;
        }

        public void Reset()
        {
            End = 0;
            _formatted = false;
        }

        public override string ToString()
        {
            return "[chars:" + new System.String(_chars, 0, End) + ", point:" + Point + "]";
        }

        public System.String Format()
        {
            if (!_formatted)
            {
                // check for minus sign
                int firstDigit = _chars[0] == '-' ? 1 : 0;
                int decPoint = Point - firstDigit;
                if (decPoint < -5 || decPoint > 21)
                {
                    ToExponentialFormat(firstDigit, decPoint);
                }
                else
                {
                    ToFixedFormat(firstDigit, decPoint);
                }
                _formatted = true;
            }
            return new System.String(_chars, 0, End);

        }

        private void ToFixedFormat(int firstDigit, int decPoint)
        {
            if (Point < End)
            {
                // insert decimal point
                if (decPoint > 0)
                {
                    // >= 1, split decimals and insert point
                    System.Array.Copy(_chars, Point, _chars, Point + 1, End - Point);
                    _chars[Point] = '.';
                    End++;
                }
                else
                {
                    // < 1,
                    int target = firstDigit + 2 - decPoint;
                    System.Array.Copy(_chars, firstDigit, _chars, target, End - firstDigit);
                    _chars[firstDigit] = '0';
                    _chars[firstDigit + 1] = '.';
                    if (decPoint < 0)
                    {
                        Fill(_chars, firstDigit + 2, target, '0');
                    }
                    End += 2 - decPoint;
                }
            }
            else if (Point > End)
            {
                // large integer, add trailing zeroes
                Fill(_chars, End, Point, '0');
                End += Point - End;
            }
        }

        private void ToExponentialFormat(int firstDigit, int decPoint)
        {
            if (End - firstDigit > 1)
            {
                // insert decimal point if more than one digit was produced
                int dot = firstDigit + 1;
                System.Array.Copy(_chars, dot, _chars, dot + 1, End - dot);
                _chars[dot] = '.';
                End++;
            }
            _chars[End++] = 'e';
            char sign = '+';
            int exp = decPoint - 1;
            if (exp < 0)
            {
                sign = '-';
                exp = -exp;
            }
            _chars[End++] = sign;

            int charPos = exp > 99 ? End + 2 : exp > 9 ? End + 1 : End;
            End = charPos + 1;

            // code below is needed because Integer.getChars() is not public
            for (;;)
            {
                int r = exp%10;
                _chars[charPos--] = Digits[r];
                exp = exp/10;
                if (exp == 0) break;
            }
        }

        private static readonly char[] Digits =
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };

        private void Fill<T>(T[] array, int fromIndex, int toIndex, T val)
        {
            for (int i = fromIndex; i < toIndex; i++)
            {
                array[i] = val;
            }
        }
    }
}
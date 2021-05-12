using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class OperatorOverloadingTests : IDisposable
    {
        private readonly Engine _engine;

        public OperatorOverloadingTests()
        {
            _engine = new Engine(cfg => cfg
                .AllowOperatorOverloading())
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("assertFalse", new Action<bool>(Assert.False))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .SetValue("Vector2", typeof(Vector2))
                .SetValue("Vector3", typeof(Vector3))
            ;
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        public class Vector2
        {
            public double X { get; }
            public double Y { get; }

            public Vector2(double x, double y)
            {
                X = x;
                Y = y;
            }

            public static Vector2 operator +(Vector2 left, Vector2 right) => new Vector2(left.X + right.X, left.Y + right.Y);
            public static Vector2 operator +(Vector2 left, double right) => new Vector2(left.X + right, left.Y + right);
            public static Vector2 operator +(string left, Vector2 right) => new Vector2(right.X, right.Y);
            public static Vector2 operator +(double left, Vector2 right) => new Vector2(right.X + left, right.Y + left);
            public static Vector2 operator *(Vector2 left, double right) => new Vector2(left.X * right, left.Y * right);
            public static Vector2 operator /(Vector2 left, double right) => new Vector2(left.X / right, left.Y / right);

            public static double operator +(Vector2 operand) => Math.Sqrt(operand.X * operand.X + operand.Y * operand.Y);
            public static Vector2 operator -(Vector2 operand) => new Vector2(-operand.X, -operand.Y);
            public static bool operator !(Vector2 operand) => (+operand) == 0;
            public static Vector2 operator ~(Vector2 operand) => new Vector2(operand.Y, operand.X);
            public static Vector2 operator ++(Vector2 operand) => new Vector2(operand.X + 1, operand.Y + 1);
            public static Vector2 operator --(Vector2 operand) => new Vector2(operand.X - 1, operand.Y - 1);
            
            public static implicit operator Vector3(Vector2 val) => new Vector3(val.X, val.Y, 0);
            public static bool operator !=(Vector2 left, Vector2 right) => !(left == right);
            public static bool operator ==(Vector2 left, Vector2 right) => left.X == right.X && left.Y == right.Y;
            public override bool Equals(object obj) => ReferenceEquals(this, obj);
            public override int GetHashCode() => X.GetHashCode() + Y.GetHashCode();
        }

        public class Vector3
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }

            public Vector3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Vector3 operator +(Vector3 left, double right)
            {
                return new Vector3(left.X + right, left.Y + right, left.Z + right);
            }

            public static Vector3 operator +(double left, Vector3 right)
            {
                return new Vector3(right.X + left, right.Y + left, right.Z + left);
            }

            public static Vector3 operator +(Vector3 left, Vector3 right)
            {
                return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
            }
        }

        [Fact]
        public void OperatorOverloading_BinaryOperators()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector2(3, 4);
                var n = 6;

                var r1 = v1 + v2;
                equal(4, r1.X);
                equal(6, r1.Y);

                var r2 = n + v1;
                equal(7, r2.X);
                equal(8, r2.Y);

                var r3 = v1 + n;
                equal(7, r3.X);
                equal(8, r3.Y);

                var r4 = v1 * n;
                equal(6, r4.X);
                equal(12, r4.Y);

                var r5 = v1 / n;
                equal(1 / 6, r5.X);
                equal(2 / 6, r5.Y);
            ");
        }

        [Fact]
        public void OperatorOverloading_ShouldCoerceTypes()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector3(4, 5, 6);
                var res = v1 + v2;
                equal(5, res.X);
                equal(7, res.Y);
                equal(6, res.Z);
            ");
        }

        [Fact]
        public void OperatorOverloading_ShouldWorkForEqualityButNotForStrictEquality()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector2(1, 2);
                assert(v1 == v2);
                assertFalse(v1 != v2);
                assert(v1 !== v2);
                assertFalse(v1 === v2);


                var z1 = new Vector3(1, 2, 3);
                var z2 = new Vector3(1, 2, 3);
                assertFalse(z1 == z2);
            ");
        }

        [Fact]
        public void OperatorOverloading_UnaryOperators()
        {
            RunTest(@"
                var v0 = new Vector2(0, 0);
                var v = new Vector2(3, 4);
                var rv = -v;
                var bv = ~v;
                
                assert(!v0);
                assertFalse(!v);

                equal(0, +v0);
                equal(5, +v);
                equal(5, +rv);
                equal(-3, rv.X);
                equal(-4, rv.Y);

                equal(4, bv.X);
                equal(3, bv.Y);
            ");
        }

        [Fact]
        public void OperatorOverloading_IncrementOperatorShouldWork()
        {
            RunTest(@"
                var v = new Vector2(3, 22);
                var original = v;
                var pre = ++v;
                var post = v++;

                equal(3, original.X);
                equal(4, pre.X);
                equal(4, post.X);
                equal(5, v.X);

                var decPre = --v;
                var decPost = v--;

                equal(4, decPre.X);
                equal(4, decPost.X);
                equal(3, v.X);
            ");
        }

    }
}

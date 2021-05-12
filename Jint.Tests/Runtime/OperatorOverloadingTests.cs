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

            // This exists to test if the operator overloading can choose the correct method
            public static Vector2 operator +(string left, Vector2 right) => new Vector2(right.X, right.Y);

            public static Vector2 operator *(Vector2 left, double right) => new Vector2(left.X * right, left.Y * right);

            public static Vector2 operator /(Vector2 left, double right) => new Vector2(left.X / right, left.Y / right);

            public static Vector2 operator +(double left, Vector2 right) => new Vector2(right.X + left, right.Y + left);

            public static implicit operator Vector3(Vector2 val) => new Vector3(val.X, val.Y, 0);

            public static bool operator !=(Vector2 left, Vector2 right) => !(left == right);

            public static bool operator ==(Vector2 left, Vector2 right) => left.X == right.X && left.Y == right.Y;

            public override bool Equals(object obj) => ReferenceEquals(this, obj);

            public override int GetHashCode() => X.GetHashCode() + Y.GetHashCode();

            public static double operator +(Vector2 operand) => Math.Sqrt(operand.X * operand.X + operand.Y * operand.Y);
            public static Vector2 operator -(Vector2 operand) => new Vector2(-operand.X, -operand.Y);
            public static bool operator !(Vector2 operand) => (+operand) == 0;
            public static Vector2 operator ~(Vector2 operand) => new Vector2(operand.Y, operand.X);
            public static Vector2 operator ++(Vector2 operand) => new Vector2(operand.X + 1, operand.Y + 1);
            public static Vector2 operator --(Vector2 operand) => new Vector2(operand.X - 1, operand.Y - 1);
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
                equal(r1.X, 4);
                equal(r1.Y, 6);

                var r2 = n + v1;
                equal(r2.X, 7);
                equal(r2.Y, 8);

                var r3 = v1 + n;
                equal(r3.X, 7);
                equal(r3.Y, 8);

                var r4 = v1 * n;
                equal(r4.X, 6);
                equal(r4.Y, 12);

                var r5 = v1 / n;
                equal(r5.X, 1 / 6);
                equal(r5.Y, 2 / 6);
            ");
        }

        [Fact]
        public void OperatorOverloading_ShouldCoerceTypes()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector3(4, 5, 6);
                var res = v1 + v2;
                equal(res.X, 5);
                equal(res.Y, 7);
                equal(res.Z, 6);
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

                equal(+v0, 0);
                equal(+v, 5);
                equal(+rv, 5);
                equal(rv.X, -3);
                equal(rv.Y, -4);

                equal(bv.X, 4);
                equal(bv.Y, 3);
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

                equal(original.X, 3);
                equal(pre.X, 4);
                equal(post.X, 4);
                equal(v.X, 5);

                var decPre = --v;
                var decPost = v--;

                equal(decPre.X, 4);
                equal(decPost.X, 4);
                equal(v.X, 3);
            ");
        }

    }
}

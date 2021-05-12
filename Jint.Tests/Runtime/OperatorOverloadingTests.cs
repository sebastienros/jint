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

            public static Vector2 operator +(Vector2 left, Vector2 right)
            {
                return new Vector2(left.X + right.X, left.Y + right.Y);
            }

            public static Vector2 operator +(Vector2 left, double right)
            {
                return new Vector2(left.X + right, left.Y + right);
            }

            public static Vector2 operator +(string left, Vector2 right)
            {
                // This exists to test if the operator overloading can choose the correct method
                return new Vector2(right.X, right.Y);
            }

            public static Vector2 operator *(Vector2 left, double right)
            {
                return new Vector2(left.X * right, left.Y * right);
            }

            public static Vector2 operator /(Vector2 left, double right)
            {
                return new Vector2(left.X / right, left.Y / right);
            }

            public static Vector2 operator +(double left, Vector2 right)
            {
                return new Vector2(right.X + left, right.Y + left);
            }

            public static implicit operator Vector3(Vector2 val)
            {
                return new Vector3(val.X, val.Y, 0);
            }

            public static bool operator !=(Vector2 left, Vector2 right)
            {
                return !(left == right);
            }

            public static bool operator ==(Vector2 left, Vector2 right)
            {
                return left.X == right.X && left.Y == right.Y;
            }

            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj);
            }

            public override int GetHashCode()
            {
                return X.GetHashCode() + Y.GetHashCode();
            }
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
        public void OperatorOverloading_ShouldWork()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector2(3, 4);
                var n = 6;

                var r1 = v1 + v2;
                assert(r1.X === 4);
                assert(r1.Y === 6);

                var r2 = n + v1;
                assert(r2.X === 7);
                assert(r2.Y === 8);

                var r3 = v1 + n;
                assert(r3.X === 7);
                assert(r3.Y === 8);

                var r4 = v1 * n;
                assert(r4.X === 6);
                assert(r4.Y === 12);

                var r5 = v1 / n;
                assert(r5.X === (1 / 6));
                assert(r5.Y === (2 / 6));
            ");
        }

        [Fact]
        public void OperatorOverloading_ShouldCoerceTypes()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector3(4, 5, 6);
                var res = v1 + v2;
                assert(res.X === 5);
                assert(res.Y === 7);
                assert(res.Z === 6);
            ");
        }

        [Fact]
        public void OperatorOverloading_ShouldWorkForEqualityButNotForStrictEquality()
        {
            RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector2(1, 2);
                assert(v1 == v2);
                assert(!(v1 != v2));
                assert(v1 !== v2);
                assert(!(v1 === v2));


                var z1 = new Vector3(1, 2, 3);
                var z2 = new Vector3(1, 2, 3);
                assert(!(z1 == z2));
            ");
        }
    }
}

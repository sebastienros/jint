using Jint.Native.String;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class OperatorOverloadingTests
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
            .SetValue("Vector2Child", typeof(Vector2Child));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    public class Vector2
    {
        public double X { get; }
        public double Y { get; }
        public double SqrMagnitude => X * X + Y * Y;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vector2 operator +(Vector2 left, Vector2 right) => new Vector2(left.X + right.X, left.Y + right.Y);
        public static Vector2 operator +(Vector2 left, double right) => new Vector2(left.X + right, left.Y + right);
        public static Vector2 operator +(string left, Vector2 right) => new Vector2(right.X, right.Y);
        public static Vector2 operator +(double left, Vector2 right) => new Vector2(right.X + left, right.Y + left);
        public static Vector2 operator -(Vector2 left, Vector2 right) => new Vector2(left.X - right.X, left.Y - right.Y);
        public static Vector2 operator *(Vector2 left, double right) => new Vector2(left.X * right, left.Y * right);
        public static Vector2 operator /(Vector2 left, double right) => new Vector2(left.X / right, left.Y / right);

        public static bool operator >(Vector2 left, Vector2 right) => left.Magnitude > right.Magnitude;
        public static bool operator <(Vector2 left, Vector2 right) => left.Magnitude < right.Magnitude;
        public static bool operator >=(Vector2 left, Vector2 right) => left.Magnitude >= right.Magnitude;
        public static bool operator <=(Vector2 left, Vector2 right) => left.Magnitude <= right.Magnitude;
        public static Vector2 operator %(Vector2 left, Vector2 right) => new Vector2(left.X % right.X, left.Y % right.Y);
        public static double operator &(Vector2 left, Vector2 right) => left.X * right.X + left.Y * right.Y;
        public static Vector2 operator |(Vector2 left, Vector2 right) => right * ((left & right) / right.SqrMagnitude);


        public static double operator +(Vector2 operand) => operand.Magnitude;
        public static Vector2 operator -(Vector2 operand) => new Vector2(-operand.X, -operand.Y);
        public static bool operator !(Vector2 operand) => operand.Magnitude == 0;
        public static Vector2 operator ~(Vector2 operand) => new Vector2(operand.Y, operand.X);
        public static Vector2 operator ++(Vector2 operand) => new Vector2(operand.X + 1, operand.Y + 1);
        public static Vector2 operator --(Vector2 operand) => new Vector2(operand.X - 1, operand.Y - 1);

        public static implicit operator Vector3(Vector2 val) => new Vector3(val.X, val.Y, 0);
        public static bool operator !=(Vector2 left, Vector2 right) => !(left == right);
        public static bool operator ==(Vector2 left, Vector2 right) => left.X == right.X && left.Y == right.Y;
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => X.GetHashCode() + Y.GetHashCode();
    }

    public class Vector2Child : Vector2
    {
        public Vector2Child(double x, double y) : base(x, y) { }

        public static Vector2Child operator +(Vector2Child left, double right) => new Vector2Child(left.X + 2 * right, left.Y + 2 * right);
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

        public static Vector3 operator +(Vector3 left, double right) => new Vector3(left.X + right, left.Y + right, left.Z + right);
        public static Vector3 operator +(double left, Vector3 right) => new Vector3(right.X + left, right.Y + left, right.Z + left);
        public static Vector3 operator +(Vector3 left, Vector3 right) => new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    private struct Vector2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }


        public static Vector2D operator +(Vector2D lhs, Vector2D rhs)
        {
            return new Vector2D(lhs.X + rhs.X, lhs.Y + rhs.Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
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

                var r6 = v2 % new Vector2(2, 3);
                equal(1, r6.X);
                equal(1, r6.Y);

                var r7 = v2 & v1;
                equal(11, r7);

                var r8 = new Vector2(3, 4) | new Vector2(2, 0);
                equal(3, r8.X);
                equal(0, r8.Y);

                var r9 = v2 - v1;
                equal(2, r9.X);
                equal(2, r9.Y);
                
                var vSmall = new Vector2(3, 4);
                var vBig = new Vector2(4, 4);

                assert(vSmall < vBig);
                assert(vSmall <= vBig);
                assert(vSmall <= vSmall);
                assert(vBig > vSmall);
                assert(vBig >= vSmall);
                assert(vBig >= vBig);

                assertFalse(vSmall > vSmall);
                assertFalse(vSmall < vSmall);
                assertFalse(vSmall > vBig);
                assertFalse(vSmall >= vBig);
                assertFalse(vBig < vBig);
                assertFalse(vBig > vBig);
                assertFalse(vBig < vSmall);
                assertFalse(vBig <= vSmall);
            ");
    }

    [Fact]
    public void OperatorOverloading_AssignmentOperators()
    {
        RunTest(@"
                var v1 = new Vector2(1, 2);
                var v2 = new Vector2(3, 4);
                var n = 6;

                var r1 = v1;
                r1 += v2;
                equal(4, r1.X);
                equal(6, r1.Y);

                var r2 = n;
                r2 += v1;
                equal(7, r2.X);
                equal(8, r2.Y);

                var r3 = v1;
                r3 += n;
                equal(7, r3.X);
                equal(8, r3.Y);

                var r4 = v1;
                r4 *= n;
                equal(6, r4.X);
                equal(12, r4.Y);

                var r5 = v1;
                r5 /= n;
                equal(1 / 6, r5.X);
                equal(2 / 6, r5.Y);

                var r6 = v2;
                r6 %= new Vector2(2, 3);
                equal(1, r6.X);
                equal(1, r6.Y);

                var r7 = v2;
                r7 &= v1;
                equal(11, r7);

                var r8 = new Vector2(3, 4);
                r8 |= new Vector2(2, 0);
                equal(3, r8.X);
                equal(0, r8.Y);

                var r9 = v2;
                r9 -= v1;
                equal(2, r9.X);
                equal(2, r9.Y);
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

    [Fact]
    public void OperatorOverloading_ShouldWorkOnDerivedClasses()
    {
        RunTest(@"
                var v1 = new Vector2Child(1, 2);
                var v2 = new Vector2Child(3, 4);
                var n = 5;

                var v1v2 = v1 + v2;
                var v1n = v1 + n;

                // Uses the (Vector2 + Vector2) operator on the parent class
                equal(4, v1v2.X);
                equal(6, v1v2.Y);

                // Uses the (Vector2Child + double) operator on the child class
                equal(11, v1n.X);
                equal(12, v1n.Y);
            ");
    }

    [Fact]
    public void OperatorOverloading_ShouldEvaluateOnlyOnce()
    {
        RunTest(@"
                var c;
                var resolve = v => { c++; return v; };

                c = 0;
                var n1 = resolve(1) + 2;
                equal(n1, 3);
                equal(c, 1);

                c = 0;
                var n2 = resolve(2) + resolve(3);
                equal(n2, 5);
                equal(c, 2);

                c = 0;
                var n3 = -resolve(1);
                equal(n3, -1);
                equal(c, 1);
            ");
    }

    [Fact]
    public void ShouldAllowStringConcatenateForOverloaded()
    {
        var engine = new Engine(cfg => cfg.AllowOperatorOverloading());
        engine.SetValue("Vector2D", TypeReference.CreateTypeReference<Vector2D>(engine));
        engine.SetValue("log", new Action<object>(Console.WriteLine));

        engine.Evaluate("let v1 = new Vector2D(1, 2);");
        Assert.Equal("(1, 2)", engine.Evaluate("new String(v1)").As<StringInstance>().StringData.ToString());
        Assert.Equal("### (1, 2) ###", engine.Evaluate("'### ' + v1 + ' ###'"));
    }
}
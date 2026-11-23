#nullable enable

using System.Numerics;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the rule that decides when <c>Options.Interop.AllowOperatorOverloading</c> may select a CLR operator: at
/// least one operand must be a host object, a CLR object projected into script (<c>IObjectWrapper</c>). A
/// primitive, or an object the engine itself created, is never offered to the lookup on its own, so it keeps its
/// ECMAScript meaning instead of selecting an operator of the CLR value it is stored as - a BigInt is a
/// <see cref="BigInteger"/>, a string a <see cref="string"/>, a Date a <see cref="DateTime"/>.
/// </summary>
/// <remarks>
/// <para>The expected strings were taken from Node 26 running the same expressions in script scope
/// (<c>vm.runInThisContext</c>), and every row must give the same answer with overloading on and off: unary
/// minus and bitwise NOT (https://tc39.es/ecma262/#sec-unary-minus-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-bitwise-not-operator-runtime-semantics-evaluation), the binary and compound
/// assignment operators (https://tc39.es/ecma262/#sec-applystringornumericbinaryoperator), which throw a
/// <c>TypeError</c> for a BigInt mixed with a Number, and the comparisons
/// (https://tc39.es/ecma262/#sec-islessthan, https://tc39.es/ecma262/#sec-islooselyequal), which compare a
/// BigInt with a Number or a string by mathematical value and two objects by identity. <c>typeof</c> a BigInt
/// result is <c>bigint</c> (https://tc39.es/ecma262/#sec-typeof-operator).</para>
/// <para>Before the rule, with overloading on, BigInt arithmetic returned the CLR <see cref="BigInteger"/> wrapped
/// as an object, <c>5n + 1</c> and <c>+5n</c> did not throw, <c>5n * 1.5</c> was <c>5</c>, the difference of
/// two Dates was a <see cref="TimeSpan"/>, two String objects with the same contents were loosely equal, and
/// comparing two symbols threw <see cref="ArgumentException"/> out of <c>Evaluate</c>.</para>
/// </remarks>
public class OperatorOverloadingPrimitiveOperandTests
{
    public enum Scope
    {
        TopLevel,
        Function,
        Generator,
        AsyncFunction,
    }

    private const string Prelude =
        "var a = 5n, b = 3n, c = 5n, s = Symbol.iterator, d1 = new Date(86400000), d2 = new Date(0), d3 = new Date(86400000);";

    /// <summary>The expression, and Node's <c>typeof r + ' ' + String(r)</c> for it, or the error it throws.</summary>
    private static readonly (string Expression, string Expected)[] Rows =
    [
        // unary, on a BigInt
        ("-a", "bigint -5"),
        ("~a", "bigint -6"),
        ("+a", "TypeError"),

        // binary, two BigInts
        ("a + 1n", "bigint 6"),
        ("a - b", "bigint 2"),
        ("a * 2n", "bigint 10"),
        ("a / b", "bigint 1"),
        ("a % b", "bigint 2"),
        ("a ** b", "bigint 125"),
        ("a & b", "bigint 1"),
        ("a | b", "bigint 7"),
        ("a ^ b", "bigint 6"),
        ("a << 2n", "bigint 20"),
        ("a >> 1n", "bigint 2"),
        ("a >>> 1n", "TypeError"),

        // comparisons, BigInt with BigInt, Number and string
        ("a < b", "boolean false"),
        ("a > b", "boolean true"),
        ("a <= b", "boolean false"),
        ("a >= b", "boolean true"),
        ("a == 5n", "boolean true"),
        ("a != b", "boolean true"),
        ("a == 5", "boolean true"),
        ("a < 5.5", "boolean true"),
        ("a == '5'", "boolean true"),
        ("a < '6'", "boolean true"),
        ("'4' > a", "boolean false"),

        // a BigInt mixed with a Number throws; with a string it concatenates
        ("a + 1", "TypeError"),
        ("1 - a", "TypeError"),
        ("a * 1.5", "TypeError"),
        ("a + 'x'", "string 5x"),
        ("'x' + a", "string x5"),

        // compound assignment
        ("c += 1n", "bigint 6"),
        ("c -= 1n", "bigint 4"),
        ("c *= 2n", "bigint 10"),
        ("c /= 2n", "bigint 2"),
        ("c %= 3n", "bigint 2"),
        ("c &= 3n", "bigint 1"),
        ("c |= 2n", "bigint 7"),
        ("c ^= 1n", "bigint 4"),
        ("c <<= 1n", "bigint 10"),
        ("c >>= 1n", "bigint 2"),
        ("c >>>= 1n", "TypeError"),
        ("c += 1", "TypeError"),

        // other primitives
        ("'1.0' == 1", "boolean true"),
        ("'a' == 'a'", "boolean true"),
        ("'a' != 'b'", "boolean true"),
        ("'abc' < 'abd'", "boolean true"),
        ("true + 1", "number 2"),
        ("null + 1", "number 1"),
        ("1.5 + 2", "number 3.5"),
        ("-1.5", "number -1.5"),
        ("~5", "number -6"),
        ("s == s", "boolean true"),
        ("s != Symbol.iterator", "boolean false"),

        // objects the engine created, whose CLR form has operators of its own
        ("d1 - d2", "number 86400000"),
        ("d1 == d3", "boolean false"),
        ("d1 > d2", "boolean true"),
        ("new String('a') == new String('a')", "boolean false"),
        ("new String('a') == 'a'", "boolean true"),
        ("Object(5n) + 1n", "bigint 6"),
        ("-Object(5n)", "bigint -5"),
        ("Object(5n) == 5n", "boolean true"),
    ];

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var operatorOverloading in new[] { false, true })
        {
            foreach (var scope in (Scope[]) Enum.GetValues(typeof(Scope)))
            {
                foreach (var (expression, expected) in Rows)
                {
                    yield return new TestCaseData(scope, expression, expected, operatorOverloading)
                        .SetArgDisplayNames(scope.ToString(), expression, operatorOverloading ? "OperatorOverloading" : "Default");
                }
            }
        }
    }

    /// <summary>
    /// Runs the expression twice, so a cache filled by the first evaluation is read by the second. In the generator
    /// and async scopes the expression runs after a resume.
    /// </summary>
    private static string BuildScript(Scope scope, string expression)
    {
        var pause = scope switch
        {
            Scope.Generator => "yield i, ",
            Scope.AsyncFunction => "await i, ",
            _ => "",
        };

        var loop = "for (var i = 0; i < 2; i++) { " + Prelude + " var r; "
            + "try { r = (" + pause + expression + "); out.push(typeof r + ' ' + String(r)); } "
            + "catch (e) { out.push(e.constructor.name); } }";
        const string Finish = "done = out.join('|');";

        return "var out = [], done;\n" + scope switch
        {
            Scope.TopLevel => loop + "\n" + Finish + "\n",
            Scope.Function => "(function () { " + loop + " })();\n" + Finish + "\n",
            Scope.Generator => "function* g() { " + loop + " }\nfor (var it = g(), st = it.next(); !st.done; st = it.next()) {}\n" + Finish + "\n",
            _ => "async function f() { " + loop + " }\nf().then(function () { " + Finish + " });\n",
        };
    }

    [TestCaseSource(nameof(Cases))]
    public void PrimitiveOperandsKeepTheirEcmaScriptMeaning(Scope scope, string expression, string expected, bool operatorOverloading)
    {
        var script = BuildScript(scope, expression);

        using var engine = new Engine(options => options.Interop.AllowOperatorOverloading = operatorOverloading);
        engine.Execute(script);

        engine.GetValue("done").AsString().Should().Be(expected + "|" + expected, script);
    }

    /// <summary>
    /// A host type with operators of its own. One operand being a host object is enough: a JS number, and a BigInt,
    /// are still offered to its operators as the other operand.
    /// </summary>
    public sealed class Money : IEquatable<Money>
    {
        public Money(long cents) => Cents = cents;

        public long Cents { get; }

        public static Money operator +(Money left, Money right) => new(left.Cents + right.Cents);
        public static Money operator +(Money left, double right) => new(left.Cents + (long) right);
        public static Money operator +(double left, Money right) => new((long) left + right.Cents);
        public static Money operator *(Money left, BigInteger right) => new(left.Cents * (long) right);
        public static Money operator -(Money operand) => new(-operand.Cents);
        public static Money operator ~(Money operand) => new(~operand.Cents);
        public static bool operator <(Money left, Money right) => left.Cents < right.Cents;
        public static bool operator >(Money left, Money right) => left.Cents > right.Cents;
        public static bool operator ==(Money? left, Money? right) => left?.Cents == right?.Cents;
        public static bool operator !=(Money? left, Money? right) => !(left == right);

        public bool Equals(Money? other) => other is not null && other.Cents == Cents;
        public override bool Equals(object? obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => Cents.GetHashCode();
    }

    [TestCase("(m1 + m2).Cents", 350)]
    [TestCase("(m1 + 1).Cents", 101)]
    [TestCase("(1 + m1).Cents", 101)]
    [TestCase("(m1 * 3n).Cents", 300)]
    [TestCase("(-m1).Cents", -100)]
    [TestCase("(~m1).Cents", -101)]
    [TestCase("(function () { var m = m1; m += m2; return m.Cents; })()", 350)]
    [TestCase("(function () { var m = m1; m += 5; return m.Cents; })()", 105)]
    [TestCase("(function () { var it = (function* () { var m = yield 0; return (m + m2).Cents; })(); it.next(); return it.next(m1).value; })()", 350)]
    public void HostOperatorsStillApply(string expression, long expected)
    {
        using var engine = CreateHostEngine();

        engine.Evaluate(expression).AsNumber().Should().Be(expected);
    }

    [TestCase("m1 < m2", true)]
    [TestCase("m1 > m2", false)]
    [TestCase("m1 == m3", true)]
    [TestCase("m1 != m3", false)]
    public void HostComparisonOperatorsStillApply(string expression, bool expected)
    {
        using var engine = CreateHostEngine();

        engine.Evaluate(expression).AsBoolean().Should().Be(expected);
    }

    [Test]
    public void HostOperatorsStillApplyAfterAResume()
    {
        using var engine = CreateHostEngine();

        var result = engine.Evaluate("(async () => { var m = await m1; m += await m2; return (-(m + 1)).Cents; })()").UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsNumber().Should().Be(-351);
    }

    private static Engine CreateHostEngine()
    {
        var engine = new Engine(options => options.Interop.AllowOperatorOverloading = true);
        engine.SetValue("m1", new Money(100));
        engine.SetValue("m2", new Money(250));
        engine.SetValue("m3", new Money(100));
        return engine;
    }
}

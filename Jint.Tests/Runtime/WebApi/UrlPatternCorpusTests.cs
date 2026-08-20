#if NET8_0_OR_GREATER
#nullable enable

using System.IO;
using System.Text;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Web Platform Tests <c>URLPattern</c> corpus, run through the JavaScript surface.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="UrlCorpusTests"/>, which drives the engine-free URL parser directly, these rows assert
/// things only the <c>URLPattern</c> <i>object</i> has: the normalized pattern string each of its eight
/// accessors returns, the truthiness of <c>test()</c>, and the exact shape of the <c>exec()</c> result down to
/// which group names exist and which of them are <c>undefined</c>. So the driver is a port of WPT's own
/// <c>urlpattern/resources/urlpatterntests.js</c>, written in JavaScript and run on one engine for the whole
/// corpus — including its rule for deriving an expected pattern string when a row does not spell one out, which
/// is where most of its coverage of the constructor-string shorthand lives.
/// </para>
/// <para>
/// <b>The exclusion table is the whole point of the driver.</b> A row that does not pass has to be named in it
/// with the divergence category it belongs to, and a row that <i>does</i> pass while named there fails the run
/// as a stale exclusion — the same discipline the test262 harness and the URL corpus use, so a fix cannot
/// silently leave a permanent exemption behind.
/// </para>
/// </remarks>
public class UrlPatternCorpusTests
{
    /// <summary>
    /// The divergence categories a failing row may be filed under. The enum is deliberately declared even while
    /// the table below is empty: a row that starts failing after a corpus bump has to be classified, not merely
    /// listed.
    /// </summary>
    private enum Divergence
    {
        /// <summary>
        /// The outcome depends on the platform's Unicode tables — the IDNA mapping behind a hostname pattern, or
        /// the general categories behind a group name's <c>ID_Start</c> / <c>ID_Continue</c> derivation.
        /// </summary>
        UnicodeVersion,

        /// <summary>
        /// The row exercises a corner of the "<c>v</c>"-flag regular expression grammar the engine's own
        /// <c>RegExp</c> implementation does not reach.
        /// </summary>
        RegExpEngine,
    }

    /// <summary>
    /// Rows that do not pass, keyed by the WPT test name — <c>Pattern: &lt;json&gt; Inputs: &lt;json&gt;</c>,
    /// exactly as the upstream runner builds it.
    /// </summary>
    /// <remarks>
    /// Empty, and meant to stay that way: every row of the corpus passes.
    /// </remarks>
    private static readonly Dictionary<string, Divergence> _exclusions = new(StringComparer.Ordinal);

    [Fact]
    public void MatchesEveryRowOfTheWebPlatformTestsCorpus()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));
        engine.SetValue("__corpus", LoadCorpus());

        var report = engine.Evaluate(Driver).AsObject();

        var checkedRows = (int) report.Get("total").AsNumber();

        // The corpus was actually run: a resource that silently stopped being embedded, or a driver that threw
        // its rows away, would otherwise be an empty green test.
        checkedRows.Should().BeGreaterThan(350);

        var failures = new List<string>();
        var failedNames = new HashSet<string>(StringComparer.Ordinal);
        var reported = report.Get("failures").AsArray();

        for (uint i = 0; i < reported.Length; i++)
        {
            var failure = reported[i].AsObject();
            var name = failure.Get("name").AsString();
            failedNames.Add(name);

            if (!_exclusions.ContainsKey(name))
            {
                failures.Add(name + ": " + failure.Get("message").AsString());
            }
        }

        var stale = new List<string>();
        foreach (var excluded in _exclusions.Keys)
        {
            if (!failedNames.Contains(excluded))
            {
                stale.Add(excluded);
            }
        }

        var message = new StringBuilder();

        if (stale.Count > 0)
        {
            message.Append(stale.Count).AppendLine(" stale exclusion(s) — these rows pass and must be removed from the exclusion table:");
            foreach (var row in stale)
            {
                message.Append("  ").AppendLine(row);
            }
        }

        if (failures.Count > 0)
        {
            message.Append(failures.Count).AppendLine(" failing row(s):");
            foreach (var row in failures)
            {
                message.Append("  ").AppendLine(row);
            }
        }

        message.ToString().Should().BeEmpty();
    }

    private static string LoadCorpus()
    {
        var assembly = typeof(UrlPatternCorpusTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("urlpatterntestdata.json", StringComparison.Ordinal));
        name.Should().NotBeNull("the urlpatterntestdata.json corpus must be embedded");

        using var stream = assembly.GetManifestResourceStream(name!)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// A port of <c>urlpattern/resources/urlpatterntests.js</c> at the pinned commit. The assertions report into
    /// a failure list instead of throwing, so one run reports every failing row rather than only the first, and
    /// the test names it builds are the ones the exclusion table is keyed on.
    /// </summary>
    private const string Driver = """
        (function () {
          const kComponents = [
            'protocol', 'username', 'password', 'hostname',
            'port', 'pathname', 'search', 'hash',
          ];

          const data = JSON.parse(__corpus);
          const failures = [];
          let total = 0;

          function describe(value) {
            if (value === undefined) return 'undefined';
            return JSON.stringify(value);
          }

          function threw(fn) {
            try {
              fn();
              return null;
            } catch (e) {
              return e;
            }
          }

          for (const entry of data) {
            if (typeof entry !== 'object' || entry === null || !('pattern' in entry)) continue;

            total++;
            const name = `Pattern: ${JSON.stringify(entry.pattern)} Inputs: ${JSON.stringify(entry.inputs)}`;
            const problems = [];

            const check = (actual, expected, what) => {
              if (!Object.is(actual, expected)) {
                problems.push(`${what}: expected ${describe(expected)} but was ${describe(actual)}`);
              }
            };

            const checkObject = (actual, expected, what) => {
              if (actual === null || typeof actual !== 'object') {
                problems.push(`${what}: expected an object but was ${describe(actual)}`);
                return;
              }
              const actualKeys = Object.keys(actual).sort();
              const expectedKeys = Object.keys(expected).sort();
              check(actualKeys.join(','), expectedKeys.join(','), `${what} keys`);
              for (const key of expectedKeys) {
                const a = actual[key];
                const e = expected[key];
                if (e !== null && typeof e === 'object') {
                  checkObject(a, e, `${what}.${key}`);
                } else {
                  check(a, e, `${what}.${key}`);
                }
              }
            };

            let pattern = null;
            const constructionError = threw(() => { pattern = new URLPattern(...entry.pattern); });

            if (entry.expected_obj === 'error') {
              if (!(constructionError instanceof TypeError)) {
                problems.push(`constructor: expected a TypeError but got ${describe(String(constructionError))}`);
              }
              if (problems.length > 0) failures.push({ name, message: problems.join('; ') });
              continue;
            }

            if (constructionError !== null) {
              failures.push({ name, message: `constructor threw ${String(constructionError)}` });
              continue;
            }

            const expectedObj = entry.expected_obj || {};

            for (const component of kComponents) {
              let expected = expectedObj[component];

              if (expected === undefined) {
                let baseURL = null;
                if (entry.pattern.length > 0 && entry.pattern[0].baseURL) {
                  baseURL = new URL(entry.pattern[0].baseURL);
                } else if (entry.pattern.length > 1 && typeof entry.pattern[1] === 'string') {
                  baseURL = new URL(entry.pattern[1]);
                }

                const EARLIER_COMPONENTS = {
                  protocol: [],
                  hostname: ['protocol'],
                  port: ['protocol', 'hostname'],
                  username: [],
                  password: [],
                  pathname: ['protocol', 'hostname', 'port'],
                  search: ['protocol', 'hostname', 'port', 'pathname'],
                  hash: ['protocol', 'hostname', 'port', 'pathname', 'search'],
                };

                if (entry.exactly_empty_components && entry.exactly_empty_components.includes(component)) {
                  expected = '';
                } else if (typeof entry.pattern[0] === 'object' && entry.pattern[0][component]) {
                  expected = entry.pattern[0][component];
                } else if (typeof entry.pattern[0] === 'object' &&
                           EARLIER_COMPONENTS[component].some(c => c in entry.pattern[0])) {
                  expected = '*';
                } else if (baseURL && component !== 'username' && component !== 'password') {
                  let baseValue = baseURL[component];
                  if (component === 'protocol') baseValue = baseValue.substring(0, baseValue.length - 1);
                  else if (component === 'search' || component === 'hash') baseValue = baseValue.substring(1);
                  expected = baseValue;
                } else {
                  expected = '*';
                }
              }

              check(pattern[component], expected, `compiled pattern property '${component}'`);
            }

            if (entry.expected_match === 'error') {
              const testError = threw(() => pattern.test(...entry.inputs));
              if (!(testError instanceof TypeError)) {
                problems.push(`test(): expected a TypeError but got ${describe(String(testError))}`);
              }
              const execError = threw(() => pattern.exec(...entry.inputs));
              if (!(execError instanceof TypeError)) {
                problems.push(`exec(): expected a TypeError but got ${describe(String(execError))}`);
              }
              if (problems.length > 0) failures.push({ name, message: problems.join('; ') });
              continue;
            }

            check(pattern.test(...entry.inputs), !!entry.expected_match, 'test() result');

            const execResult = pattern.exec(...entry.inputs);

            if (!entry.expected_match || typeof entry.expected_match !== 'object') {
              check(execResult, entry.expected_match, 'exec() failed match result');
              if (problems.length > 0) failures.push({ name, message: problems.join('; ') });
              continue;
            }

            if (execResult === null) {
              failures.push({ name, message: 'exec() returned null but a match was expected' });
              continue;
            }

            const expectedInputs = entry.expected_match.inputs || entry.inputs;
            check(execResult.inputs.length, expectedInputs.length, 'exec() result.inputs.length');
            for (let i = 0; i < execResult.inputs.length; ++i) {
              const input = execResult.inputs[i];
              const expectedInput = expectedInputs[i];
              if (typeof input === 'string') {
                check(input, expectedInput, `exec() result.inputs[${i}]`);
                continue;
              }
              for (const component of kComponents) {
                check(input[component], expectedInput[component], `exec() result.inputs[${i}][${component}]`);
              }
            }

            for (const component of kComponents) {
              let expectedComponent = entry.expected_match[component];

              if (!expectedComponent) {
                expectedComponent = { input: '', groups: {} };
                if (!entry.exactly_empty_components || !entry.exactly_empty_components.includes(component)) {
                  expectedComponent.groups['0'] = '';
                }
              }

              // JSON cannot carry undefined, so the data file writes null where a group did not participate.
              for (const key in expectedComponent.groups) {
                if (expectedComponent.groups[key] === null) {
                  expectedComponent.groups[key] = undefined;
                }
              }

              checkObject(execResult[component], expectedComponent, `exec() result for ${component}`);
            }

            if (problems.length > 0) failures.push({ name, message: problems.join('; ') });
          }

          return { total, failures };
        })();
        """;
}
#endif

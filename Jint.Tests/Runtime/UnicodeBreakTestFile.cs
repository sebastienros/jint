#nullable enable

using System.Globalization;
using System.Text;

namespace Jint.Tests.Runtime;

/// <summary>
/// The conformance tests of UAX #29 for Unicode 17.0.0 (https://www.unicode.org/Public/17.0.0/ucd/auxiliary/), vendored
/// under tools/text-segmentation/ and embedded by Jint.Tests.csproj: each line is a string of code points with every
/// position between them marked, ÷ for a boundary and × for none.
/// </summary>
internal static class UnicodeBreakTestFile
{
    /// <summary>
    /// Runs every line of the embedded <paramref name="resourceName"/>, whose first line must be <paramref name="header"/>,
    /// through <c>Intl.Segmenter</c> with <paramref name="granularity"/>, and returns how many lines there are and the
    /// ones whose boundaries differ, each with the boundaries found.
    /// </summary>
    internal static (int Cases, List<string> Failures) Run(string resourceName, string header, string granularity)
    {
        var engine = new Engine();
        engine.SetValue("granularity", granularity);
        engine.Execute("""
            const segmenter = new Intl.Segmenter('en', { granularity });
            function boundaries(s) {
                const result = [...segmenter.segment(s)].map(x => x.index);
                result.push(s.length);
                return result.join();
            }
            """);

        var cases = 0;
        var failures = new List<string>();
        using var stream = typeof(UnicodeBreakTestFile).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(resourceName + " is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        reader.ReadLine().Should().Be(header);
        while (reader.ReadLine() is { } line)
        {
            var hash = line.IndexOf('#');
            var data = (hash < 0 ? line : line.Substring(0, hash)).Trim();
            if (data.Length == 0)
            {
                continue;
            }

            var input = new StringBuilder();
            var expected = new List<int>();
            foreach (var token in data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "\u00F7")
                {
                    expected.Add(input.Length);
                }
                else if (token != "\u00D7")
                {
                    input.Append(char.ConvertFromUtf32(int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                }
            }

            cases++;
            var actual = engine.Invoke("boundaries", input.ToString()).AsString();
            if (actual != string.Join(",", expected))
            {
                failures.Add(data + "  =>  " + actual);
            }
        }

        return (cases, failures);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Jint.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Jint.Tests.Test262
{
    public abstract class Test262Test
    {
        private static readonly string[] Sources;

        private static readonly string BasePath;

        private static readonly TimeZoneInfo _pacificTimeZone;

        private static readonly Dictionary<string, string> _skipReasons =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _strictSkips =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static Test262Test()
        {
            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;

            string[] files =
            {
                @"harness\sta.js",
                @"harness\assert.js",
                @"harness\propertyHelper.js",
                @"harness\compareArray.js",
                @"harness\decimalToHexString.js",
            };

            Sources = new string[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                Sources[i] = File.ReadAllText(Path.Combine(BasePath, files[i]));
            }

            var content = File.ReadAllText(Path.Combine(BasePath, "test/skipped.json"));
            var doc = JArray.Parse(content);
            foreach (var entry in doc.Values<JObject>())
            {
                var source = entry["source"].Value<string>();
                _skipReasons[source] = entry["reason"].Value<string>();
                if (entry.TryGetValue("mode", out var mode) && mode.Value<string>() == "strict")
                {
                    _strictSkips.Add(source);
                }
            }
        }

        protected void RunTestCode(string code, bool strict)
        {
            var engine = new Engine(cfg => cfg
                .LocalTimeZone(_pacificTimeZone)
                .Strict(strict));

            for (int i = 0; i < Sources.Length; ++i)
            {
                engine.Execute(Sources[i]);
            }

            string lastError = null;

            bool negative = code.IndexOf("negative:", StringComparison.Ordinal) > -1;
            try
            {
                engine.Execute(code);
            }
            catch (JavaScriptException j)
            {
                lastError = TypeConverter.ToString(j.Error);
            }
            catch (Exception e)
            {
                lastError = e.ToString();
            }

            if (negative)
            {
                Assert.NotNull(lastError);
            }
            else
            {
                Assert.Null(lastError);
            }
        }

        protected void RunTestInternal(SourceFile sourceFile)
        {
            if (sourceFile.Skip)
            {
                return;
            }

            if (sourceFile.Code.IndexOf("onlyStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(sourceFile.Code, strict: false);
            }

            if (!_strictSkips.Contains(sourceFile.Source)
                && sourceFile.Code.IndexOf("noStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(sourceFile.Code, strict: true);
            }
        }

        public static IEnumerable<object[]> SourceFiles(string pathPrefix, bool skipped)
        {
            var results = new List<object[]>();
            var fixturesPath = Path.Combine(BasePath, "test");
            var searchPath = Path.Combine(fixturesPath, pathPrefix);
            var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var name = file.Substring(fixturesPath.Length + 1).Replace("\\", "/");
                bool skip = _skipReasons.TryGetValue(name, out var reason);

                var code = skip ? "" : File.ReadAllText(file);

                var flags = Regex.Match(code, "flags: \\[(.+?)\\]");
                if (flags.Success)
                {
                    var items = flags.Groups[1].Captures[0].Value.Split(",");
                    foreach (var item in items.Select(x => x.Trim()))
                    {
                        switch (item)
                        {
                            // TODO implement
                            case "async":
                                skip = true;
                                reason = "async not implemented";
                                break;
                        }
                    }
                }

                var features = Regex.Match(code, "features: \\[(.+?)\\]");
                if (features.Success)
                {
                    var items = features.Groups[1].Captures[0].Value.Split(",");
                    foreach (var item in items.Select(x => x.Trim()))
                    {
                        switch (item)
                        {
                            // TODO implement
                            case "cross-realm":
                                skip = true;
                                reason = "realms not implemented";
                                break;
                            case "tail-call-optimization":
                                skip = true;
                                reason = "tail-calls not implemented";
                                break;
                            case "class":
                                skip = true;
                                reason = "class keyword not implemented";
                                break;
                            case "Symbol.species":
                                skip = true;
                                reason = "Symbol.species not implemented";
                                break;
                            case "Proxy":
                                skip = true;
                                reason = "Proxies not implemented";
                                break;
                            case "object-spread":
                                skip = true;
                                reason = "Object spread not implemented";
                                break;
                            case "Symbol.unscopables":
                                skip = true;
                                reason = "Symbol.unscopables not implemented";
                                break;
                            case "Symbol.match":
                                skip = true;
                                reason = "Symbol.match not implemented";
                                break;
                            case "Symbol.matchAll":
                                skip = true;
                                reason = "Symbol.matchAll not implemented";
                                break;
                            case "Symbol.split":
                                skip = true;
                                reason = "Symbol.split not implemented";
                                break;
                            case "String.prototype.matchAll":
                                skip = true;
                                reason = "proposal stage";
                                break;
                            case "Symbol.search":
                                skip = true;
                                reason = "Symbol.search not implemented";
                                break;
                            case "Symbol.replace":
                                skip = true;
                                reason = "Symbol.replace not implemented";
                                break;
                            case "Symbol.toStringTag":
                                skip = true;
                                reason = "Symbol.toStringTag not implemented";
                                break;
                            case "BigInt":
                                skip = true;
                                reason = "BigInt not implemented";
                                break;
                            case "generators":
                                skip = true;
                                reason = "generators not implemented";
                                break;
                            case "let":
                                skip = true;
                                reason = "let not implemented";
                                break;
                            case "async-functions":
                                skip = true;
                                reason = "async-functions not implemented";
                                break;
                            case "async-iteration":
                                skip = true;
                                reason = "async not implemented";
                                break;
                            case "new.target":
                                skip = true;
                                reason = "MetaProperty not implemented";
                                break;
                            case "super":
                                skip = true;
                                reason = "super not implemented";
                                break;
                        }
                    }
                }

                if (code.IndexOf("SpecialCasing.txt") > -1)
                {
                    skip = true;
                    reason = "SpecialCasing.txt not implemented";
                }

                if (name.StartsWith("language/expressions/object/dstr-async-gen-meth-"))
                {
                    skip = true;
                    reason = "Esprima problem, Unexpected token *";
                }

                if (file.EndsWith("tv-line-continuation.js")
                    || file.EndsWith("tv-line-terminator-sequence.js")
                    || file.EndsWith("special-characters.js"))
                {
                    // LF endings required
                    code = code.Replace("\r\n", "\n");
                }

                var sourceFile = new SourceFile(
                    name,
                    file,
                    skip,
                    reason,
                    code);

                if (skipped == sourceFile.Skip)
                {
                    results.Add(new object[]
                    {
                        sourceFile
                    });
                }
            }

            return results;
        }
    }

    public class SourceFile : IXunitSerializable
    {
        public SourceFile()
        {

        }

        public SourceFile(
            string source,
            string fullPath,
            bool skip,
            string reason,
            string code)
        {
            Skip = skip;
            Source = source;
            Reason = reason;
            FullPath = fullPath;
            Code = code;
        }

        public string Source { get; set; }
        public bool Skip { get; set; }
        public string Reason { get; set; }
        public string FullPath { get; set; }
        public string Code { get; set; }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Skip = info.GetValue<bool>(nameof(Skip));
            Source = info.GetValue<string>(nameof(Source));
            Reason = info.GetValue<string>(nameof(Reason));
            FullPath = info.GetValue<string>(nameof(FullPath));
            Code = info.GetValue<string>(nameof(Code));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Skip), Skip);
            info.AddValue(nameof(Source), Source);
            info.AddValue(nameof(Reason), Reason);
            info.AddValue(nameof(FullPath), FullPath);
            info.AddValue(nameof(Code), Code);
        }

        public override string ToString()
        {
            return Source;
        }
    }
}
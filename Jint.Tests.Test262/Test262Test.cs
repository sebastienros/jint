using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Jint.Tests.Test262
{
    public abstract class Test262Test
    {
        private static readonly Dictionary<string, Script> Sources;

        private static readonly string BasePath;

        private static readonly TimeZoneInfo _pacificTimeZone;

        private static readonly Dictionary<string, string> _skipReasons = new(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _strictSkips = new(StringComparer.OrdinalIgnoreCase);

        static Test262Test()
        {
            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            try
            {
                _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // https://stackoverflow.com/questions/47848111/how-should-i-fetch-timezoneinfo-in-a-platform-agnostic-way
                // should be natively supported soon https://github.com/dotnet/runtime/issues/18644
                _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            }

            var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.Location).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;

            string[] files =
            {
                "sta.js",
                "assert.js",
                "arrayContains.js",
                "isConstructor.js",
                "promiseHelper.js",
                "propertyHelper.js",
                "compareArray.js",
                "decimalToHexString.js",
                "proxyTrapsHelper.js",
                "dateConstants.js",
                "assertRelativeDateMs.js",
                "regExpUtils.js",
                "nans.js",
                "compareIterator.js",
                "nativeFunctionMatcher.js",
                "wellKnownIntrinsicObjects.js",
                "fnGlobalObject.js",
                "testTypedArray.js",
                "detachArrayBuffer.js",
                "byteConversionValues.js"
            };

            Sources = new Dictionary<string, Script>(files.Length);
            for (var i = 0; i < files.Length; i++)
            {
                var source = File.ReadAllText(Path.Combine(BasePath, "harness", files[i]));
                Sources[files[i]] = new JavaScriptParser(source, new ParserOptions(files[i])).ParseScript();
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

        protected void RunTestCode(string fileName, string code, bool strict)
        {
            var engine = new Engine(cfg => cfg
                .LocalTimeZone(_pacificTimeZone)
                .Strict(strict)
            );

            engine.Execute(Sources["sta.js"]);
            engine.Execute(Sources["assert.js"]);
            engine.SetValue("print",
                new ClrFunctionInstance(engine, "print", (thisObj, args) => TypeConverter.ToString(args.At(0))));

            var o = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
            o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunctionInstance(engine, "evalScript",
                (thisObj, args) =>
                {
                    if (args.Length > 1)
                    {
                        throw new Exception("only script parsing supported");
                    }

                    var options = new ParserOptions {AdaptRegexp = true, Tolerant = false};
                    var parser = new JavaScriptParser(args.At(0).AsString(), options);
                    var script = parser.ParseScript(strict);

                    return engine.Evaluate(script);
                }), true, true, true));

            o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunctionInstance(engine, "createRealm",
                (thisObj, args) =>
                {
                    var realm = engine._host.CreateRealm();
                    realm.GlobalObject.Set("global", realm.GlobalObject);
                    return realm.GlobalObject;
                }), true, true, true));

            o.FastSetProperty("detachArrayBuffer", new PropertyDescriptor(new ClrFunctionInstance(engine, "detachArrayBuffer",
                (thisObj, args) =>
                {
                    var buffer = (ArrayBufferInstance) args.At(0);
                    buffer.DetachArrayBuffer();
                    return JsValue.Undefined;
                }), true, true, true));

            engine.SetValue("$262", o);

            var includes = Regex.Match(code, @"includes: \[(.+?)\]");
            if (includes.Success)
            {
                var files = includes.Groups[1].Captures[0].Value.Split(',');
                foreach (var file in files)
                {
                    engine.Execute(Sources[file.Trim()]);
                }
            }

            if (code.IndexOf("propertyHelper.js", StringComparison.OrdinalIgnoreCase) != -1)
            {
                engine.Execute(Sources["propertyHelper.js"]);
            }

            string lastError = null;

            bool negative = code.IndexOf("negative:", StringComparison.Ordinal) > -1;
            try
            {
                engine.Execute(new JavaScriptParser(code, new ParserOptions(fileName)).ParseScript());
            }
            catch (JavaScriptException j)
            {
                lastError = j.ToString();
            }
            catch (Exception e)
            {
                lastError = e.ToString();
            }

            if (!negative && !string.IsNullOrWhiteSpace(lastError))
            {
                throw new XunitException(lastError);
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
                RunTestCode(sourceFile.Source, sourceFile.Code, strict: false);
            }

            if (!_strictSkips.Contains(sourceFile.Source)
                && sourceFile.Code.IndexOf("noStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(sourceFile.Source, sourceFile.Code, strict: true);
            }
        }

        public static IEnumerable<object[]> SourceFiles(string pathPrefix, bool skipped)
        {
            var results = new ConcurrentBag<object[]>();
            var fixturesPath = Path.Combine(BasePath, "test");
            var segments = pathPrefix.Split('\\');
            var searchPath = Path.Combine(fixturesPath, Path.Combine(segments));
            var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var name = file.Substring(fixturesPath.Length + 1).Replace("\\", "/");
                bool skip = _skipReasons.TryGetValue(name, out var reason);

                var code = skip ? "" : File.ReadAllText(file);

                var flags = Regex.Match(code, "flags: \\[(.+?)\\]");
                if (flags.Success)
                {
                    var items = flags.Groups[1].Captures[0].Value.Split(',');
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
                    var items = features.Groups[1].Captures[0].Value.Split(',');
                    foreach (var item in items.Select(x => x.Trim()))
                    {
                        switch (item)
                        {
                            // TODO implement
                            case "tail-call-optimization":
                                skip = true;
                                reason = "tail-calls not implemented";
                                break;
                            case "BigInt":
                                skip = true;
                                reason = "BigInt not implemented";
                                break;
                            case "generators":
                                skip = true;
                                reason = "generators not implemented";
                                break;
                            case "async-functions":
                                skip = true;
                                reason = "async-functions not implemented";
                                break;
                            case "async-iteration":
                                skip = true;
                                reason = "async not implemented";
                                break;
                            case "class-fields-private":
                            case "class-fields-public":
                                skip = true;
                                reason = "private/public class fields not implemented in esprima";
                                break;
                            case "super":
                                skip = true;
                                reason = "super not implemented";
                                break;
                            case "String.prototype.replaceAll":
                                skip = true;
                                reason = "not in spec yet";
                                break;
                            case "u180e":
                                skip = true;
                                reason = "unicode/regexp not implemented";
                                break;
                            case "regexp-match-indices":
                                skip = true;
                                reason = "regexp-match-indices not implemented";
                                break;
                            case "regexp-named-groups":
                                skip = true;
                                reason = "regexp-named-groups not implemented";
                                break;
                            case "regexp-lookbehind":
                                skip = true;
                                reason = "regexp-lookbehind not implemented";
                                break;
                            case "SharedArrayBuffer":
                                skip = true;
                                reason = "SharedArrayBuffer not implemented";
                                break;
                            case "resizable-arraybuffer":
                                skip = true;
                                reason = "resizable-arraybuffer not implemented";
                                break;
                            case "TypedArray.prototype.at":
                                skip = true;
                                reason = "TypedArray.prototype.at not implemented";
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

                if (name.StartsWith("built-ins/RegExp/property-escapes/generated/"))
                {
                    skip = true;
                    reason = "Esprima problem, Invalid regular expression";
                }

                if (name.StartsWith("built-ins/RegExp/unicode_"))
                {
                    skip = true;
                    reason = "Unicode support and its special cases need more work";
                }

                if (name.StartsWith("built-ins/RegExp/CharacterClassEscapes/"))
                {
                    skip = true;
                    reason = "for-of not implemented";
                }

                // Promises
                if (name.StartsWith("built-ins/Promise/allSettled") ||
                    name.StartsWith("built-ins/Promise/any"))
                {
                    skip = true;
                    reason = "Promise.any and Promise.allSettled are not implemented yet";
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

        private static ParserOptions CreateParserOptions(string fileName) =>
            new ParserOptions(fileName)
            {
                AdaptRegexp = true,
                Tolerant = true
            };
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using Path = Fluent.IO.Path;

namespace Jint.Tests.Scaffolding
{
    class Program
    {
        private static string[] suites = new string[]
            {
                "http://test262.ecmascript.org/json/ch06.json",
                "http://test262.ecmascript.org/json/ch07.json",
                "http://test262.ecmascript.org/json/ch08.json",
                "http://test262.ecmascript.org/json/ch09.json",
                "http://test262.ecmascript.org/json/ch10.json",
                "http://test262.ecmascript.org/json/ch11.json",
                "http://test262.ecmascript.org/json/ch12.json",
                "http://test262.ecmascript.org/json/ch13.json",
                "http://test262.ecmascript.org/json/ch14.json",
                "http://test262.ecmascript.org/json/ch15.json",

            };

        private const string SpecsUrl = "http://www.ecma-international.org/ecma-262/5.1/";
        private static string _specs;
        private static Path _testPath ;
        private static Path _suitePath;
        private static string _classTemplate;
        private static string _methodTemplate;
        private static Dictionary<string, List<TestEntry>> categorizedTests = new Dictionary<string, List<TestEntry>>(); 

        static void Main(string[] args)
        {

            var assemblyPath = typeof(Program).Assembly.Location;
            var assemblyDirectory = Path.Get(assemblyPath);
            _testPath = assemblyDirectory.Parent().Parent().Parent().Parent().Combine("Jint.Tests.Ecma");
            _suitePath = _testPath;
            if (!_suitePath.Exists)
            {
                _suitePath.CreateDirectory();
            }

            _classTemplate = File.ReadAllText("ClassTemplate.txt");
            _methodTemplate = File.ReadAllText("MethodTemplate.txt");

            foreach(var url in suites) {
                var content = new WebClient().DownloadString(url);
                dynamic suite = JsonConvert.DeserializeObject(content);
                var collection = suite.testsCollection;
                var tests = collection.tests;

                Console.Write(".");

                foreach (var test in tests)
                {
                    byte[] data = Convert.FromBase64String((string)test.code);
                    string decodedString = Encoding.UTF8.GetString(data);

                    RenderTest(
                        decodedString,
                        (string)test.commentary,
                        (string)test.description,
                        (string)test.path,
                        test.negative != null
                        );
                }
            }

            foreach (var category in categorizedTests.Keys)
            {
                var file = _testPath.Combine("Ecma").Combine(category + ".cs");

                var methods = new StringBuilder();
                foreach (var test in categorizedTests[category])
                {
                    methods.Append(test.Test);
                    methods.AppendLine();
                }

                var fileContent = _classTemplate;
                fileContent = fileContent.Replace("$$Methods$$", methods.ToString());
                fileContent = fileContent.Replace("$$ClassName$$", GetClassName(category));

                File.WriteAllText(file.FullPath, fileContent);
            }
        }

        private static void RenderTest(string code, string commentary, string description, string path, bool negative)
        {
            var file = _suitePath.Combine(path.Substring(0, path.Length - 3) + ".cs");
            if (!file.Parent().Exists)
            {
                file.Parent().CreateDirectory();
            }

            var className = GetClassName(file.FileNameWithoutExtension);
            var category = GetCategory(file.FileNameWithoutExtension);
            var content = _methodTemplate;
            var methodName = GetMethodName(String.IsNullOrEmpty(commentary) ? description : commentary);
            var uniqueMethodName = methodName;

            if (!categorizedTests.ContainsKey(category))
            {
                categorizedTests.Add(category, new List<TestEntry>());
            }

            if (categorizedTests[category].Any(x => x.MethodName == methodName))
            {
                uniqueMethodName += categorizedTests[category].Count(x => x.MethodName == methodName) + 1;
            }

            content = content.Replace("$$ClassName$$", className);
            content = content.Replace("$$Source$$", EncodeCSharpString(path));
            content = content.Replace("$$Description$$", EncodeCSharpString(description));
            content = content.Replace("$$Category$$", category);
            content = content.Replace("$$MethodName$$", uniqueMethodName);
            content = content.Replace("$$Negative$$", negative ? "true" : "false");
            content = NormalizeLineEndings(content);


            categorizedTests[category].Add(new TestEntry
                {
                    MethodName = methodName,
                    Test = content
                });

            File.WriteAllText(_suitePath.Combine(path).FullPath, code);
        }

        private static readonly Regex R = new Regex(@"(\r\n)|(\n)", RegexOptions.Multiline);
        private static string NormalizeLineEndings(string text)
        {
            return R.Replace(text, Environment.NewLine);
        }

        private static string EncodeCSharpString(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return String.Empty;
            }

            return value.Replace("\"", "\"\"");
        }

        private static string GetCategory(string filename)
        {
            string category = filename;
            category = filename.Split('-', '_').First();

            if (category.StartsWith("S"))
            {
                category = category.Substring(1);
            }

            return category;
        }

        public static string GetClassName(string filename)
        {
            return "Test_" + filename.Replace(".", "_").Replace("-", "__");
        }

        public static string GetMethodName(string description)
        {
            var simplified = Sanitize(description);
            var result = new StringBuilder(simplified.Length);
            var previousIsSpace = true;
            for (int i = 0; i < simplified.Length; i++)
            {
                if (simplified[i] == '-')
                {
                    previousIsSpace = true;
                    continue;
                }

                if (previousIsSpace)
                {
                    result.Append(simplified[i].ToString().ToUpper());
                }
                else
                {
                    result.Append(simplified[i].ToString().ToLower());
                }

                previousIsSpace = Char.IsDigit(simplified[i]);
            }

            return result.ToString();
        }

        public static string Sanitize(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return "";

            var result = new char[text.Length];

            var cursor = 0;
            var previousIsNotLetter = false;
            for (var i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (IsLetter(current) || (Char.IsDigit(current) && cursor > 0))
                {
                    if (previousIsNotLetter && i != 0 && cursor > 0)
                    {
                        result[cursor++] = '-';
                    }

                    result[cursor++] = Char.ToLowerInvariant(current);
                    previousIsNotLetter = false;
                }
                else
                {
                    previousIsNotLetter = true;
                }
            }

            return new string(result, 0, cursor);
        }
        
        public static bool IsLetter(char c)
        {
            return ('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z');
        }

        private class TestEntry
        {
            public string Test;
            public string MethodName;
        }
    }
}

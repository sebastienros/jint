using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Newtonsoft.Json;
using Undefined = Jint.Native.Undefined;

namespace Jint.Benchmark
{
    /// <summary>
    /// Test case for situation where object is projected via filter and map, Jint deems code as uncacheable.
    /// </summary>
    [MemoryDiagnoser]
    public class UncacheableExpressionsBenchmark
    {
        private Document doc;

        private string targetObject;
        private JsValue[] targetJsObject;

        private const string script = @"
function output(d) {
    var doc = d.SubDocuments.find(function(x){return x.Id==='testing';});
    return { Id : d.Id, Deleted : d.Deleted, SubTestId : (doc!==null&&doc!==undefined)?doc.Id:null, Values : d.SubDocuments.map(function(x){return {TargetId:x.TargetId,TargetValue:x.TargetValue,SubDocuments:x.SubDocuments.filter(function(s){return (s!==null&&s!==undefined);}).map(function(s){return {TargetId:s.TargetId,TargetValue:s.TargetValue};})};}) };
}
";

        private Engine engine;

        public class Document
        {
            public string Id { get; set; }
            public string TargetId { get; set; }
            public decimal TargetValue { get; set; }
            public bool Deleted { get; set; }
            public IEnumerable<Document> SubDocuments { get; set; }
        }

        [GlobalSetup]
        public void Setup()
        {
            doc = new Document
            {
                Deleted = false,
                SubDocuments = new List<Document>
                {
                    new Document
                    {
                        TargetId = "id1",
                        SubDocuments = Enumerable.Range(1, 200).Select(x => new Document()).ToList()
                    },
                    new Document
                    {
                        TargetId = "id2",
                        SubDocuments = Enumerable.Range(1, 200).Select(x => new Document()).ToList()
                    }
                }
            };

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    JsonSerializer.CreateDefault().Serialize(writer, doc);
                    writer.Flush();

                    var targetObjectJson = Encoding.UTF8.GetString(stream.ToArray());
                    targetObject = $"var d = {targetObjectJson};";
                }
            }

            CreateEngine();
        }

        private static void InitializeEngine(Options options)
        {
            options
                .LimitRecursion(64)
                .MaxStatements(int.MaxValue)
                .Strict()
                .LocalTimeZone(TimeZoneInfo.Utc);
        }

        [Params(500)]
        public int N { get; set; }

        [Benchmark]
        public void Benchmark()
        {
            var call = engine.GetValue("output").TryCast<ICallable>();
            for (int i = 0; i < N; ++i)
            {
                call.Call(Undefined.Instance, targetJsObject);
            }
        }

        private void CreateEngine()
        {
            engine = new Engine(InitializeEngine);
            engine.Execute(Polyfills);
            engine.Execute(script);
            engine.Execute(targetObject);
            targetJsObject = new[] {engine.GetValue("d")};
        }

        private const string Polyfills = @"
//https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/String/endsWith
if (!String.prototype.endsWith) {
    String.prototype.endsWith = function (searchStr, position) {
        if (!(position < this.length))
            position = this.length;
        else
            position |= 0; // round position
        return this.substr(position - searchStr.length,
            searchStr.length) === searchStr;
    };
}
//https://github.com/jsPolyfill/Array.prototype.find/blob/master/find.js
if (!Array.prototype.find) {
    Array.prototype.find = Array.prototype.find || function(callback) {
        if (this === null) {
            throw new TypeError('Array.prototype.find called on null or undefined');
        } else if (typeof callback !== 'function') {
            throw new TypeError('callback must be a function');
        }
        var list = Object(this);
        // Makes sures is always has an positive integer as length.
        var length = list.length >>> 0;
        var thisArg = arguments[1];
        for (var i = 0; i < length; i++) {
            var element = list[i];
            if ( callback.call(thisArg, element, i, list) ) {
                return element;
            }
        }
    };
}

if (!Array.prototype.fastFilter) {
    Array.prototype.fastFilter = function(callback) {
        var results = [];
        var item;
        var len = this.length;
        for (var i = 0, len = len; i < len; i++) {
            item = this[i];
            if (callback(item)) results.push(item);
        }
        return results;
    }
}

if (!Array.prototype.fastMap) {
    Array.prototype.fastMap = function(callback) {
        var h = [];
        var len = this.length;
        for (var i = 0, len = len; i < len; i++) {
            h.push(callback(this[i]));
        }
        return h;
    }
}


if (!Array.prototype.fastFind) {
    Array.prototype.fastFind = function(callback) {
        var item;
        var len = this.length;
        for (var i = 0, len = len; i < len; i++) {
            item = this[i];
            if (callback(item)) return item;
        }
    }
}

";
    }
}
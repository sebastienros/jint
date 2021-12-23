using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Test262.Language
{
    // Hacky way to get objects from assert.js and sta.js into the module context
    internal sealed class ModuleTestHost : Host
    {
        private readonly static Dictionary<string, JsValue> _staticValues = new();

        static ModuleTestHost()
        {
            var assemblyPath = new Uri(typeof(ModuleTestHost).GetTypeInfo().Assembly.Location).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var basePath = assemblyDirectory.Parent.Parent.Parent.FullName;

            var engine = new Engine();
            var assertSource = File.ReadAllText(Path.Combine(basePath, "harness", "assert.js"));
            var staSource = File.ReadAllText(Path.Combine(basePath, "harness", "sta.js"));

            engine.Execute(assertSource);
            engine.Execute(staSource);

            _staticValues["assert"] = engine.GetValue("assert");
            _staticValues["Test262Error"] = engine.GetValue("Test262Error");
            _staticValues["$ERROR"] = engine.GetValue("$ERROR");
            _staticValues["$DONOTEVALUATE"] = engine.GetValue("$DONOTEVALUATE");

            _staticValues["print"] = new ClrFunctionInstance(engine, "print", (thisObj, args) => TypeConverter.ToString(args.At(0)));
        }

        protected override ObjectInstance CreateGlobalObject(Realm realm)
        {
            var globalObj = base.CreateGlobalObject(realm);

            foreach (var key in _staticValues.Keys)
            {
                globalObj.FastAddProperty(key, _staticValues[key], true, true, true);
            }

            return globalObj;
        }
    }
}

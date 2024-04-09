#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint.Tests.Runtime
{
    public class ParserOptionsPropagationTests
    {
        public enum SourceKind
        {
            Script,
            ModuleViaBuilder,
            ModuleViaFactory,
        }

        private sealed class ModuleScript : IModuleLoader
        {
            public const string MainSpecifier = "main";
            private readonly bool _prepare;
            private readonly string _code;
            private readonly ModuleParseOptions _parseOptions;

            public ModuleScript(bool prepare, string code, ModuleParseOptions parseOptions)
            {
                _prepare = prepare;
                _code = code;
                _parseOptions = parseOptions;
            }

            ResolvedSpecifier IModuleLoader.Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            {
                if (moduleRequest.Specifier == MainSpecifier)
                    return new ResolvedSpecifier(moduleRequest, MainSpecifier, Uri: null, SpecifierType.Bare);

                throw new ArgumentException(null, nameof(moduleRequest));
            }

            Jint.Runtime.Modules.Module IModuleLoader.LoadModule(Engine engine, ResolvedSpecifier resolved)
            {
                if (resolved.ModuleRequest.Specifier == MainSpecifier)
                {
                    return _prepare
                        ? ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule(_code, MainSpecifier, options: new ModulePrepareOptions { ParseOptions = _parseOptions }))
                        : ModuleFactory.BuildSourceTextModule(engine, resolved, _code, _parseOptions);
                }

                throw new ArgumentException(null, nameof(resolved));
            }
        }

        [Theory]
        // NOTE: Can't test eval as Esprima can't parse eval code in non-tolerant mode.
        //[InlineData(SourceKind.Script, @"'' + eval('!!({g\\u0065t y() {} })')", false, false, null)]
        //[InlineData(SourceKind.Script, @"'' + eval('!!({g\\u0065t y() {} })')", true, false, null)]
        //[InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        //[InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]
        //[InlineData(SourceKind.ModuleViaBuilder, @"export default '' + eval('!!({g\\u0065t y() {} })')", false, false, null)]
        //[InlineData(SourceKind.ModuleViaBuilder, @"export default '' + eval('!!({g\\u0065t y() {} })')", true, false, null)]
        //[InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        //[InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]
        //[InlineData(SourceKind.ModuleViaFactory, @"export default '' + eval('!!({g\\u0065t y() {} })')", false, false, null)]
        //[InlineData(SourceKind.ModuleViaFactory, @"export default '' + eval('!!({g\\u0065t y() {} })')", true, false, null)]
        //[InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        //[InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + eval('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]

        [InlineData(SourceKind.Script, @"'' + new Function('return !!({g\\u0065t y() {} })')()", false, false, null)]
        [InlineData(SourceKind.Script, @"'' + new Function('return !!({g\\u0065t y() {} })')()", true, false, null)]
        [InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", false, true, "[object Object]true")]
        [InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", true, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default '' + new Function('return !!({g\\u0065t y() {} })')()", false, false, null)]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default '' + new Function('return !!({g\\u0065t y() {} })')()", true, false, null)]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", false, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", true, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaFactory, @"export default '' + new Function('return !!({g\\u0065t y() {} })')()", false, false, null)]
        [InlineData(SourceKind.ModuleViaFactory, @"export default '' + new Function('return !!({g\\u0065t y() {} })')()", true, false, null)]
        [InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", false, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + new Function('return !!({g\\u0065t y() {} })')()", true, true, "[object Object]true")]

        [InlineData(SourceKind.Script, @"'' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, false, null)]
        [InlineData(SourceKind.Script, @"'' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, false, null)]
        [InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        [InlineData(SourceKind.Script, @"({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default '' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, false, null)]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default '' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, false, null)]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaBuilder, @"export default ({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaFactory, @"export default '' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, false, null)]
        [InlineData(SourceKind.ModuleViaFactory, @"export default '' + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, false, null)]
        [InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", false, true, "[object Object]true")]
        [InlineData(SourceKind.ModuleViaFactory, @"export default ({g\u0065t x() {} }) + new ShadowRealm().evaluate('!!({g\\u0065t y() {} })')", true, true, "[object Object]true")]
        public void DynamicCodeShouldBeParsedWithCallerParserOptions(SourceKind sourceKind, string code, bool prepare, bool tolerant, string? expectedReturnValue)
        {
            Engine engine;
            Func<JsValue> evalAction;

            if (sourceKind == SourceKind.Script)
            {
                var parseOptions = ScriptParseOptions.Default with { Tolerant = tolerant };
                engine = new Engine();

                if (prepare)
                {
                    var preparedScript = Engine.PrepareScript(code, options: new ScriptPrepareOptions { ParseOptions = parseOptions });
                    evalAction = () => engine.Evaluate(preparedScript);
                }
                else
                {
                    evalAction = () => engine.Evaluate(code, parseOptions);
                }
            }
            else
            {
                var parseOptions = ModuleParseOptions.Default with { Tolerant = tolerant };
                if (sourceKind == SourceKind.ModuleViaBuilder)
                {
                    engine = new Engine();

                    if (prepare)
                    {
                        engine.Modules.Add("main", o =>
                        {
                            var preparedModule = Engine.PrepareModule(code, options: new ModulePrepareOptions { ParseOptions = parseOptions });
                            o.AddModule(preparedModule);
                        });
                    }
                    else
                    {
                        engine.Modules.Add("main", o => o.AddSource(code).WithOptions(_ => parseOptions));
                    }
                }
                else if (sourceKind == SourceKind.ModuleViaFactory)
                {
                    var moduleScript = new ModuleScript(prepare, code, parseOptions);
                    engine = new Engine(o => o.EnableModules(moduleScript));
                }
                else
                {
                    throw new InvalidOperationException();
                }

                evalAction = () =>
                {
                    var ns = engine.Modules.Import("main");
                    return ns.Get("default");
                };
            }

            if (expectedReturnValue is not null)
            {
                Assert.Equal(expectedReturnValue, evalAction());
            }
            else
            {
                var ex = Assert.ThrowsAny<Exception>(evalAction);
                Assert.True(ex is JavaScriptException or ParseErrorException);
            }
        }

        [Theory]
        [InlineData(false, false, null)]
        [InlineData(false, true, "true")]
        [InlineData(true, false, null)]
        [InlineData(true, true, "true")]
        public void TransitivelyImportedModuleShouldBeParsedWithOwnParserOptions(bool mainTolerant, bool otherTolerant, string? expectedReturnValue)
        {
            var engine = new Engine();
            engine.Modules.Add("main", o => o.AddSource("import { x } from 'other'; export default x").WithOptions(_ => ModuleParseOptions.Default with { Tolerant = mainTolerant }));
            engine.Modules.Add("other", o => o.AddSource("export const x = '' + new Function('return !!({g\\\\u0065t y() {} })')()").WithOptions(_ => ModuleParseOptions.Default with { Tolerant = otherTolerant }));

            var evalAction = () =>
            {
                var ns = engine.Modules.Import("main");
                return ns.Get("default");
            };

            if (expectedReturnValue is not null)
            {
                Assert.Equal(expectedReturnValue, evalAction());
            }
            else
            {
                var ex = Assert.Throws<JavaScriptException>(evalAction);
                Assert.True(ex.Error.InstanceofOperator(engine.Realm.Intrinsics.SyntaxError));
            }
        }

        [Fact]
        public void RealmsShouldBeIsolatedWithRegardToParserOptions()
        {
            var engine = new Engine();

            var parseOptions = ScriptParseOptions.Default with { AllowReturnOutsideFunction = true, Tolerant = true };
            Assert.Equal("true", engine.Evaluate("return '' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions));

            var shadowRealm1 = engine.Intrinsics.ShadowRealm.Construct();
            var shadowRealm2 = engine.Intrinsics.ShadowRealm.Construct();

            var ex = Assert.Throws<JavaScriptException>(() => shadowRealm1.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions with { Tolerant = false }));
            Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.TypeError));

            Assert.Equal("true", engine.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions));

            ex = Assert.Throws<JavaScriptException>(() => shadowRealm2.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions with { Tolerant = false }));
            Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.TypeError));

            Assert.Equal("true", shadowRealm1.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions));

            ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions with { Tolerant = false }));
            Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.SyntaxError));

            Assert.Equal("true", shadowRealm2.Evaluate("'' + new Function('return !!({g\\\\u0065t y() {} })')()", parseOptions));
        }
    }
}

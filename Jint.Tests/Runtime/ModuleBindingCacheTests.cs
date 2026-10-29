namespace Jint.Tests.Runtime;

/// <summary>
/// Reads of module-scope bindings from functions nested in the module remember where the value is stored.
/// Modules here declare at least nine top-level bindings so their environment keeps a hash table, which is
/// the storage the cache applies to; each test reads repeatedly so the cached location is exercised.
/// </summary>
public class ModuleBindingCacheTests
{
    private const string Padding = "const p1 = 1, p2 = 2, p3 = 3, p4 = 4, p5 = 5, p6 = 6, p7 = 7, p8 = 8, p9 = 9;\n";

    private static string Run(string main, params (string Name, string Code)[] others)
    {
        var engine = new Engine();
        foreach (var (name, code) in others)
        {
            engine.Modules.Add(name, Padding + code);
        }

        engine.Modules.Add("main", Padding + main);
        return engine.Modules.Import("main").Get("result").ToString();
    }

    [Test]
    public void ReassignedModuleBindingIsReadLive()
    {
        Run("""
            let counter = 0;
            const read = () => counter;
            let seen = '';
            for (let i = 0; i < 4; i++) { seen += read(); counter++; }
            export const result = seen;
            """).Should().Be("0123");
    }

    [Test]
    public void ImportSeesTheExportersReassignments()
    {
        Run("""
            import { value, bump } from 'lib';
            const read = () => value;
            let seen = '';
            for (let i = 0; i < 3; i++) { seen += read(); bump(); }
            export const result = seen + read();
            """, ("lib", "export let value = 10; export function bump() { value++; }")).Should().Be("10111213");
    }

    [Test]
    public void RenamedAndReExportedImportsResolveToTheOwner()
    {
        Run("""
            import { renamed, again } from 'middle';
            function read() { return renamed + ':' + again; }
            export const result = read() + ',' + read();
            """,
            ("lib", "export let value = 'v';"),
            ("middle", "export { value as renamed } from 'lib'; export { value as again } from 'lib';")).Should().Be("v:v,v:v");
    }

    [Test]
    public void ShadowingBindingInAFreshActivationWins()
    {
        Run("""
            const name = 'module';
            function outer(shadow) {
              if (shadow) { const name = 'block'; return (() => name)(); }
              return (() => name)();
            }
            export const result = [outer(false), outer(true), outer(false)].join();
            """).Should().Be("module,block,module");
    }

    [Test]
    public void TemporalDeadZoneStillThrows()
    {
        Run("""
            function read() { try { return later; } catch (e) { return e.constructor.name; } }
            const before = read();
            let later = 'set';
            export const result = before + ',' + read();
            """).Should().Be("ReferenceError,set");
    }
}

using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Workers;

/// <summary>https://html.spec.whatwg.org/multipage/workers.html#the-workerlocation-interface</summary>
internal static class WorkerLocationInstaller
{
    internal static void Install(Engine engine, Uri finalUrl)
    {
        var url = UrlParser.Parse(finalUrl.AbsoluteUri)
            ?? throw new InvalidOperationException("The worker's final URL is not a valid URL.");
        var prototype = new ObjectInstance(engine);
        var location = new WorkerLocationObject(engine, prototype, url);
        var constructor = new ClrFunction(engine, "WorkerLocation",
            (_, _) =>
            {
                Throw.TypeError(engine.Realm, "Illegal constructor");
                return JsValue.Undefined;
            });
        constructor.DefineOwnProperty("prototype", new PropertyDescriptor(prototype, PropertyFlag.None));
        prototype.DefineOwnProperty("constructor", new PropertyDescriptor(constructor, PropertyFlag.NonEnumerable));
        prototype.DefineOwnProperty(GlobalSymbolRegistry.ToStringTag,
            new PropertyDescriptor("WorkerLocation", PropertyFlag.Configurable));

        foreach (var name in new[] { "href", "origin", "protocol", "host", "hostname", "port", "pathname", "search", "hash" })
        {
            prototype.DefineOwnProperty(name, new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get " + name, (receiver, _) => Read(receiver, name)), null,
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
        }
        prototype.DefineOwnProperty("toString", new PropertyDescriptor(
            new ClrFunction(engine, "toString", (receiver, _) => Read(receiver, "href")),
            PropertyFlag.ConfigurableEnumerableWritable));
        var global = engine._mainRealm.GlobalObject;
        global.DefineOwnProperty("WorkerLocation", new PropertyDescriptor(constructor, PropertyFlag.NonEnumerable));
        global.DefineOwnProperty("location", new GetSetPropertyDescriptor(
            new ClrFunction(engine, "get location", (receiver, _) =>
            {
                if (!ReferenceEquals(receiver, global)) Throw.TypeError(engine.Realm, "Illegal invocation");
                return location;
            }),
            null, PropertyFlag.Configurable | PropertyFlag.Enumerable));

        JsValue Read(JsValue receiver, string name)
        {
            if (receiver is not WorkerLocationObject workerLocation)
            {
                Throw.TypeError(engine.Realm, "Illegal invocation");
                return JsValue.Undefined;
            }
            return JsString.Create(name switch
            {
                "href" => workerLocation.Url.Serialize(),
                "origin" => workerLocation.Url.SerializeOrigin(),
                "protocol" => workerLocation.Url.SerializeProtocol(),
                "host" => workerLocation.Url.SerializeHostAndPort(),
                "hostname" => workerLocation.Url.SerializeHost(),
                "port" => workerLocation.Url.SerializePort(),
                "pathname" => workerLocation.Url.SerializePath(),
                "search" => workerLocation.Url.SerializeSearch(),
                "hash" => workerLocation.Url.SerializeHash(),
                _ => throw new InvalidOperationException("Unknown WorkerLocation component: " + name),
            });
        }
    }

    private sealed class WorkerLocationObject : ObjectInstance
    {
        internal WorkerLocationObject(Engine engine, ObjectInstance prototype, UrlRecord url) : base(engine)
        {
            _prototype = prototype;
            Url = url;
        }

        internal UrlRecord Url { get; }
    }
}

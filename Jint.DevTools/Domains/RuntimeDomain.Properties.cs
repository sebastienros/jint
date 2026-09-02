using System.Buffers;
using System.Text.Json;
using Acornima.Ast;
using Jint.Diagnostics;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Runtime;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using EngineDescriptor = Jint.Runtime.Descriptors.PropertyDescriptor;

namespace Jint.DevTools.Domains;

/// <summary>
/// <c>Runtime.getProperties</c>: what one value is made of, read without running any of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No getter is ever invoked and no proxy trap ever fires.</b> Every property is read as a descriptor —
/// an accessor is reported as its two functions rather than called — and a proxy is answered as having
/// nothing, because <c>ownKeys</c> and <c>getOwnPropertyDescriptor</c> are script and this is a command a
/// client sends while looking rather than while running. That is the same promise
/// <see cref="ValueInspector"/> makes, and it is what lets a client expand an object in a paused engine
/// without changing what the engine will do next.
/// </para>
/// <para>
/// A CLR value projected into script is worth naming because it looks like an exception and is not: the
/// engine projects a CLR property as an <i>accessor</i> descriptor, so it is listed with its functions and
/// never read, exactly as a script-defined accessor is. A host inspecting its own objects therefore sees the
/// members and not their values, which is a real limitation and the same promise as everywhere else rather
/// than a carve-out for interop. What the guards below catch is the rarer case: a host object whose key
/// enumeration or whose descriptor read throws, which is reported as one bad property rather than as a
/// failed listing.
/// </para>
/// </remarks>
internal sealed partial class RuntimeDomain
{
    /// <summary>
    /// How far up a prototype chain the walk goes. A cycle is impossible — <c>[[SetPrototypeOf]]</c> refuses
    /// one — so this bounds only a chain nobody would want listed anyway.
    /// </summary>
    private const int MaxPrototypeHops = 32;

    private static readonly PropertyDescriptor[] NoProperties = [];

#pragma warning disable JINT0002 // ValueInspector is the engine's getter-free describer; this is what it is for
    private static readonly ValueInspectorOptions HeaderOnly = new() { MaxDepth = 0, MaxEntries = 0, MaxStringLength = 256 };
#pragma warning restore JINT0002

    /// <inheritdoc/>
    protected override ValueTask<GetPropertiesResponse> GetPropertiesAsync(GetPropertiesRequest parameters, CommandContext context)
    {
        var value = _target.Runtime.RemoteObjects.Resolve(parameters.ObjectId, out var group);

        var request = new RemoteObjectRequest
        {
            Addressable = true,
            GeneratePreview = parameters.GeneratePreview == true,

            // Billed to the group the object itself belongs to, so that releasing that group frees the tree
            // a client expanded rather than only the root it started from.
            ObjectGroup = group,
        };

        if (value is not ObjectInstance instance)
        {
            // A handle to a symbol or a BigInt: addressable, and with nothing inside it a client may ask for.
            return new ValueTask<GetPropertiesResponse>(new GetPropertiesResponse { Result = NoProperties });
        }

#pragma warning disable JINT0002
        var header = ValueInspector.Describe(value, HeaderOnly);
#pragma warning restore JINT0002
        if (IsProxy(header))
        {
            // Every way in is a trap, and a trap is script. A client sees an object with nothing in it
            // rather than an engine that ran the page's code because somebody clicked an expander.
            return new ValueTask<GetPropertiesResponse>(new GetPropertiesResponse { Result = NoProperties });
        }

        var accessorsOnly = parameters.AccessorPropertiesOnly == true;
        var properties = Collect(instance, parameters, request, accessorsOnly);

        return new ValueTask<GetPropertiesResponse>(new GetPropertiesResponse
        {
            Result = properties,

            // The protocol says an accessor-only listing carries no internal properties either, and the
            // front end relies on that to build its "show accessors" view without a second shape.
            InternalProperties = accessorsOnly ? null : InternalProperties(instance, header, request),
        });
    }

    private PropertyDescriptor[] Collect(
        ObjectInstance instance,
        GetPropertiesRequest parameters,
        in RemoteObjectRequest request,
        bool accessorsOnly)
    {
        var ownOnly = parameters.OwnProperties == true;
        var nonIndexedOnly = parameters.NonIndexedPropertiesOnly == true;

        var results = new List<PropertyDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var current = instance;
        for (var hops = 0; current is not null && hops < MaxPrototypeHops; hops++)
        {
            // A proxy anywhere in the chain ends the walk, not only at its start: `ownKeys` is a trap
            // wherever the object holding it sits, and a client expanding an object never runs a page's code.
            if (hops > 0 && IsProxy(current))
            {
                break;
            }

            AppendOwn(current, isOwn: hops == 0, nonIndexedOnly, accessorsOnly, in request, results, seen);

            if (ownOnly)
            {
                break;
            }

            // The slot rather than `[[GetPrototypeOf]]`, so reading it calls nothing either.
            current = current.Prototype;
        }

        return results.ToArray();
    }

    private void AppendOwn(
        ObjectInstance instance,
        bool isOwn,
        bool nonIndexedOnly,
        bool accessorsOnly,
        in RemoteObjectRequest request,
        List<PropertyDescriptor> results,
        HashSet<string> seen)
    {
        List<JsValue> keys;
        try
        {
            keys = instance.GetOwnPropertyKeys();
        }
        catch (Exception exception) when (exception is not ProtocolException)
        {
            // A CLR value whose member enumeration threw. The command still answers; a client asking what is
            // inside an object is not asking to be told the whole listing failed.
            return;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var name = key.ToString();

            if (nonIndexedOnly && IsArrayIndex(name))
            {
                continue;
            }

            if (!seen.Add(name))
            {
                // A property the prototype chain shadows is reported once, by the object nearest the
                // receiver, which is the one a script would actually read.
                continue;
            }

            var described = Describe(instance, key, name, isOwn, accessorsOnly, in request);
            if (described is not null)
            {
                results.Add(described);
            }
        }
    }

    private PropertyDescriptor? Describe(
        ObjectInstance instance,
        JsValue key,
        string name,
        bool isOwn,
        bool accessorsOnly,
        in RemoteObjectRequest request)
    {
        EngineDescriptor descriptor;
        try
        {
            descriptor = instance.GetOwnProperty(key);
        }
        catch (Exception exception) when (exception is not ProtocolException)
        {
            return Failed(name, isOwn, exception);
        }

        if (ReferenceEquals(descriptor, EngineDescriptor.Undefined))
        {
            return null;
        }

        var isAccessor = descriptor.IsAccessorDescriptor();
        if (accessorsOnly && !isAccessor)
        {
            return null;
        }

        var symbol = key.IsSymbol() ? _objects.Describe(key, request) : null;

        if (isAccessor)
        {
            var get = descriptor.Get;
            var set = descriptor.Set;

            return new PropertyDescriptor
            {
                Name = name,

                // Described, never invoked. A client that wants what the getter returns asks for it with a
                // callFunctionOn of its own, which is a thing it did on purpose.
                Get = get is not null && !get.IsUndefined() ? _objects.Describe(get, request) : null,
                Set = set is not null && !set.IsUndefined() ? _objects.Describe(set, request) : null,
                Configurable = descriptor.Configurable,
                Enumerable = descriptor.Enumerable,
                IsOwn = isOwn,
                Symbol = symbol,
            };
        }

        JsValue value;
        try
        {
            value = descriptor.Value;
        }
        catch (Exception exception) when (exception is not ProtocolException)
        {
            return Failed(name, isOwn, exception);
        }

        return new PropertyDescriptor
        {
            Name = name,
            Value = _objects.Describe(value, request),
            Writable = descriptor.Writable,
            Configurable = descriptor.Configurable,
            Enumerable = descriptor.Enumerable,
            IsOwn = isOwn,
            Symbol = symbol,
        };
    }

    /// <summary>
    /// One property whose read threw, which on this path means host code rather than script: a CLR member
    /// projected into the object. Reported as the protocol's <c>wasThrown</c> rather than failing the whole
    /// listing, so one bad member does not hide the other forty.
    /// </summary>
    private static PropertyDescriptor Failed(string name, bool isOwn, Exception exception)
    {
        return new PropertyDescriptor
        {
            Name = name,
            Value = new RemoteObject
            {
                Type = RemoteObjectTypeValues.Object,
                Subtype = RemoteObjectSubtypeValues.Error,
                ClassName = exception.GetType().Name,
                Description = exception.Message,
            },
            WasThrown = true,
            Configurable = false,
            Enumerable = false,
            IsOwn = isOwn,
        };
    }

#pragma warning disable JINT0002 // ValueInspector is the engine's getter-free describer; this is what it is for
    /// <summary>
    /// The slots a client is shown in double brackets: what the value inherits from, what a promise has
    /// settled to, where a function was declared, and what a bound function is bound to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[[Handler]]</c> and <c>[[Target]]</c> of a proxy are deliberately absent rather than forgotten: a
    /// proxy answers no properties at all here, and the engine publishes neither slot outside its own
    /// assembly. <c>[[Entries]]</c> of a map or a set is absent for the same reason it is optional in the
    /// protocol — the preview already carries them, bounded. <c>[[Scopes]]</c> is absent because the
    /// environment a closure captured is not published either; the scope chain of a <i>paused</i> frame is,
    /// through <c>Debugger.paused</c>.
    /// </para>
    /// <para>
    /// Reading any of them runs nothing: a function's declaration is an abstract syntax tree node it already
    /// carries, and a bound function's three slots are fields.
    /// </para>
    /// </remarks>
    private InternalPropertyDescriptor[]? InternalProperties(ObjectInstance instance, ValueDescription header, in RemoteObjectRequest request)
    {
        var properties = new List<InternalPropertyDescriptor>(3)
        {
            new()
            {
                Name = "[[Prototype]]",
                Value = _objects.Describe(instance.Prototype ?? JsValue.Null, request),
            },
        };

        if (header.PromiseState is { } state)
        {
            properties.Add(new InternalPropertyDescriptor
            {
                Name = "[[PromiseState]]",
                Value = _objects.Describe(state switch
                {
                    ValuePromiseState.Fulfilled => "fulfilled",
                    ValuePromiseState.Rejected => "rejected",
                    _ => "pending",
                }, request),
            });

            if (header.PromiseResult is { } result)
            {
                properties.Add(new InternalPropertyDescriptor
                {
                    Name = "[[PromiseResult]]",
                    Value = RemoteObjectMapper.FromDescription(result),
                });
            }
        }

        AppendFunctionSlots(instance, request, properties);

        return properties.ToArray();
    }
#pragma warning restore JINT0002

    /// <summary>
    /// What a function carries beyond its properties: where it was declared, and — for a bound one — what it
    /// was bound to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[[FunctionLocation]]</c> is what makes a function clickable: a front end opens the script at the
    /// declaration rather than only naming it. A function the engine has no declaration for — every built-in,
    /// and anything a host installed — carries none, rather than a location against the sentinel script
    /// identifier <c>0</c> that a front end cannot open.
    /// </para>
    /// <para>
    /// <b>This is the one caller of <c>ScriptRegistry.At</c> that is not a rendered stack frame</b>, and the
    /// reason is that there is no identity to look up: a function value publishes its declaration node
    /// through <c>Function.FunctionDeclaration</c> and does not publish the <c>Program</c> that node was
    /// parsed in, the way <c>CallFrame.Program</c> does for a frame. So the script is matched by the source
    /// name the declaration's own location carries, with that lookup's caveat — a name is shared by every
    /// program a host parsed under it, and every sourceless <c>Execute</c> is <c>&lt;anonymous&gt;</c>.
    /// Closing it is the seam <see href="https://github.com/sebastienros/jint/issues/3632">#3632</see>
    /// opened for a frame, extended to a function value; nothing here depends on that landing.
    /// </para>
    /// <para>
    /// A bound function's three slots are Chrome's names in Chrome's order. <c>[[BoundArgs]]</c> is a
    /// <i>copy</i>: the engine's own array is what every call through the bound function reads its leading
    /// arguments from, and a client holding a handle to it could otherwise write through it.
    /// </para>
    /// </remarks>
    private void AppendFunctionSlots(
        ObjectInstance instance,
        in RemoteObjectRequest request,
        List<InternalPropertyDescriptor> properties)
    {
        if (instance is BindFunction bound)
        {
            properties.Add(new InternalPropertyDescriptor
            {
                Name = "[[TargetFunction]]",
                Value = _objects.Describe(bound.BoundTargetFunction, request),
            });
            properties.Add(new InternalPropertyDescriptor
            {
                Name = "[[BoundThis]]",
                Value = _objects.Describe(bound.BoundThis, request),
            });

            var arguments = bound.BoundArguments;
            var copy = new JsValue[arguments.Length];
            Array.Copy(arguments, copy, arguments.Length);

            properties.Add(new InternalPropertyDescriptor
            {
                Name = "[[BoundArgs]]",
                Value = _objects.Describe(new JsArray(_target.Runtime.Engine, copy), request),
            });

            return;
        }

        if (instance is Function function && function.FunctionDeclaration is Node declaration)
        {
            properties.Add(new InternalPropertyDescriptor
            {
                Name = "[[FunctionLocation]]",
                Value = Location(declaration),
            });
        }
    }

    /// <summary>
    /// One declaration position in the shape V8 sends it: a remote object with no handle, whose
    /// <c>value</c> is the location itself.
    /// </summary>
    private RemoteObject Location(Node declaration)
    {
        var location = declaration.Location;
        var script = _target.Runtime.Scripts?.At(location.SourceFile, location.Start.Line, location.Start.Column);

        var buffer = new ArrayBufferWriter<byte>(96);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("scriptId", script?.ScriptId ?? "0");

            // The engine counts lines from one and the protocol counts them from zero; both count columns
            // from zero, which is why only one of the two is shifted.
            writer.WriteNumber("lineNumber", Math.Max(0, location.Start.Line - 1));
            writer.WriteNumber("columnNumber", location.Start.Column);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);

        return new RemoteObject
        {
            Type = RemoteObjectTypeValues.Object,

            // Not one of RemoteObjectSubtypeValues: V8 sends this subtype for a location and the pinned
            // protocol description does not declare it, so the string is written here and named nowhere.
            Subtype = "internal#location",
            Value = document.RootElement.Clone(),
        };
    }

#pragma warning disable JINT0002 // ValueInspector is the engine's getter-free describer; this is what it is for
    /// <summary>Whether a value is a proxy, asked without touching a single one of its traps.</summary>
    private static bool IsProxy(ObjectInstance value) => IsProxy(ValueInspector.Describe(value, HeaderOnly));

    /// <inheritdoc cref="IsProxy(ObjectInstance)"/>
    private static bool IsProxy(ValueDescription description) => description.Kind == ValueKind.Proxy;
#pragma warning restore JINT0002

    /// <summary>Whether a property name is a canonical array index, which is what <c>nonIndexedPropertiesOnly</c> filters.</summary>
    private static bool IsArrayIndex(string name)
    {
        if (name.Length == 0 || name.Length > 10)
        {
            return false;
        }

        if (name.Length > 1 && name[0] == '0')
        {
            // "01" is a property name and not an index, so the two are different properties.
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return uint.TryParse(name, out var index) && index != uint.MaxValue;
    }
}

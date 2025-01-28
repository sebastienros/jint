using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Modules;
using Jint.Runtime.CallStack;

namespace Jint;

public class Options
{
    private static readonly CultureInfo _defaultCulture = CultureInfo.CurrentCulture;
    private static readonly TimeZoneInfo _defaultTimeZone = TimeZoneInfo.Local;

    private ITimeSystem? _timeSystem;
    internal List<Action<Engine>> _configurations { get; } = new();

    public delegate JsValue? MemberAccessorDelegate(Engine engine, object target, string member);

    public delegate ObjectInstance? WrapObjectDelegate(Engine engine, object target, Type? type);

    public delegate bool ExceptionHandlerDelegate(Exception exception);

    public delegate string? BuildCallStackDelegate(string shortDescription, SourceLocation location, string[]? arguments);

    /// <summary>
    /// Execution constraints for the engine.
    /// </summary>
    public ConstraintOptions Constraints { get; } = new();

    /// <summary>
    /// CLR interop related options.
    /// </summary>
    public InteropOptions Interop { get; } = new();

    /// <summary>
    /// Debugger configuration.
    /// </summary>
    public DebuggerOptions Debugger { get; } = new();

    /// <summary>
    /// Host options.
    /// </summary>
    public HostOptions Host { get; } = new();

    /// <summary>
    /// Module options
    /// </summary>
    public ModuleOptions Modules { get; } = new();

    /// <summary>
    /// Whether the code should be always considered to be in strict mode. Can improve performance.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>
    /// The culture the engine runs on, defaults to current culture.
    /// </summary>
    public CultureInfo Culture { get; set; } = _defaultCulture;

    /// <summary>
    /// Configures a time system to use. Defaults to DefaultTimeSystem using local time.
    /// </summary>
    public ITimeSystem TimeSystem
    {
        get => _timeSystem ??= new DefaultTimeSystem(TimeZone, Culture);
        set => _timeSystem = value;
    }

    /// <summary>
    /// The time zone the engine runs on, defaults to local. Same as setting DefaultTimeSystem with the time zone.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = _defaultTimeZone;

    /// <summary>
    /// Reference resolver allows customizing behavior for reference resolving. This can be useful in cases where
    /// you want to ignore long chain of property accesses that might throw if anything is null or undefined.
    /// An example of such is <code>var a = obj.field.subField.value</code>. Custom resolver could accept chain to return
    /// null/undefined on first occurrence.
    /// </summary>
    public IReferenceResolver ReferenceResolver { get; set; } = DefaultReferenceResolver.Instance;

    /// <summary>
    /// Whether calling 'eval' with custom code and function constructors taking function code as string is allowed.
    /// Defaults to true.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
    /// </remarks>
    [Obsolete("Use Options.Host.StringCompilationAllowed")]
    public bool StringCompilationAllowed
    {
        get => Host.StringCompilationAllowed;
        set => Host.StringCompilationAllowed = value;
    }

    /// <summary>
    /// Options for the built-in JSON (de)serializer which
    /// gets used using <c>JSON.parse</c> or <c>JSON.stringify</c>
    /// </summary>
    public JsonOptions Json { get; set; } = new();

    /// <summary>
    /// What experimental features are allowed, functionality may lacking or even plain wrong. Defaults to having none.
    /// </summary>
    public ExperimentalFeature ExperimentalFeatures { get; set; }

    /// <summary>
    /// Called by the <see cref="Engine"/> instance that loads this <see cref="Options" />
    /// once it is loaded.
    /// </summary>
    internal void Apply(Engine engine)
    {
        foreach (var configuration in _configurations)
        {
            configuration(engine);
        }

        // add missing bits if needed
        if (Interop.Enabled)
        {
#pragma warning disable IL2026

            engine.Realm.GlobalObject.SetProperty("System", new PropertyDescriptor(new NamespaceReference(engine, "System"), PropertyFlag.AllForbidden));

            engine.Realm.GlobalObject.SetProperty("importNamespace", new PropertyDescriptor(new ClrFunction(
                    engine,
                    "importNamespace",
                    (_, arguments) => new NamespaceReference(engine, arguments.At(0).IsNullOrUndefined() ? null : TypeConverter.ToString(arguments.At(0)))),
                PropertyFlag.AllForbidden));

            engine.Realm.GlobalObject.SetProperty("clrHelper", new PropertyDescriptor(ObjectWrapper.Create(engine, new ClrHelper(Interop)), PropertyFlag.AllForbidden));

#pragma warning restore IL2026
        }

        if (Interop.ExtensionMethodTypes.Count > 0)
        {
            AttachExtensionMethodsToPrototypes(engine);
        }

        if (Modules.RegisterRequire)
        {
            // Node js like loading of modules
            engine.Realm.GlobalObject.SetProperty("require", new PropertyDescriptor(new ClrFunction(
                    engine,
                    "require",
                    (thisObj, arguments) =>
                    {
                        var specifier = TypeConverter.ToString(arguments.At(0));
                        return engine.Modules.Import(specifier);
                    }),
                PropertyFlag.AllForbidden));
        }

        engine.Modules = new Engine.ModuleOperations(engine, Modules.ModuleLoader);
    }

    private static void AttachExtensionMethodsToPrototypes(Engine engine)
    {
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Array.PrototypeObject, typeof(Array));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Boolean.PrototypeObject, typeof(bool));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Date.PrototypeObject, typeof(DateTime));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Number.PrototypeObject, typeof(double));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Object.PrototypeObject, typeof(ExpandoObject));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.RegExp.PrototypeObject, typeof(System.Text.RegularExpressions.Regex));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.String.PrototypeObject, typeof(string));
    }

    private static void AttachExtensionMethodsToPrototype(Engine engine, ObjectInstance prototype, Type objectType)
    {
        if (!engine._extensionMethods.TryGetExtensionMethods(objectType, out var methods))
        {
            return;
        }

        foreach (var overloads in methods.GroupBy(x => x.Name, StringComparer.Ordinal))
        {
            PropertyDescriptor CreateMethodInstancePropertyDescriptor(ClrFunction? function)
            {
                var instance = new MethodInfoFunction(
                    engine,
                    objectType,
                    target: null,
                    overloads.Key,
                    methods: MethodDescriptor.Build(overloads.ToList()),
                    function);

                return new PropertyDescriptor(instance, PropertyFlag.AllForbidden);
            }

            JsValue key = overloads.Key;
            PropertyDescriptor? descriptorWithFallback = null;
            PropertyDescriptor? descriptorWithoutFallback = null;

            if (prototype.HasOwnProperty(key) &&
                prototype.GetOwnProperty(key).Value is ClrFunction clrFunctionInstance)
            {
                descriptorWithFallback = CreateMethodInstancePropertyDescriptor(clrFunctionInstance);
                prototype.SetOwnProperty(key, descriptorWithFallback);
            }
            else
            {
                descriptorWithoutFallback = CreateMethodInstancePropertyDescriptor(null);
                prototype.SetOwnProperty(key, descriptorWithoutFallback);
            }

            // make sure we register both lower case and upper case
            if (char.IsUpper(overloads.Key[0]))
            {
                key = char.ToLower(overloads.Key[0], CultureInfo.InvariantCulture) + overloads.Key.Substring(1);

                if (prototype.HasOwnProperty(key) &&
                    prototype.GetOwnProperty(key).Value is ClrFunction lowerclrFunctionInstance)
                {
                    descriptorWithFallback ??= CreateMethodInstancePropertyDescriptor(lowerclrFunctionInstance);
                    prototype.SetOwnProperty(key, descriptorWithFallback);
                }
                else
                {
                    descriptorWithoutFallback ??= CreateMethodInstancePropertyDescriptor(null);
                    prototype.SetOwnProperty(key, descriptorWithoutFallback);
                }
            }
        }
    }


    public class DebuggerOptions
    {
        /// <summary>
        /// Whether debugger functionality is enabled, defaults to false.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Configures the statement handling strategy, defaults to Ignore.
        /// </summary>
        public DebuggerStatementHandling StatementHandling { get; set; } = DebuggerStatementHandling.Ignore;

        /// <summary>
        /// Configures the step mode used when entering the script.
        /// </summary>
        public StepMode InitialStepMode { get; set; } = StepMode.None;
    }

    public class InteropOptions
    {
        /// <summary>
        /// Whether accessing CLR and it's types and methods is allowed from JS code, defaults to false.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether to expose <see cref="object.GetType"></see> which can allow bypassing allow lists and open a way to reflection.
        /// Defaults to false.
        /// </summary>
        public bool AllowGetType { get; set; }

        /// <summary>
        /// Whether Jint should allow wrapping objects from System.Reflection namespace.
        /// Defaults to false.
        /// </summary>
        public bool AllowSystemReflection { get; set; }

        /// <summary>
        /// Whether writing to CLR objects is allowed (set properties), defaults to true.
        /// </summary>
        public bool AllowWrite { get; set; } = true;

        /// <summary>
        /// Whether operator overloading resolution is allowed, defaults to false.
        /// </summary>
        public bool AllowOperatorOverloading { get; set; }

        /// <summary>
        /// Types holding extension methods that should be considered when resolving methods.
        /// </summary>
        public List<Type> ExtensionMethodTypes { get; } = new();

        /// <summary>
        /// Object converters to try when build-in conversions.
        /// </summary>
        public List<IObjectConverter> ObjectConverters { get; } = new();

        /// <summary>
        /// Whether identity map is persisted for object wrappers in order to maintain object identity. This can cause
        /// memory usage to grow when targeting large set and freeing of memory can be delayed due to ConditionalWeakTable semantics.
        /// Defaults to false.
        /// </summary>
        public bool TrackObjectWrapperIdentity { get; set; }

        /// <summary>
        /// If no known type could be guessed, objects are by default wrapped as an
        /// ObjectInstance using class ObjectWrapper. This function can be used to
        /// change the behavior.
        /// </summary>
        public WrapObjectDelegate WrapObjectHandler { get; set; } = static (engine, target, type) => ObjectWrapper.Create(engine, target, type);

        /// <summary>
        /// The handler used to build stack traces. Changing this enables mapping
        /// stack traces to code different from the code being executed, eg. when
        /// executing code transpiled from TypeScript.
        /// </summary>
        public BuildCallStackDelegate? BuildCallStackHandler { get; set; }

        /// <summary>
        ///
        /// </summary>
        public MemberAccessorDelegate MemberAccessor { get; set; } = static (engine, target, member) => null;

        /// <summary>
        /// Exceptions that thrown from CLR code are converted to JavaScript errors and
        /// can be used in at try/catch statement. By default these exceptions are bubbled
        /// to the CLR host and interrupt the script execution. If handler returns true these exceptions are converted
        /// to JS errors that can be caught by the script.
        /// </summary>
        public ExceptionHandlerDelegate ExceptionHandler { get; set; } = _defaultExceptionHandler;

        /// <summary>
        /// Assemblies to allow scripts to call CLR types directly like <example>System.IO.File</example>.
        /// </summary>
        public List<Assembly> AllowedAssemblies { get; set; } = new();

        /// <summary>
        /// Type and member resolving strategy, which allows filtering allowed members and configuring member
        /// name matching comparison.
        /// </summary>
        /// <remarks>
        /// As this object holds caching state same instance should be shared between engines, if possible.
        /// </remarks>
        public TypeResolver TypeResolver { get; set; } = TypeResolver.Default;

        /// <summary>
        /// When writing values to CLR objects, how should JS values be coerced to CLR types.
        /// Defaults to only coercing to string values when writing to string targets.
        /// </summary>
        public ValueCoercionType ValueCoercion { get; set; } = ValueCoercionType.String;

        /// <summary>
        /// Strategy to create a CLR object to hold converted <see cref="ObjectInstance"/>.
        /// </summary>
        public Func<ObjectInstance, IDictionary<string, object?>>? CreateClrObject = _ => new ExpandoObject();

        /// <summary>
        /// Strategy to create a CLR object from TypeReference.
        /// Defaults to retuning null which makes TypeReference attempt to find suitable constructor.
        /// </summary>
        public Func<Engine, Type, JsValue[], object?> CreateTypeReferenceObject = (_, _, _) => null;

        internal static readonly ExceptionHandlerDelegate _defaultExceptionHandler = static exception => false;

        /// <summary>
        /// When not null, is used to serialize any CLR object in an
        /// <see cref="IObjectWrapper"/> passing through 'JSON.stringify'.
        /// </summary>
        public Func<object, string>? SerializeToJson { get; set; }

        /// <summary>
        /// What kind of date time should be produced when JavaScript date is converted to DateTime. If Local, uses <see cref="Options.TimeZone"/>.
        /// Defaults to <see cref="System.DateTimeKind.Utc"/>.
        /// </summary>
        public DateTimeKind DateTimeKind { get; set; } = DateTimeKind.Utc;

        /// <summary>
        /// Should the Array prototype be attached instead of Object prototype to the wrapped interop objects when type looks suitable. Defaults to true.
        /// </summary>
        public bool AttachArrayPrototype { get; set; } = true;

        /// <summary>
        /// Whether the engine should throw an error when a member is not found on a CLR object. Defaults to false.
        /// </summary>
        public bool ThrowOnUnresolvedMember { get; set; }

        /// <summary>
        /// Types of CLR members reported by <see cref="ObjectWrapper"/> when enumerating properties/serializing <see cref="ObjectWrapper.ToObject"/>.
        /// Supported values are: <see cref="MemberTypes.Field"/>, <see cref="MemberTypes.Property"/>, <see cref="MemberTypes.Method"/>.
        /// All other values are ignored.
        /// </summary>
        public MemberTypes ObjectWrapperReportedMemberTypes { get; set; } = MemberTypes.Field | MemberTypes.Property | MemberTypes.Method;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        public BindingFlags ObjectWrapperReportedFieldBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        public BindingFlags ObjectWrapperReportedPropertyBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" /> | <see cref="BindingFlags.Static" />.
        /// </summary>
        public BindingFlags ObjectWrapperReportedMethodBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;
    }

    public class ConstraintOptions
    {
        /// <summary>
        /// Registered constraints.
        /// </summary>
        public List<Constraint> Constraints { get; } = new();

        /// <summary>
        /// Maximum recursion depth allowed, defaults to -1 (no checks).
        /// </summary>
        public int MaxRecursionDepth { get; set; } = -1;

        /// <summary>
        /// Maximum recursion stack count, defaults to -1 (as-is dotnet stacktrace).
        /// </summary>
        /// <remarks>
        /// Chrome and V8 based engines (ClearScript) that can handle 13955.
        /// When set to a different value except -1, it can reduce slight performance/stack trace readability drawback. (after hitting the engine's own limit),
        /// When max stack size to be exceeded, Engine throws an exception <see cref="JavaScriptException" />.
        /// </remarks>
        public int MaxExecutionStackCount { get; set; } = StackGuard.Disabled;

        /// <summary>
        /// Maximum time a Regex is allowed to run, defaults to 10 seconds.
        /// </summary>
        public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum size for JavaScript array, defaults to <see cref="uint.MaxValue"/>.
        /// </summary>
        public uint MaxArraySize { get; set; } = uint.MaxValue;

        /// <summary>
        /// How many iterations is Atomics.pause allowed to instruct to wait using <see cref="System.Threading.Thread.SpinWait"/>, defaults to 10 000.
        /// </summary>
        public int MaxAtomicsPauseIterations { get; set; } = 10_000;
    }

    /// <summary>
    /// Host related customization, still work in progress.
    /// </summary>
    public class HostOptions
    {
        internal Func<Engine, Host> Factory { get; set; } = _ => new Host();

        /// <summary>
        /// Whether calling 'eval' with custom code and function constructors taking function code as string is allowed.
        /// Defaults to true.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
        /// </remarks>
        public bool StringCompilationAllowed { get; set; } = true;

        /// <summary>
        /// Possibility to override Jint's default function() { [native code] } format for functions using AST Node.
        /// If callback return null, Jint will use its own default logic.
        /// </summary>
        public Func<Function, Node, string?> FunctionToStringHandler { get; set; } = (_, _) => null;
    }

    /// <summary>
    /// Module related customization
    /// </summary>
    public class ModuleOptions
    {
        /// <summary>
        /// Whether to register require function to engine which will delegate to module loader, defaults to false.
        /// </summary>
        public bool RegisterRequire { get; set; }

        /// <summary>
        /// Module loader implementation, by default exception will be thrown if module loading is not enabled.
        /// </summary>
        public IModuleLoader ModuleLoader { get; set; } = FailFastModuleLoader.Instance;
    }

    /// <summary>
    /// JSON.parse / JSON.stringify related customization
    /// </summary>
    public class JsonOptions
    {
        /// <summary>
        /// The maximum depth allowed when parsing JSON files using "JSON.parse",
        /// defaults to 64.
        /// </summary>
        public int MaxParseDepth { get; set; } = 64;
    }
}

/// <summary>
/// Rules for writing values to CLR fields.
/// </summary>
[Flags]
public enum ValueCoercionType
{
    /// <summary>
    /// No coercion will be done. If there's no type converter, and error will be thrown.
    /// </summary>
    None = 0,

    /// <summary>
    /// JS coercion using boolean rules "dog" == true, "" == false, 1 == true, 3 == true, 0 == false, { "prop": 1 } == true etc.
    /// </summary>
    Boolean = 1,

    /// <summary>
    /// JS coercion to numbers, false == 0, true == 1. valueOf functions will be used when available for object instances.
    /// Valid against targets of type: Decimal, Double, Int32, Int64.
    /// </summary>
    Number = 2,

    /// <summary>
    /// JS coercion to strings, toString function will be used when available for objects.
    /// </summary>
    String = 4,

    /// <summary>
    /// All coercion rules enabled.
    /// </summary>
    All = Boolean | Number | String
}

/// <summary>
/// Features that only work partially, if all.
/// </summary>
[Flags]
public enum ExperimentalFeature
{
    /// <summary>
    /// No experimental features enabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Generator support
    /// </summary>
    Generators = 1,

    /// <summary>
    /// Wrapping tasks to promises
    /// </summary>
    TaskInterop = 2,

    /// <summary>
    /// All coercion rules enabled.
    /// </summary>
    All = Generators | TaskInterop
}

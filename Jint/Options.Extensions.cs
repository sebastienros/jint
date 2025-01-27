using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

/// <summary>
/// Compatibility layer to allow fluent syntax against options object.
/// </summary>
public static class OptionsExtensions
{
    /// <summary>
    /// Run the script in strict mode.
    /// </summary>
    public static Options Strict(this Options options, bool strict = true)
    {
        options.Strict = strict;
        return options;
    }

    /// <summary>
    /// Selects the handling for script <code>debugger</code> statements.
    /// </summary>
    /// <remarks>
    /// The <c>debugger</c> statement can either be ignored (default) trigger debugging at CLR level (e.g. Visual Studio),
    /// or trigger a break in Jint's DebugHandler.
    /// </remarks>
    public static Options DebuggerStatementHandling(this Options options,
        DebuggerStatementHandling debuggerStatementHandling)
    {
        options.Debugger.StatementHandling = debuggerStatementHandling;
        return options;
    }

    /// <summary>
    /// Allow to run the script in debug mode.
    /// </summary>
    public static Options DebugMode(this Options options, bool debugMode = true)
    {
        options.Debugger.Enabled = debugMode;
        return options;
    }

    /// <summary>
    /// Set initial step mode.
    /// </summary>
    public static Options InitialStepMode(this Options options, StepMode initialStepMode = StepMode.None)
    {
        options.Debugger.InitialStepMode = initialStepMode;
        return options;
    }

    /// <summary>
    /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
    /// </summary>
    public static Options AddObjectConverter<T>(this Options options) where T : IObjectConverter, new()
    {
        return AddObjectConverter(options, new T());
    }

    /// <summary>
    /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
    /// </summary>
    public static Options AddObjectConverter(this Options options, IObjectConverter objectConverter)
    {
        options.Interop.ObjectConverters.Add(objectConverter);
        return options;
    }

    /// <summary>
    /// Sets maximum allowed depth of recursion.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="maxRecursionDepth">
    /// The allowed depth.
    /// a) In case max depth is zero no recursion is allowed.
    /// b) In case max depth is equal to n it means that in one scope function can be called no more than n times.
    /// </param>
    /// <returns>Options instance for fluent syntax</returns>
    public static Options LimitRecursion(this Options options, int maxRecursionDepth = 0)
    {
        options.Constraints.MaxRecursionDepth = maxRecursionDepth;
        return options;
    }

    public static Options Culture(this Options options, CultureInfo cultureInfo)
    {
        options.Culture = cultureInfo;
        return options;
    }

    public static Options LocalTimeZone(this Options options, TimeZoneInfo timeZoneInfo)
    {
        options.TimeZone = timeZoneInfo;
        return options;
    }

    /// <summary>
    /// Disables calling 'eval' with custom code and function constructors taking function code as string.
    /// By default eval and function code parsing is allowed.
    /// </summary>
    public static Options DisableStringCompilation(this Options options, bool disable = true)
    {
        options.Host.StringCompilationAllowed = !disable;
        return options;
    }

    public static Options AddExtensionMethods(this Options options, params Type[] types)
    {
        options.Interop.ExtensionMethodTypes.AddRange(types);
        return options;
    }

    /// <summary>
    /// If no known type could be guessed, objects are normally wrapped as an
    /// ObjectInstance using class ObjectWrapper. This function can be used to
    /// register a handler for a customized handling.
    /// </summary>
    public static Options SetWrapObjectHandler(this Options options, Options.WrapObjectDelegate wrapObjectHandler)
    {
        options.Interop.WrapObjectHandler = wrapObjectHandler;
        return options;
    }

    /// <summary>
    /// Sets the handler used to build stack traces. This is useful if the code currently
    /// running was transpiled (eg. TypeScript) and the source map of original code is available.
    /// </summary>
    public static Options SetBuildCallStackHandler(this Options options, Options.BuildCallStackDelegate buildCallStackHandler)
    {
        options.Interop.BuildCallStackHandler = buildCallStackHandler;
        return options;
    }

    /// <summary>
    /// Sets the type converter to use.
    /// </summary>
    public static Options SetTypeConverter(this Options options, Func<Engine, ITypeConverter> typeConverterFactory)
    {
        options._configurations.Add(engine => engine.TypeConverter = typeConverterFactory(engine));
        return options;
    }

    /// <summary>
    /// Registers a delegate that is called when CLR members are invoked. This allows
    /// to change what values are returned for specific CLR objects, or if any value
    /// is returned at all.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="accessor">
    /// The delegate to invoke for each CLR member. If the delegate
    /// returns <c>null</c>, the standard evaluation is performed.
    /// </param>
    public static Options SetMemberAccessor(this Options options, Options.MemberAccessorDelegate accessor)
    {
        options.Interop.MemberAccessor = accessor;
        return options;
    }

    /// <summary>
    /// Allows scripts to call CLR types directly like <example>System.IO.File</example>
    /// </summary>
    public static Options AllowClr(this Options options, params Assembly[] assemblies)
    {
        options.Interop.Enabled = true;
        options.Interop.AllowedAssemblies.AddRange(assemblies);
        options.Interop.AllowedAssemblies = options.Interop.AllowedAssemblies.Distinct().ToList();
        return options;
    }

    public static Options AllowClrWrite(this Options options, bool allow = true)
    {
        options.Interop.AllowWrite = allow;
        return options;
    }

    public static Options AllowOperatorOverloading(this Options options, bool allow = true)
    {
        options.Interop.AllowOperatorOverloading = allow;
        return options;
    }

    /// <summary>
    /// Exceptions thrown from CLR code are converted to JavaScript errors and
    /// can be used in at try/catch statement. By default these exceptions are bubbled
    /// to the CLR host and interrupt the script execution.
    /// </summary>
    public static Options CatchClrExceptions(this Options options)
    {
        CatchClrExceptions(options, _ => true);
        return options;
    }

    /// <summary>
    /// Exceptions that thrown from CLR code are converted to JavaScript errors and
    /// can be used in at try/catch statement. By default these exceptions are bubbled
    /// to the CLR host and interrupt the script execution.
    /// </summary>
    public static Options CatchClrExceptions(this Options options, Options.ExceptionHandlerDelegate handler)
    {
        options.Interop.ExceptionHandler = handler;
        return options;
    }

    public static Options Constraint(this Options options, Constraint constraint)
    {
        if (constraint != null)
        {
            options.Constraints.Constraints.Add(constraint);
        }

        return options;
    }

    public static Options WithoutConstraint(this Options options, Predicate<Constraint> predicate)
    {
        options.Constraints.Constraints.RemoveAll(predicate);
        return options;
    }

    public static Options RegexTimeoutInterval(this Options options, TimeSpan regexTimeoutInterval)
    {
        options.Constraints.RegexTimeout = regexTimeoutInterval;
        return options;
    }


    public static Options MaxArraySize(this Options options, uint maxSize)
    {
        options.Constraints.MaxArraySize = maxSize;
        return options;
    }

    public static Options MaxJsonParseDepth(this Options options, int maxDepth)
    {
        options.Json.MaxParseDepth = maxDepth;
        return options;
    }

    public static Options SetReferencesResolver(this Options options, IReferenceResolver resolver)
    {
        options.ReferenceResolver = resolver;
        return options;
    }

    public static Options SetTypeResolver(this Options options, TypeResolver resolver)
    {
        options.Interop.TypeResolver = resolver;
        return options;
    }

    /// <summary>
    /// Registers some custom logic to apply on an <see cref="Engine"/> instance when the options
    /// are loaded.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="configuration">The action to register.</param>
    public static Options Configure(this Options options, Action<Engine> configuration)
    {
        options._configurations.Add(configuration);
        return options;
    }

    /// <summary>
    /// Allows to configure how the host is constructed.
    /// </summary>
    /// <remarks>
    /// Passed Engine instance is still in construction and should not be used during call stage.
    /// </remarks>
    public static Options UseHostFactory<T>(this Options options, Func<Engine, T> factory) where T : Host
    {
        options.Host.Factory = factory;
        return options;
    }

    /// <summary>
    /// Enables module loading in the engine via the 'require' function. By default there's no sand-boxing and
    /// you need to trust the script loading the modules not doing bad things.
    /// </summary>
    public static Options EnableModules(this Options options, string basePath, bool restrictToBasePath = true)
    {
        return EnableModules(options, new DefaultModuleLoader(basePath, restrictToBasePath));
    }

    /// <summary>
    /// Enables module loading using a custom loader implementation.
    /// </summary>
    public static Options EnableModules(this Options options, IModuleLoader moduleLoader)
    {
        options.Modules.ModuleLoader = moduleLoader;
        return options;
    }
}

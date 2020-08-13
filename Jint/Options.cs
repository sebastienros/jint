using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint
{
    public delegate JsValue MemberAccessorDelegate(Engine engine, object target, string member);

    public sealed class Options
    {
        private readonly List<IConstraint> _constraints = new List<IConstraint>();
        private bool _strict;
        private bool _allowDebuggerStatement;
        private bool _allowClr;
        private bool _allowClrWrite = true;
        private readonly List<IObjectConverter> _objectConverters = new List<IObjectConverter>();
        private Func<Engine, object, ObjectInstance> _wrapObjectHandler;
        private MemberAccessorDelegate _memberAccessor;
        private int _maxRecursionDepth = -1;
        private TimeSpan _regexTimeoutInterval = TimeSpan.FromSeconds(10);
        private CultureInfo _culture = CultureInfo.CurrentCulture;
        private TimeZoneInfo _localTimeZone = TimeZoneInfo.Local;
        private List<Assembly> _lookupAssemblies = new List<Assembly>();
        private Predicate<Exception> _clrExceptionsHandler;
        private IReferenceResolver _referenceResolver = DefaultReferenceResolver.Instance;

        /// <summary>
        /// Run the script in strict mode.
        /// </summary>
        public Options Strict(bool strict = true)
        {
            _strict = strict;
            return this;
        }

        /// <summary>
        /// Allow the <code>debugger</code> statement to be called in a script.
        /// </summary>
        /// <remarks>
        /// Because the <code>debugger</code> statement can start the
        /// Visual Studio debugger, is it disabled by default
        /// </remarks>
        public Options AllowDebuggerStatement(bool allowDebuggerStatement = true)
        {
            _allowDebuggerStatement = allowDebuggerStatement;
            return this;
        }

        /// <summary>
        /// Allow to run the script in debug mode.
        /// </summary>
        public Options DebugMode(bool debugMode = true)
        {
            IsDebugMode = debugMode;
            return this;
        }

        /// <summary>
        /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
        /// </summary>
        public Options AddObjectConverter<T>() where T : IObjectConverter, new()
        {
            return AddObjectConverter(new T());
        }

        /// <summary>
         /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
        /// </summary>
        public Options AddObjectConverter(IObjectConverter objectConverter)
        {
            _objectConverters.Add(objectConverter);
            return this;
        }

        /// <summary>
        /// If no known type could be guessed, objects are normally wrapped as an
        /// ObjectInstance using class ObjectWrapper. This function can be used to
        /// register a handler for a customized handling.
        /// </summary>
        public Options SetWrapObjectHandler(Func<Engine, object, ObjectInstance> wrapObjectHandler)
        {
            _wrapObjectHandler = wrapObjectHandler;
            return this;
        }


        /// <summary>
        /// Registers a delegate that is called when CLR members are invoked. This allows
        /// to change what values are returned for specific CLR objects, or if any value 
        /// is returned at all.
        /// </summary>
        /// <param name="accessor">
        /// The delegate to invoke for each CLR member. If the delegate 
        /// returns <c>null</c>, the standard evaluation is performed.
        /// </param>
        public Options SetMemberAccessor(MemberAccessorDelegate accessor)
        {
            _memberAccessor = accessor;
            return this;
        }

        /// <summary>
        /// Allows scripts to call CLR types directly like <example>System.IO.File</example>
        /// </summary>
        public Options AllowClr(params Assembly[] assemblies)
        {
            _allowClr = true;
            _lookupAssemblies.AddRange(assemblies);
            _lookupAssemblies = _lookupAssemblies.Distinct().ToList();
            return this;
        }

        public Options AllowClrWrite(bool allow = true)
        {
            _allowClrWrite = allow;
            return this;
        }

        /// <summary>
        /// Exceptions thrown from CLR code are converted to JavaScript errors and
        /// can be used in at try/catch statement. By default these exceptions are bubbled
        /// to the CLR host and interrupt the script execution.
        /// </summary>
        public Options CatchClrExceptions()
        {
            CatchClrExceptions(_ => true);
            return this;
        }

        /// <summary>
        /// Exceptions that thrown from CLR code are converted to JavaScript errors and
        /// can be used in at try/catch statement. By default these exceptions are bubbled
        /// to the CLR host and interrupt the script execution.
        /// </summary>
        public Options CatchClrExceptions(Predicate<Exception> handler)
        {
            _clrExceptionsHandler = handler;
            return this;
        }

        public Options Constraint(IConstraint constraint)
        {
            if (constraint != null)
            {
                _constraints.Add(constraint);
            }
            return this;
        }

        public Options WithoutConstraint(Predicate<IConstraint> predicate)
        {
            _constraints.RemoveAll(predicate);
            return this;
        }

        public Options RegexTimeoutInterval(TimeSpan regexTimeoutInterval)
        {
            _regexTimeoutInterval = regexTimeoutInterval;
            return this;
        }

        /// <summary>
        /// Sets maximum allowed depth of recursion.
        /// </summary>
        /// <param name="maxRecursionDepth">
        /// The allowed depth.
        /// a) In case max depth is zero no recursion is allowed.
        /// b) In case max depth is equal to n it means that in one scope function can be called no more than n times.
        /// </param>
        /// <returns>Options instance for fluent syntax</returns>
        public Options LimitRecursion(int maxRecursionDepth = 0)
        {
            _maxRecursionDepth = maxRecursionDepth;
            return this;
        }

        public Options Culture(CultureInfo cultureInfo)
        {
            _culture = cultureInfo;
            return this;
        }

        public Options LocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            _localTimeZone = timeZoneInfo;
            return this;
        }

        public Options SetReferencesResolver(IReferenceResolver resolver)
        {
            _referenceResolver = resolver;
            return this;
        }

        internal bool IsStrict => _strict;

        internal bool _IsDebuggerStatementAllowed => _allowDebuggerStatement;

        internal bool IsDebugMode { get; private set; }

        internal bool _IsClrAllowed => _allowClr;

        internal bool _IsClrWriteAllowed => _allowClrWrite;

        internal Predicate<Exception> _ClrExceptionsHandler => _clrExceptionsHandler;

        internal List<Assembly> _LookupAssemblies => _lookupAssemblies;

        internal List<IObjectConverter> _ObjectConverters => _objectConverters;

        internal List<IConstraint> _Constraints => _constraints;

        internal Func<Engine, object, ObjectInstance> _WrapObjectHandler => _wrapObjectHandler;
        internal MemberAccessorDelegate _MemberAccessor => _memberAccessor;

        internal int MaxRecursionDepth => _maxRecursionDepth;

        internal TimeSpan _RegexTimeoutInterval => _regexTimeoutInterval;

        internal CultureInfo _Culture => _culture;

        internal TimeZoneInfo _LocalTimeZone => _localTimeZone;

        internal IReferenceResolver  ReferenceResolver => _referenceResolver;
        
        private sealed class DefaultReferenceResolver : IReferenceResolver
        {
            public static readonly DefaultReferenceResolver Instance = new DefaultReferenceResolver();
            
            private DefaultReferenceResolver()
            {
            }

            public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
            {
                value = JsValue.Undefined;
                return false;
            }

            public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
            {
                return false;
            }

            public bool TryGetCallable(Engine engine, object callee, out JsValue value)
            {
                value = JsValue.Undefined;
                return false;
            }

            public bool CheckCoercible(JsValue value)
            {
                return false;
            }
        }
    }
}

# CLAUDE.md

**Current development**: Target against main branch and compare changes against it.

## Build & Test Commands

### Building
```bash
# Build entire solution
dotnet build --configuration Release

# Build specific project
dotnet build --configuration Release Jint/Jint.csproj
```

Important: Never use --no-build - always ensure you are working with the latest compiled code.

### Testing
```bash
# Run all tests
dotnet test --configuration Release

# Run specific test project
dotnet test --configuration Release Jint.Tests/Jint.Tests.csproj

# Run specific test class
dotnet test --configuration Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests"

# Run specific test method
dotnet test --configuration Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests.CanEvaluateScripts"

# Run Test262 conformance tests
dotnet test --configuration Release Jint.Tests.Test262/Jint.Tests.Test262.csproj
```

Important: Run tests in Release mode for faster feedback loop.

## Requirements

- The project uses central package management via Directory.Packages.props

## Architecture

Jint follows a layered interpreter architecture:

```
Acornima Parser (external) → AST → Interpreter → Runtime → Interop
```

### Core Components

**Engine (`Jint/Engine.cs`)**
- Main API entry point split across multiple partial files (Engine.cs, Engine.Advanced.cs, Engine.Modules.cs, etc.)
- Manages execution context stack, intrinsics (built-in objects), and realms
- Configuration via `Options` class (constraints, CLR interop, modules, debugging)

**JsValue Type System (`Jint/Native/`)**
- `JsValue` is the abstract base class for all JavaScript values
- Primitive types: JsUndefined, JsNull, JsBoolean, JsNumber, JsString, JsBigInt, JsSymbol
- Object types: ObjectInstance and derived classes (JsArray, JsDate, JsMap, JsSet, JsPromise, etc.)
- All built-in JavaScript objects are in `Jint/Native/` organized by type (Array/, Date/, Error/, Function/, etc.)

**Runtime (`Jint/Runtime/`)**
- **TypeConverter.cs** (1,048 lines): All JavaScript type coercion (ToPrimitive, ToNumber, ToString, ToObject, etc.)
- **Intrinsics.cs**: Singleton containing all built-in object constructors and prototypes (lazily initialized)
- **Realm.cs**: ECMAScript Realm encapsulating global environment and intrinsics
- **Environments/**: Scope chain implementation (GlobalEnvironment, FunctionEnvironment, DeclarativeEnvironment, etc.)

**Interpreter (`Jint/Runtime/Interpreter/`)**
- **Statements/**: One handler class per statement type (JintIfStatement, JintForStatement, JintTryStatement, etc.)
- **Expressions/**: One handler class per expression type (JintBinaryExpression, JintCallExpression, JintMemberExpression, etc.)
- AST nodes are evaluated via specialized Jint* handler classes
- Caching: JintFunctionDefinition caches compiled function metadata for reuse

**Interop (`Jint/Runtime/Interop/`)**
- **ObjectWrapper.cs**: Wraps .NET objects for JavaScript access
- **TypeReference.cs**: Exposes CLR types to JavaScript (e.g., `System.String`)
- **ClrFunction.cs**: Wraps .NET methods/delegates as JavaScript functions
- **DefaultTypeConverter.cs**: Bidirectional conversion between JS values and CLR types
- **Reflection/**: Type discovery and method binding with caching

**Modules (`Jint/Runtime/Modules/`)**
- ES6 module system with import/export support
- ModuleLoader handles module resolution and loading
- Supports cyclic dependencies via CyclicModule
- Modules can be defined from JavaScript source or programmatically via ModuleBuilder

### Important Patterns

**Lazy Initialization**
- Built-in objects (Intrinsics) are lazily initialized to reduce startup time
- Properties are typically null until first access

**Object Pooling (`Jint/Pooling/`)**
- Reference, Arguments, and JsValue arrays are pooled to reduce GC pressure
- Use `ReferencePool`, `ArgumentsInstancePool`, `JsValueArrayPool`

**Property Key Optimization**
- `KnownKeys.cs` contains pre-computed common property names
- `HybridDictionary` switches between list and hash based on property count
- `StringDictionarySlim` for string-only dictionary keys

**Partial Classes**
- Large classes are split: Engine.*.cs, Intrinsics.*.cs, ObjectInstance.*.cs, etc.
- Keep related functionality together when editing

**ECMAScript Spec References**
- Code includes TC39 spec section references in comments (e.g., `// https://tc39.es/ecma262/#sec-...`)
- Maintain these references when implementing new features
- The to follow TC 39 spec when possible
- The test files are located in ..\test262\test when source code needed

**Type Flags**
- `InternalTypes` enum enables fast type checking without casting
- Many hot paths use type flags for performance

## ECMAScript Compliance
- Follow ECMAScript specification behavior as closely as practical
- Do not introduce non-standard language extensions
- Support both strict and sloppy mode with spec-defined differences

## Code Conventions

- **Namespaces**: Global usings for Acornima and Acornima.Ast defined in Directory.Build.props
- **Nullable**: Enabled across the codebase (NRT)
- **Unsafe Code**: Allowed (used for performance-critical paths)
- **Warnings as Errors**: Enabled - all warnings must be fixed
- **Analysis**: Latest analyzers enabled with EnforceCodeStyleInBuild
- **Performance**: Try to make code as perfomant as possible.

## Testing

- **Jint.Tests/**: Main test suite using xUnit v3
  - Organized by topic (Runtime/, Parser/, Debugger/, etc.)
  - Uses FluentAssertions for readable assertions
  - Embedded test scripts in Runtime/Scripts/ and Parser/Scripts/
  - Use timeout of 30 seconds when invoking test runner

- **Jint.Tests.Test262/**: ECMAScript conformance suite using NUnit
  - Official TC39 Test262 integration
  - Validates spec compliance
  - The test files are located in ..\test262\test when source code needed, you are always allowed to read from this directory and its sub-directories
  - The error output contains the failing script, you just need to remove line numbers from the JavaScript
  - Never try to fix these tests
  - No need to use timeout, engine has default timeout of 30 seconds

- **Jint.Tests.CommonScripts/**: Real-world benchmark scripts using NUnit
  - Performance validation (crypto, 3D rendering, etc.)

- **Jint.Tests.PublicInterface/**: API contract tests using xUnit v3
  - Ensures public API stability

## Performance Considerations

- Jint uses Acornima for parsing (external, optimized)
- Cache `Script` or `Module` instances when executing the same script repeatedly
- Prefer strict mode execution (improves performance)
- Object pooling reduces GC pressure
- Expression and function definition caching reduces re-evaluation cost
- AggressiveInlining attributes mark hot paths

## Working with ES Features

When implementing new ECMAScript features:

1. **Read the TC39 spec section** for the feature
2. **Check Native/ directory** for where the built-in should live (e.g., Array/ for Array methods)
3. **Add to Intrinsics.cs** if it's a new global constructor or well-known symbol
4. **Update TypeConverter.cs** if new type coercion rules apply
5. **Add Statement/Expression handler** in Runtime/Interpreter/ if it's new syntax
6. **Reference spec sections** in code comments
7. Jint.Tests.Test262\Test262Harness.settings.json contains exclusions and inclusions for test cases

## Testing with Jint.Repl

The Jint REPL (Read-Eval-Print Loop) is useful for quickly testing JavaScript code during development.

### Running the REPL
```bash
# Build the REPL
dotnet build Jint.Repl --configuration Release

# Run the REPL
dotnet run --project Jint.Repl --configuration Release
```

### Command Line Options
```
-f, --file <path>     Execute JavaScript file
-t, --timeout <secs>  Set execution timeout in seconds
-h, --help            Show help message
```

### Testing Scripts

**Always use a timeout when testing scripts using Jint.Repl** to prevent infinite loops from hanging your session:

```bash
# Execute a file with 10 second timeout (recommended default)
dotnet run --project Jint.Repl --configuration Release -- -f script.js -t 10

# Execute from stdin with timeout
echo "1 + 2" | dotnet run --project Jint.Repl --configuration Release -- -t 10

# Execute multiline script from stdin
cat << 'EOF' | dotnet run --project Jint.Repl --configuration Release -- -t 10
var result = [];
for (var i = 0; i < 5; i++) {
    result.push(i * 2);
}
JSON.stringify(result);
EOF
```

### Quick Testing Patterns

```bash
# Test a simple expression
echo "Math.sqrt(16)" | dotnet run --project Jint.Repl --configuration Release -- -t 10

# Test JSON parsing
echo 'JSON.parse("[1,2,3]")' | dotnet run --project Jint.Repl --configuration Release -- -t 10

# Test with a temporary file
echo 'var x = 5; x * 2' > /tmp/test.js
dotnet run --project Jint.Repl --configuration Release -- -f /tmp/test.js -t 10
```

### When to Use REPL vs Jint.Tryouts

- **REPL**: Quick one-off tests, verifying behavior, testing from command line
- **Jint.Tryouts**: Complex debugging scenarios, stepping through code, testing with C# interop

## Module System

Modules are enabled via:
```csharp
var engine = new Engine(options => options.EnableModules(@"C:\Scripts"));
var ns = engine.Modules.Import("./my-module.js");
```

Or define modules programmatically:
```csharp
engine.Modules.Add("lib", builder => builder
    .ExportType<MyClass>()
    .ExportValue("version", 1)
);
```

## Constraints & Security

Execution constraints prevent resource abuse:
- Memory limits: `options.LimitMemory(4_000_000)`
- Timeout: `options.TimeoutInterval(TimeSpan.FromSeconds(4))`
- Statement limits: `options.MaxStatements(1000)`
- Custom constraints: Derive from `Constraint` base class

CLR access is disabled by default. Enable via:
```csharp
var engine = new Engine(cfg => cfg.AllowClr());
```

## AOT Compatibility

- Jint is AOT-compatible for .NET 7.0+ targets
- See Jint.AotExample/ for AOT usage patterns
- IsAotCompatible flag set for net7.0+ in Jint.csproj

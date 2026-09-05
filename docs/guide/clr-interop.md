# CLR interop

`SetValue` projects CLR primitives, delegates, objects, collections, and types into JavaScript.

```csharp
var person = new Person { Name = "Ada" };

var engine = new Engine()
    .SetValue("person", person)
    .SetValue("write", new Action<string>(Console.WriteLine));

engine.Execute("write(person.Name)");
```

Public properties and methods are available through the wrapper. Direct writes to CLR properties, fields,
indexers, dictionaries, lists, and live arrays are disabled by default:

```csharp
var engine = new Engine(options => options.Interop.AllowWrite = true)
    .SetValue("person", person);

engine.Execute("'use strict'; person.Name = 'Grace'");
```

`AllowWrite` does not make objects safe when false. Methods, delegates, and registered extension methods can
still mutate host state. Expose only intended capabilities to untrusted scripts.

## CLR namespaces and types

Namespace access is disabled by default. Prefer exporting individual values or types. If namespace lookup is
required, allow only the needed assemblies:

```csharp
var engine = new Engine(options =>
    options.AllowClr(typeof(MyPublicApi).Assembly));
```

An explicit type can be exported without enabling namespace lookup:

```csharp
engine.SetValue(
    "MyPublicApi",
    TypeReference.CreateTypeReference<MyPublicApi>(engine));
```

An assembly allowlist is not a complete sandbox. An allowed API may load assemblies, access files or networks,
start processes, or return more powerful objects. For untrusted code, leave CLR access off or combine a minimal
assembly list with a positive `TypeResolver.MemberFilter`, purpose-built capability objects, and process
isolation. Keep `AllowGetType` and `AllowSystemReflection` disabled unless they are explicitly required.

## Arrays

CLR arrays default to `ArrayConversionMode.Copy`: scripts receive an independent native JavaScript array.
Choose a connected, fixed-size wrapper explicitly:

```csharp
var engine = new Engine(options =>
{
    options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
    options.Interop.AllowWrite = true;
});
```

`LiveView` makes CLR-side changes visible and, with `AllowWrite`, lets script change elements. It is not a native
JavaScript array and cannot be resized.

See [Error handling](./errors.md) for exceptions crossing the interop boundary and
[Performance](./performance.md) for repeated projections.

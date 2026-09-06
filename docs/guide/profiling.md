# Profiling

Profiling is disabled by default. Enable it when constructing the engine, then bracket only the work you want
to measure.

## Sampling profiler

Sampling answers where script time went with low overhead:

```csharp
var engine = new Engine(options => options.Profiling.Enabled = true);

engine.Diagnostics.StartSampling();
engine.Execute(script, "workload.js");
var profile = engine.Diagnostics.StopSampling();

using var output = File.Create("profile.json");
profile.WriteTo(output);
```

Open the result in [Firefox Profiler](https://profiler.firefox.com/). Frames are categorized as script,
Jint built-in, or host interop. Samples are taken at interpreter check points, not by another thread, so a long
host callback or indivisible built-in appears as time at its calling frame. The default one-millisecond interval
is a floor, not a scheduling guarantee.

`SamplingOptions.MaxSamples` bounds storage; dropped samples are reported by
`SampledProfile.DroppedSampleCount`. Sampling APIs are preview and produce the `JINT0002` diagnostic unless
acknowledged.

## Evented profiler

Evented profiling records call boundaries exactly and has a cost per call:

```csharp
var engine = new Engine(options => options.Profiling.Enabled = true);

engine.Diagnostics.StartProfiling();
engine.Execute(script, "workload.js");
var profile = engine.Diagnostics.StopProfiling();

using var output = File.Create("profile.speedscope.json");
profile.WriteSpeedscopeJson(output);
```

Open the result in [speedscope](https://www.speedscope.app/). `Options.Profiling.MaxEvents` caps the session;
when reached, `ScriptProfile.Truncated` is set and the recorded event stream remains balanced.

Sampling and evented sessions are independent and may run together. Both record on the engine-owning thread;
do not inspect a running engine from another thread. Use a CLR profiler to investigate time spent entirely
inside host callbacks.

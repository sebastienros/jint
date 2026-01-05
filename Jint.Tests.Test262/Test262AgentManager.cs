#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.Test262;

/// <summary>
/// Implements the $262.agent API for Test262 multi-agent tests.
/// See https://github.com/tc39/test262/blob/main/INTERPRETING.md
/// </summary>
internal sealed class Test262AgentManager : IDisposable
{
    private readonly ConcurrentQueue<string> _reports = new();
    private readonly List<AgentWorker> _workers = [];
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private byte[]? _broadcastBufferData;
    private int _broadcastBufferLength;
    private readonly object _broadcastLock = new();
    private readonly ManualResetEventSlim _broadcastEvent = new(false);
    private int _broadcastVersion;

    /// <summary>
    /// Adds the $262.agent object to the engine's $262 object.
    /// </summary>
    public void InstallAgent(Engine engine, ObjectInstance container)
    {
        var agent = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

        // start(scriptSource) - spawn a new agent running the given script
        agent.FastSetProperty("start", new PropertyDescriptor(new ClrFunction(engine, "start",
            (_, args) =>
            {
                var scriptSource = args.At(0).AsString();
                StartAgent(scriptSource);
                return JsValue.Undefined;
            }), true, true, true));

        // broadcast(sab) - share SharedArrayBuffer with all agents
        agent.FastSetProperty("broadcast", new PropertyDescriptor(new ClrFunction(engine, "broadcast",
            (_, args) =>
            {
                var buffer = args.At(0) switch
                {
                    JsSharedArrayBuffer sab => sab,
                    JsTypedArray ta when ta._viewedArrayBuffer is JsSharedArrayBuffer sab2 => sab2,
                    _ => throw new JavaScriptException(engine.Realm.Intrinsics.TypeError, "broadcast requires SharedArrayBuffer")
                };
                Broadcast(buffer);
                return JsValue.Undefined;
            }), true, true, true));

        // getReport() - get a report from the queue (returns null if none)
        agent.FastSetProperty("getReport", new PropertyDescriptor(new ClrFunction(engine, "getReport",
            (_, _) =>
            {
                var report = GetReport();
                return report is null ? JsValue.Null : new JsString(report);
            }), true, true, true));

        // sleep(ms) - sleep for ms milliseconds
        agent.FastSetProperty("sleep", new PropertyDescriptor(new ClrFunction(engine, "sleep",
            (_, args) =>
            {
                var ms = (int)TypeConverter.ToNumber(args.At(0));
                Thread.Sleep(ms);
                return JsValue.Undefined;
            }), true, true, true));

        // monotonicNow() - high-resolution timestamp in milliseconds
        agent.FastSetProperty("monotonicNow", new PropertyDescriptor(new ClrFunction(engine, "monotonicNow",
            (_, _) =>
            {
                return MonotonicNow();
            }), true, true, true));

        container.FastSetProperty("agent", new PropertyDescriptor(agent, true, true, true));
    }

    private void StartAgent(string scriptSource)
    {
        var worker = new AgentWorker(this, scriptSource);
        lock (_workers)
        {
            _workers.Add(worker);
        }
        worker.Start();
    }

    private void Broadcast(JsSharedArrayBuffer buffer)
    {
        lock (_broadcastLock)
        {
            // Store the raw buffer data so it can be shared across threads
            _broadcastBufferData = buffer._arrayBufferData;
            _broadcastBufferLength = buffer.ArrayBufferByteLength;
            _broadcastVersion++;
            _broadcastEvent.Set();
        }
    }

    private string? GetReport()
    {
        return _reports.TryDequeue(out var report) ? report : null;
    }

    private double MonotonicNow()
    {
        return _stopwatch.Elapsed.TotalMilliseconds;
    }

    public void Dispose()
    {
        // Wait for all workers to finish
        List<AgentWorker> workers;
        lock (_workers)
        {
            workers = [.._workers];
        }

        foreach (var worker in workers)
        {
            worker.WaitForExit(TimeSpan.FromSeconds(30));
        }

        _broadcastEvent.Dispose();
    }

    private sealed class AgentWorker
    {
        private readonly Test262AgentManager _manager;
        private readonly string _scriptSource;
        private Thread? _thread;
        private int _lastBroadcastVersion;

        public AgentWorker(Test262AgentManager manager, string scriptSource)
        {
            _manager = manager;
            _scriptSource = scriptSource;
        }

        public void Start()
        {
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "Test262Agent"
            };
            _thread.Start();
        }

        public void WaitForExit(TimeSpan timeout)
        {
            _thread?.Join(timeout);
        }

        private void Run()
        {
            try
            {
                var engine = new Engine(cfg =>
                {
                    cfg.ExperimentalFeatures = ExperimentalFeature.All;
                    cfg.TimeoutInterval(TimeSpan.FromSeconds(30));
                });

                // Setup agent-side $262.agent
                var agentObj = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

                // receiveBroadcast(callback) - wait for broadcast and call callback with SAB
                agentObj.FastSetProperty("receiveBroadcast", new PropertyDescriptor(new ClrFunction(engine, "receiveBroadcast",
                    (thisValue, args) =>
                    {
                        var callback = args.At(0);
                        WaitForBroadcast(engine, callback);
                        return JsValue.Undefined;
                    }), true, true, true));

                // report(value) - send a report back to main thread
                agentObj.FastSetProperty("report", new PropertyDescriptor(new ClrFunction(engine, "report",
                    (_, args) =>
                    {
                        var value = TypeConverter.ToString(args.At(0));
                        _manager._reports.Enqueue(value);
                        return JsValue.Undefined;
                    }), true, true, true));

                // sleep(ms) - sleep for ms milliseconds
                agentObj.FastSetProperty("sleep", new PropertyDescriptor(new ClrFunction(engine, "sleep",
                    (_, args) =>
                    {
                        var ms = (int)TypeConverter.ToNumber(args.At(0));
                        Thread.Sleep(ms);
                        return JsValue.Undefined;
                    }), true, true, true));

                // leaving() - signal agent is done
                agentObj.FastSetProperty("leaving", new PropertyDescriptor(new ClrFunction(engine, "leaving",
                    (_, _) =>
                    {
                        // Nothing special needed here
                        return JsValue.Undefined;
                    }), true, true, true));

                // monotonicNow() - high-resolution timestamp in milliseconds
                agentObj.FastSetProperty("monotonicNow", new PropertyDescriptor(new ClrFunction(engine, "monotonicNow",
                    (_, _) =>
                    {
                        return _manager.MonotonicNow();
                    }), true, true, true));

                var container = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
                container.FastSetProperty("agent", new PropertyDescriptor(agentObj, true, true, true));
                engine.SetValue("$262", container);

                // Execute the agent script
                var script = Engine.PrepareScript(_scriptSource, options: new ScriptPreparationOptions
                {
                    ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
                });
                engine.Execute(script);
            }
            catch (Exception ex)
            {
                // Report any exceptions back
                _manager._reports.Enqueue($"AGENT ERROR: {ex.Message}");
            }
        }

        private void WaitForBroadcast(Engine engine, JsValue callback)
        {
            while (true)
            {
                _manager._broadcastEvent.Wait();

                byte[]? bufferData;
                int bufferLength;
                int version;

                lock (_manager._broadcastLock)
                {
                    bufferData = _manager._broadcastBufferData;
                    bufferLength = _manager._broadcastBufferLength;
                    version = _manager._broadcastVersion;
                }

                if (bufferData is not null && version > _lastBroadcastVersion)
                {
                    _lastBroadcastVersion = version;

                    // Create a SharedArrayBuffer instance in this engine that shares the same data
                    var sab = CreateSharedArrayBuffer(engine, bufferData, bufferLength);

                    // Call the callback with the SAB
                    if (callback is ICallable callable)
                    {
                        callable.Call(JsValue.Undefined, [sab]);
                    }

                    break;
                }

                Thread.Sleep(1);
            }
        }

        private static JsSharedArrayBuffer CreateSharedArrayBuffer(Engine engine, byte[] data, int byteLength)
        {
            // Create a new SharedArrayBuffer that shares the same underlying byte array
            // Get prototype via the constructor's "prototype" property
            var constructor = engine.Realm.Intrinsics.SharedArrayBuffer;
            var prototype = (ObjectInstance)constructor.Get(CommonProperties.Prototype);
            var sab = new JsSharedArrayBuffer(engine, data, null, (uint)byteLength)
            {
                _prototype = prototype
            };
            return sab;
        }
    }
}

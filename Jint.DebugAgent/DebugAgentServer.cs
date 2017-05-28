using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Jint.DebugAgent.Domains;
using Jint.Runtime.Debugger;
using Newtonsoft.Json.Linq;

namespace Jint.DebugAgent
{
    /// <summary>
    /// The Debug Agent Server provides a CHrome Debug Protocol Server for one or more Jint engines.
    /// The code loaded into the registered engines will be transmitted to the debugging tools and allow setting breakpoints and stepping through code.
    /// To stop the server, dispose the instance.
    /// </summary>
    /// <remarks>
    /// Because of the asynchronous nature o the debugging protocol, it may happen that pre-set breakpoints are transmitted by the debugger
    /// after the code has already been run; in this case either delay the execution or use the <c>debugger</c> statement to force breaking into the debugger
    /// </remarks>
    public class DebugAgentServer : IDisposable, IDebugAgent, IProtocolServerOwner
    {
        [Flags]
        public enum Options
        {
            None=0x00,
            /// <summary>
            /// wait for debugger to connect before executing the first statement
            /// </summary>
            WaitForDebuggerOnFirstStatement=0x01,
            /// <summary>
            /// Halts on the first executed statement, with or without est breakpoint
            /// </summary>
            HaltOnFirstStatement=0x02,
            /// <summary>
            /// If Debugger gets disconnected, wait on next breakpoint or step for debugger to reconnect again. 
            /// If not set, execution is continued while debugger is not connected. If reconnect happens, break is done on next breakpoint or debugger statement.
            /// </summary>
            WaitForDebuggerReconnect=0x04,
        }

        private readonly List<WeakReference<Engine>> engines = new List<WeakReference<Engine>>();
        private readonly CancellationTokenSource serverCancellation;
        private ChromeDebugProtocolServer debuggingServer;
        private readonly DebuggerDomain debuggerDomain;
        private readonly RuntimeDomain runtimeDomain;

        /// <summary>
        /// Create the server and expose the engines registered. Additional engines may be added afterwards.
        /// </summary>
        /// <remarks>
        /// The default port is 9222.
        /// To attach the chrome debugger, use the following URL to start the dev tools and connect to the server:
        /// "chrome-devtools://devtools/bundled/inspector.html?ws=localhost:9222"
        /// </remarks>
        public DebugAgentServer(Options options = Options.None, params Engine[] engines) : this(options, 9222, engines) { }
        /// <summary>
        /// Create the server and expose the engines registered. Additional engines may be added afterwards.
        /// </summary>
        /// <remarks>
        /// The default port is 9222.
        /// To attach the chrome debugger, use the following URL to start the dev tools and connect to the server:
        /// "chrome-devtools://devtools/bundled/inspector.html?ws=localhost:9222"
        /// </remarks>
        public DebugAgentServer(Options options = Options.None, int port = 9222, params Engine[] engines)
        {
            this.runtimeDomain = new RuntimeDomain(this, options.HasFlag(Options.WaitForDebuggerOnFirstStatement));
            this.debuggerDomain = new DebuggerDomain(this, options.HasFlag(Options.HaltOnFirstStatement), options.HasFlag(Options.WaitForDebuggerReconnect), this.runtimeDomain);

            Array.ForEach(engines, this.AddEngine);

            //Start the chrome debug protocol server
            this.serverCancellation = new CancellationTokenSource();
            Task.Run(async () =>
            {
                this.debuggingServer = new ChromeDebugProtocolServer(this, this.debuggerDomain, this.runtimeDomain);
                await debuggingServer.Start(this.serverCancellation.Token, $"http://localhost:{port}/", $"http://127.0.0.1:{port}/");
            }).ContinueWith(_ =>
            {
                if (_.IsFaulted)
                {
                    Debug.Fail(_.Exception?.Flatten().InnerExceptions.FirstOrDefault()?.Message);
                }
            });
        }

        /// <summary>
        /// Register an additional Jint engine for debugging
        /// </summary>
        [PublicAPI]
        public void AddEngine(Engine engine)
        {
            this.engines.Add(new WeakReference<Engine>(engine));
            engine.Step += Engine_Step;
            engine.Break += Engine_Break;
            engine.ExceptionThrown += Engine_ExceptionThrown;
            engine.Parse += Engine_Parse;
        }

        public void Dispose()
        {
            this.serverCancellation.Cancel();
        }

        private StepMode Engine_ExceptionThrown(object sender, DebugInformation e)
        {
            return this.debuggerDomain.NotifyExceptionThrown(e);
        }

        private void Engine_Parse(object sender, SourceInformation e)
        {
            this.debuggerDomain.NotifyParse((Engine) sender, e);
        }

        private StepMode Engine_Step(object sender, DebugInformation e)
        {
            this.runtimeDomain.NotifyStep();
            return this.debuggerDomain.NotifyStep(e);
        }

        private StepMode Engine_Break(object sender, DebugInformation e)
        {
            return this.debuggerDomain.NotifyBreak(e);
        }

        public void Transmit(string domain, string method, object parameter )
        {
            this.debuggingServer.Transmit(domain, method, JObject.FromObject(parameter));
        }

        public Engine GetEngine(int engineId)
        {
            return this.GetEngines().FirstOrDefault(_ => _.GetHashCode() == engineId);
        }

        public IEnumerable<Engine> GetEngines()
        {
            foreach (WeakReference<Engine> EngineReference in this.engines.ToArray())
            {
                Engine Engine;
                if (EngineReference.TryGetTarget(out Engine))
                {
                    yield return Engine;
                }
                else
                {
                    this.engines.Remove(EngineReference);
                }
            }
        }

        public void NotifyConnected()
        {
        }

        public void NotifyDisconnected()
        {
            this.runtimeDomain.NotifyDisconnected();
            this.debuggerDomain.NotifyDisconnected();
        }
    }
}

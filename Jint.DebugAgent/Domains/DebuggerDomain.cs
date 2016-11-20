using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Jint.Native;
using Jint.Native.Object;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using ExecutionContext = Jint.Runtime.Environments.ExecutionContext;
using StackFrame = Jint.Runtime.Debugger.StackFrame;

namespace Jint.DebugAgent.Domains
{
    /// <summary>
    /// Implements commands from the 'Debugger' Domain
    /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/
    /// </summary>
    internal class DebuggerDomain : DomainBase
    {
        private class SourceData
        {
            private readonly WeakReference<string> source;
            public int EngineId { get; }
            public Statement[] Statements { get; }

            public string Source
            {
                get
                {
                    string Source;
                    return this.source.TryGetTarget(out Source) ? Source : null;
                }
            }

            public bool IsGarbageCollected
            {
                get
                {
                    string Source;
                    return this.source.TryGetTarget(out Source)==false;
                }
            }

            public SourceData(int engineId, string source, Statement[] statements)
            {
                this.source = new WeakReference<string>(source);
                EngineId = engineId;
                Statements = statements;
            }
        }

        private readonly Dictionary<string, SourceData> sources = new Dictionary<string, SourceData>();
        private readonly AutoResetEvent continueExecutionEvent = new AutoResetEvent(true);
        private StepMode nextStepMode = StepMode.None;
        private readonly RuntimeDomain runtimeDomain;
        private DebugInformation currentDebugInformation;
        private DebuggerDomainProtocol.PauseOnExceptionMode pauseOnExceptionMode;
        private bool skipAllPauses;
        private bool deactivateBreakpoints;
        private bool isDebuggerConnected;
        private readonly object debuggerLock=new object();
        private readonly bool waitForDebuggerReconnect;

        public DebuggerDomain(IDebugAgent debugAgent, bool haltOnFirstStatement, bool waitForDebuggerReconnect, RuntimeDomain runtimeDomain) : base("Debugger", debugAgent)
        {
            this.runtimeDomain = runtimeDomain;
            this.waitForDebuggerReconnect = waitForDebuggerReconnect;
            if (haltOnFirstStatement)
            {
                this.continueExecutionEvent.Reset();
            }
        }

        public void NotifyDisconnected()
        {
            lock (this.debuggerLock)
            {
                this.isDebuggerConnected = false;
                if (this.waitForDebuggerReconnect == false)
                {
                    this.SetNextStepModeAndContinueExecution(StepMode.None);
                }
            }
        }

        public StepMode NotifyBreak(DebugInformation debugInformation)
        {
            if (this.skipAllPauses || this.deactivateBreakpoints || this.isDebuggerConnected==false)
            {
                return debugInformation.StepMode;
            }
            else
            {
                this.continueExecutionEvent.Reset();

                SendPausedEvent("debugCommand", debugInformation);
                this.currentDebugInformation = debugInformation;

                this.continueExecutionEvent.WaitOne();

                this.currentDebugInformation = null;
                SendResumedEvent();
                return this.nextStepMode;
            }
        }

        public StepMode NotifyStep(DebugInformation debugInformation)
        {
            if (this.isDebuggerConnected == false)
            {
                this.currentDebugInformation = debugInformation;
                //the event is reset if wait for debugger is enabled
                this.continueExecutionEvent.WaitOne();
                this.currentDebugInformation = null;

                return StepMode.None;
            }
            else
            {
                SendPausedEvent("debugCommand", debugInformation);
                this.currentDebugInformation = debugInformation;

                if (debugInformation.Engine.BreakPoints?.Any(_ => BreakpointMatchesStatement(_, debugInformation.CurrentStatement, debugInformation.Engine)) == true)
                {
                    //if breakpoint was just set during the execution, still halt now
                    this.continueExecutionEvent.Reset();
                }

                this.continueExecutionEvent.WaitOne();

                this.currentDebugInformation = null;
                SendResumedEvent();
                return this.nextStepMode;
            }
        }

        public StepMode NotifyExceptionThrown(DebugInformation debugInformation)
        {
            if (this.skipAllPauses || this.isDebuggerConnected == false)
            {
                return debugInformation.StepMode;
            }
            else
            {
                if (this.pauseOnExceptionMode == DebuggerDomainProtocol.PauseOnExceptionMode.All ||
                    (this.pauseOnExceptionMode == DebuggerDomainProtocol.PauseOnExceptionMode.Uncaught && debugInformation.UncaughtException))
                {
                    this.continueExecutionEvent.Reset();

                    SendPausedEvent("exception", debugInformation);
                    this.currentDebugInformation = debugInformation;

                    this.continueExecutionEvent.WaitOne();

                    this.currentDebugInformation = null;
                    SendResumedEvent();
                    return this.nextStepMode;
                }
                else
                {
                    return debugInformation.StepMode;
                }
            }
        }

        public void NotifyParse(Engine engine, SourceInformation sourceInformation)
        {
            lock (this.debuggerLock)
            {
                string Source = sourceInformation.Source;
                this.sources.Add(sourceInformation.Name, new SourceData(engine.GetHashCode(), Source, sourceInformation.Statements));
                if (this.isDebuggerConnected)
                {
                    SendScriptParsedEvent(engine.GetHashCode(), sourceInformation.Name, Source);
                }
            }
        }

        private bool BreakpointMatchesStatement(BreakPoint breakpoint, Statement statement, Engine engine)
        {
            if (breakpoint.Statement == statement)
            {
                return string.IsNullOrEmpty(breakpoint.Condition) || engine.ExecuteWithoutDebugging(breakpoint.Condition).GetCompletionValue().AsBoolean();
            }
            else
            {
                return false;
            }
        }
        #region Domain Events
        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#event-paused
        /// </summary>
        private void SendPausedEvent(string reason, DebugInformation debugInformation)
        {
            this.Transmit("paused", new DebuggerDomainProtocol.PausedEventParameters
            {
                callFrames=CreateCallFrames(debugInformation.Engine, debugInformation.CallStack, debugInformation.CurrentStatement),
                reason= reason,
                hitBreakpoints=debugInformation.BreakPoint != null ? new[] {GetIdFromHash(debugInformation.BreakPoint) } : null,
                data = CreateExceptionAuxData(debugInformation.Exception)
            });
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#event-resumed
        /// </summary>
        private void SendResumedEvent()
        {
            this.Transmit("resumed", new Dictionary<string, object>());
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#event-scriptParsed
        /// </summary>
        private void SendScriptParsedEvent(int engineId, string name, string source)
        {
            string[] SourceLines = source.Split('\n', '\r');

            this.Transmit("scriptParsed", new DebuggerDomainProtocol.ScriptParsedEventParameter 
            {
                url=name,
                scriptId=name,
                startLine=0,
                startColumn=0,
                endLine=SourceLines.Length,
                endColumn=SourceLines.Last().Length,
                executionContextId=engineId,
                hash=GetIdFromHash(source)
            });
        }

        #endregion

        #region Domain Methods
        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-stepOver
        /// </summary>
        [UsedImplicitly]
        private void StepOver()
        {
            SetNextStepModeAndContinueExecution(StepMode.Over);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-stepInto
        /// </summary>
        [UsedImplicitly]
        private void StepInto()
        {
            SetNextStepModeAndContinueExecution(StepMode.Into);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-stepOut
        /// </summary>
        [UsedImplicitly]
        private void StepOut()
        {
            SetNextStepModeAndContinueExecution(StepMode.Out);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-pause
        /// </summary>
        [UsedImplicitly]
        private void Pause()
        {
            this.continueExecutionEvent.Reset();
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-resume
        /// </summary>
        [UsedImplicitly]
        private void Resume()
        {
            SetNextStepModeAndContinueExecution(StepMode.None);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-enable
        /// </summary>
        [UsedImplicitly]
        private void Enable()
        {
            lock (this.debuggerLock)
            {
                this.isDebuggerConnected = true;

                foreach (KeyValuePair<string, SourceData> Source in this.GetScripts())
                {
                    this.SendScriptParsedEvent(Source.Value.EngineId, Source.Key, Source.Value.Source);
                }
                if (this.currentDebugInformation != null)
                {
                    this.SendPausedEvent("debugCommand",this.currentDebugInformation);
                }
            }
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-getScriptSource
        /// </summary>
        [UsedImplicitly]
        private DebuggerDomainProtocol.GetScriptSourceResult GetScriptSource(string scriptId)
        {
            string Source = this.GetScript(scriptId)?.Source ?? "<unloaded>";
            return new DebuggerDomainProtocol.GetScriptSourceResult  {scriptSource= Source};
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-setBreakpointByUrl
        /// </summary>
        [UsedImplicitly]
        private DebuggerDomainProtocol.SetBreakpointResult SetBreakpointByUrl(string url, int lineNumber, int columnNumber)
        {
            SourceData SourceData = this.GetScript(url);
            int EngineId = SourceData.EngineId;
            Engine Engine = this.GetEngine(EngineId);
            Statement NearestStatement = null;
            string BreakPointId = null;
            if (Engine != null)
            {
                IEnumerable<Statement> FlattenedStatements = GetFlattenedStatements(SourceData.Statements)
                    .OrderByDescending(_ => _.Location.Start.Line).ThenByDescending(_ => _.Location.Start.Column);
                //Get statement nearest to the requested position. Careful: Parser line numbers are 1 based, not 0 based!
                NearestStatement = FlattenedStatements.FirstOrDefault(_ => lineNumber >= _.Location.Start.Line-1) ??FlattenedStatements.Last();

                if (NearestStatement != null)
                {
                    BreakPoint BreakPoint = new BreakPoint(NearestStatement);
                    Engine.BreakPoints.Add(BreakPoint);
                    BreakPointId = GetIdFromHash(BreakPoint);
                }
            }
            if (NearestStatement != null)
            {
                return new DebuggerDomainProtocol.SetBreakpointResult
                {
                    breakpointId= BreakPointId,
                    locations= new[] {this.CreateLocation(NearestStatement.Location.Start, this.GetScriptId(NearestStatement.Location.Source)) }
                    
                };
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-removeBreakpoint
        /// </summary>
        [UsedImplicitly]
        private void RemoveBreakpoint(string breakpointId)
        {
            Tuple<Engine, BreakPoint> BreakPointInfo = (from Engine in this.GetEngines()
                from BreakPoint in Engine.BreakPoints
                where GetIdFromHash(BreakPoint) == breakpointId
                select new Tuple<Engine, BreakPoint>(Engine, BreakPoint)).FirstOrDefault();
            BreakPointInfo?.Item1.BreakPoints.Remove(BreakPointInfo.Item2);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/tot/Debugger/#method-setBreakpointsActive
        /// </summary>
        [UsedImplicitly]
        private void SetBreakpointsActive(bool active)
        {
            this.deactivateBreakpoints = !active;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-setPauseOnExceptions
        /// </summary>
        [UsedImplicitly]
        private void SetPauseOnExceptions(string state)
        {
            this.pauseOnExceptionMode = (DebuggerDomainProtocol.PauseOnExceptionMode)Enum.Parse(typeof(DebuggerDomainProtocol.PauseOnExceptionMode),state,true);
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/tot/Debugger/#method-setSkipAllPauses
        /// </summary>
        [UsedImplicitly]
        private void SetSkipAllPauses(bool skip)
        {
            this.skipAllPauses = skip;
        }

        /// <summary>
        /// https://chromedevtools.github.io/debugger-protocol-viewer/1-2/Debugger/#method-evaluateOnCallFrame
        /// </summary>
        [UsedImplicitly]
        private DebuggerDomainProtocol.EvaluateOnCallFrameResult EvaluateOnCallFrame( string callFrameId, string expression )
        {
            int FrameNumber = int.Parse(callFrameId);
            if (FrameNumber == 0)
            {
                try
                {
                    JsValue Result = this.currentDebugInformation.Engine.ExecuteWithoutDebugging(expression).GetCompletionValue();
                    return new DebuggerDomainProtocol.EvaluateOnCallFrameResult
                    {
                        result = this.runtimeDomain.GetRemoteObject(Result)
                    };
                }
                catch (JavaScriptException Exception)
                {
                    /*return new DebuggerDomainProtocol.EvaluateOnCallFrameResult
                    {
                        exceptionDetails = new RuntimeDomainProtocol.ExceptionDetails {exception =}
                    }*/
                    return new DebuggerDomainProtocol.EvaluateOnCallFrameResult
                    {
                        result = this.runtimeDomain.GetRemoteObject(JsValue.FromObject(this.currentDebugInformation.Engine, $"<{Exception.Error.AsObject().GetProperty("message").Value}>"))
                    };
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return new DebuggerDomainProtocol.EvaluateOnCallFrameResult
                {
                    result = this.runtimeDomain.GetRemoteObject(JsValue.FromObject(this.currentDebugInformation.Engine,"<Evaluation is only supported on the top stack frame>"))
                };
            }
        }

        private void SetNextStepModeAndContinueExecution(StepMode mode)
        {
            this.nextStepMode = mode;
            this.continueExecutionEvent.Set();
        }
        #endregion

        private IEnumerable<DebuggerDomainProtocol.CallFrame> CreateCallFrames(Engine engine, StackFrame[] callStack, Statement currentStatement)
        {
            Statement CurrentStatement = currentStatement;
            int Index = 0;

            //frame Execution context is the one of the calling method. engine execution context is the current one
            ExecutionContext Context= engine.ExecutionContext;
            foreach (StackFrame Frame in callStack)
            {
                yield return CreateCallFrame(engine, Index++, Frame.ToString(), CurrentStatement, Context);
                Context = Frame.ExecutionContext;
            }
            yield return CreateCallFrame(engine, Index, "<entry>", CurrentStatement, Context);
        }

        private DebuggerDomainProtocol.CallFrame CreateCallFrame(Engine engine, int index, string frameName, Statement currentStatement, ExecutionContext executionContext)
        {
            return new DebuggerDomainProtocol.CallFrame
            {
                callFrameId= index.ToString(),
                functionName= frameName,
                location= this.CreateLocation(currentStatement.Location.Start,this.GetScriptId(currentStatement.Location.Source)),
                scopeChain= new[] {
                    this.CreateScope(true, engine.GlobalEnvironment.Record, "Global scope"),
                    executionContext.LexicalEnvironment.Outer!=null
                    ?this.CreateScope(false, executionContext.LexicalEnvironment.Record, "Local scope"):null
                }.Where(_=>_!=null).ToArray() ,
                @this= this.runtimeDomain.GetRemoteObject(executionContext.ThisBinding)
            };
        }

        private DebuggerDomainProtocol.Scope CreateScope(bool global, ObjectInstance value, string description)
        {
            return new DebuggerDomainProtocol.Scope
            {
                type= global ? "global":"local" , /*global, local, with, closure, catch, block, script.*/
                @object= this.runtimeDomain.GetRemoteObject(value,description),
            };
        }



        private DebuggerDomainProtocol.Location CreateLocation(Position position, string scriptId)
        {
            return
                new
                    DebuggerDomainProtocol.Location
                {
                    scriptId = scriptId,
                    lineNumber = position.Line - 1,
                    columnNumber =position.Column
                };
        }

        private DebuggerDomainProtocol.AuxData CreateExceptionAuxData(JavaScriptException exception)
        {
            if (exception != null)
            {
                return new DebuggerDomainProtocol.AuxData
                {
                    description = exception.Message
                };
            }
            else
            {
                return null;
            }
        }

        private IEnumerable<Statement> GetFlattenedStatements(IEnumerable<Statement> statements)
        {
            foreach (Statement Statement in statements)
            {
                yield return Statement;
                IEnumerable<Statement> InnerStatements;

                if (Statement is BlockStatement)
                {
                    InnerStatements = ((BlockStatement) Statement).Body;
                }
                else if (Statement is CatchClause )
                {
                    InnerStatements = new[] {((CatchClause) Statement).Body};
                }
                else if (Statement is DoWhileStatement)
                {
                    InnerStatements = new[] {((DoWhileStatement) Statement).Body};
                }
                else if (Statement is ForInStatement)
                {
                    InnerStatements = new[] {((ForInStatement) Statement).Body};
                }
                else if (Statement is ForStatement)
                {
                    InnerStatements = new[] {((ForStatement) Statement).Body};
                }
                else if (Statement is FunctionDeclaration)
                {
                    InnerStatements = new[] {((FunctionDeclaration) Statement).Body};
                }
                else if (Statement is IfStatement)
                {
                    InnerStatements = new[] {((IfStatement) Statement).Consequent,((IfStatement) Statement).Alternate};
                }
                else if (Statement is LabelledStatement)
                {
                    InnerStatements = new[] {((LabelledStatement) Statement).Body};
                }
                else if (Statement is Program)
                {
                    InnerStatements = ((Program) Statement).Body;
                }
                else if (Statement is VariableDeclaration)
                {
                    InnerStatements = ((VariableDeclaration) Statement).Declarations.Select(_=>(_.Init as FunctionExpression)?.Body).Where(_=>_!=null);
                }
                else if (Statement is TryStatement)
                {
                    InnerStatements = new[] { ((TryStatement)Statement).Block, ((TryStatement)Statement).Finalizer};
                }
                else if (Statement is WhileStatement)
                {
                    InnerStatements = new[] { ((WhileStatement)Statement).Body};
                }
                else if (Statement is WithStatement)
                {
                    InnerStatements = new[] {((WithStatement) Statement).Body};
                }
                else
                {
                    InnerStatements = null;
                }


                if (InnerStatements != null)
                {
                    foreach (Statement InnerStatement in GetFlattenedStatements(InnerStatements))
                    {
                        yield return InnerStatement;
                    }
                }
            } 
        }

        private string GetScriptId(string source)
        {
            return this.GetScripts().Where(_ => _.Value.Source == source).Select(_ => _.Key).FirstOrDefault();
        }

        private SourceData GetScript(string scriptId)
        {
            return this.GetScripts().Where(_=>_.Key==scriptId).Select(_=>_.Value).FirstOrDefault();
        }

        private IEnumerable<KeyValuePair<string, SourceData>> GetScripts()
        {
            foreach (KeyValuePair<string, SourceData> Script in this.sources.ToArray())
            {
                if (Script.Value.IsGarbageCollected)
                {
                    this.sources.Remove(Script.Key);
                }
                else
                {
                    yield return Script;
                }
            }
        }
    }
}
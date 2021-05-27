using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint
{
    /// <summary>
    /// Allows setting values and evaluating statements and expressions inside engine context.
    /// </summary>
    public interface IScriptExecutor : IDisposable
    {
        /// <summary>
        /// Sets variable to engine's global scope.
        /// </summary>
        /// <param name="name">Variable's name.</param>
        /// <param name="value">Variable's value.</param>
        void SetValue(string name, object value);

        /// <summary>
        /// Sets variable to engine's global scope.
        /// </summary>
        /// <param name="name">Variable's name.</param>
        /// <param name="value">Variable's value.</param>
        void SetValue(string name, int value);
        
        /// <summary>
        /// Sets variable to engine's global scope.
        /// </summary>
        /// <param name="name">Variable's name.</param>
        /// <param name="value">Variable's value.</param>
        void SetValue(string name, double value);
        
        /// <summary>
        /// Sets variable to engine's global scope.
        /// </summary>
        /// <param name="name">Variable's name.</param>
        /// <param name="value">Variable's value.</param>
        void SetValue(string name, string value);
        
        /// <summary>
        /// Sets variable to engine's global scope.
        /// </summary>
        /// <param name="name">Variable's name.</param>
        /// <param name="value">Variable's value.</param>
        void SetValue(string name, bool value);

        /// <summary>
        /// Evaluates given source script and returns last evaluation result.
        /// </summary>
        /// <param name="source">Script to parse and evaluate.</param>
        /// <param name="parserOptions">Parsing options to use.</param>
        /// <returns>The last value that script evaluation produced.</returns>
        JsValue Evaluate(string source, ParserOptions parserOptions = null);
        
        /// <summary>
        /// Evaluates given script, ideal for running already parsed scripts when parsing overhead should be avoided.
        /// </summary>
        /// <param name="script">Pre-parsed script to run.</param>
        /// <returns>The last value that script evaluation produced.</returns>
        JsValue Evaluate(Script script);
    }
}
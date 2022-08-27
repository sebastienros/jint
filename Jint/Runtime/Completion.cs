using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime
{
    public enum CompletionType : byte
    {
        Normal = 0,
        Return = 1,
        Throw = 2,
        Break,
        Continue
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-completion-record-specification-type
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Completion
    {
        internal static readonly Node _emptyNode = new Identifier("");
        private static readonly Completion _emptyCompletion = new(CompletionType.Normal, null!, _emptyNode);

        internal readonly Node _source;

        internal Completion(CompletionType type, JsValue value, string? target, Node source)
        {
            Type = type;
            Value = value;
            Target = target;
            _source = source;
        }

        public Completion(CompletionType type, JsValue value, Node source)
            : this(type, value, null, source)
        {
        }

        public Completion(CompletionType type, string target, Node source)
            : this(type, null!, target, source)
        {
        }

        internal Completion(in ExpressionResult result)
        {
            Type = (CompletionType) result.Type;
            // this cast protects us from getting from type
            Value = (JsValue) result.Value;
            Target = null;
            _source = result._source;
        }

        public readonly CompletionType Type;
        public readonly JsValue Value;
        public readonly string? Target;
        public ref readonly Location Location => ref _source.Location;

        public static Completion Normal(JsValue value, Node source)
            => new Completion(CompletionType.Normal, value, source);

        public static ref readonly Completion Empty() => ref _emptyCompletion;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetValueOrDefault()
        {
            return Value ?? Undefined.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAbrupt()
        {
            return Type != CompletionType.Normal;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-updateempty
        /// </summary>
        internal Completion UpdateEmpty(JsValue value)
        {
            if (Value is not null)
            {
                return this;
            }

            return new Completion(Type, value, Target, _source);
        }
    }
}

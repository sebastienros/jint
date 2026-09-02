using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Diagnostics;

/// <summary>
/// Describes any <see cref="JsValue"/> without running script: no getter is invoked, no proxy trap fires,
/// and the walk is bounded in depth, entry count and string length.
/// </summary>
/// <remarks>
/// <para>
/// Written for a debugger, a log line and a protocol preview — anywhere a value has to be shown to somebody
/// while the engine is paused, or while a bounded host cannot afford the value to run anything back. What it
/// answers is a <see cref="ValueDescription"/>, a snapshot of strings holding no <see cref="JsValue"/> at
/// all, so it may be kept and read after the call returns.
/// </para>
/// <para>
/// The one invariant is the whole point of it: <b>describing a value executes none of that value's code</b>.
/// An accessor property is reported rather than called, a proxy is described by its kind alone because every
/// trap on it is script, a CLR value is named rather than read, and every well-known exotic answers from its
/// internal slots rather than through the configurable property of the same name.
/// </para>
/// <para>
/// Pass the value on the thread that owns its engine, as with every other <see cref="JsValue"/>; the
/// description that comes back has no such constraint.
/// </para>
/// <para>
/// <b>This is a preview surface and not part of Jint's compatibility contract</b>, declared to the compiler
/// as <c>JINT0002</c>; see <see cref="JintDiagnosticIds"/> for how a host acknowledges it.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public static class ValueInspector
{
    /// <summary>The hard ceiling on <see cref="ValueInspectorOptions.MaxDepth"/>, whatever a caller asks for.</summary>
    private const int DepthCeiling = 32;

    private static readonly ValueInspectorOptions Defaults = new();

    private static readonly ValueDescription UndefinedDescription = new(ValueKind.Undefined, "undefined");

    /// <summary>
    /// Returns a bounded, getter-free description of <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to describe.</param>
    /// <param name="options">The bounds to work within, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static ValueDescription Describe(JsValue value, ValueInspectorOptions? options = null)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(nameof(value));
        }

        var walker = new Walker(options ?? Defaults);
        return walker.Describe(value, depth: 0);
    }

    /// <summary>
    /// One description walk. A struct because it lives for exactly the call, carrying the bounds plus the
    /// path being walked — which is what stops a cycle once a caller has raised the depth bound.
    /// </summary>
    private struct Walker
    {
        private readonly int _maxEntries;
        private readonly int _maxDepth;
        private readonly int _maxStringLength;
        private List<ObjectInstance>? _path;

        internal Walker(ValueInspectorOptions options)
        {
            _maxEntries = System.Math.Max(0, options.MaxEntries);
            _maxDepth = System.Math.Clamp(options.MaxDepth, 0, DepthCeiling);
            _maxStringLength = System.Math.Max(0, options.MaxStringLength);
            _path = null;
        }

        internal ValueDescription Describe(JsValue value, int depth)
        {
            switch (value)
            {
                case JsUndefined:
                    return UndefinedDescription;
                case JsNull:
                    return new ValueDescription(ValueKind.Null, "null");
                case JsBoolean:
                    return new ValueDescription(ValueKind.Boolean, TypeConverter.ToString(value));
                case JsNumber:
                    return new ValueDescription(ValueKind.Number, TypeConverter.ToString(value));
                case JsBigInt:
                    return new ValueDescription(ValueKind.BigInt, TypeConverter.ToString(value) + "n");
                case JsString text:
                    return new ValueDescription(ValueKind.String, Truncate(text.ToString()));
                case JsSymbol symbol:
                    return new ValueDescription(ValueKind.Symbol, symbol.ToString());
                case ObjectInstance obj:
                    return DescribeObject(obj, depth);
                default:
                    return new ValueDescription(ValueKind.Object, TypeConverter.ToString(value));
            }
        }

        private ValueDescription DescribeObject(ObjectInstance obj, int depth)
        {
            // A proxy is named and not walked: `ownKeys` and `getOwnPropertyDescriptor` are traps, and a
            // trap is script -- the one thing this class promises never to run.
            if (obj is JsProxy proxy)
            {
                var revoked = ValueSlotReader.ProxyTarget(proxy) is null;
                return new ValueDescription(ValueKind.Proxy, revoked ? "Proxy (revoked)" : "Proxy", "Proxy");
            }

            // A CLR value is named and not read: every member of it is host code, which may throw, block or
            // observe, and none of that belongs in a description somebody asked for while paused.
            if (obj is TypeReference typeReference)
            {
                return HostObjectDescription(typeReference.ReferenceType);
            }

            if (obj is IObjectWrapper wrapper)
            {
                return HostObjectDescription(wrapper.Target?.GetType());
            }

            if (obj is Function function)
            {
                return new ValueDescription(ValueKind.Function, FunctionText(function), ValueSlotReader.ConstructorName(obj));
            }

            // Everything below may carry entries, so it takes part in both the cycle and the depth bound.
            if (_path is not null && _path.Contains(obj))
            {
                return new ValueDescription(KindOf(obj), "[Circular]", ValueSlotReader.ConstructorName(obj));
            }

            _path ??= new List<ObjectInstance>();
            _path.Add(obj);
            try
            {
                return DescribeWalkable(obj, depth);
            }
            finally
            {
                _path.RemoveAt(_path.Count - 1);
            }
        }

        private ValueDescription DescribeWalkable(ObjectInstance obj, int depth)
        {
            var className = ValueSlotReader.ConstructorName(obj);

            switch (obj)
            {
                case JsDate date:
                    return new ValueDescription(ValueKind.Date, ValueSlotReader.DateText(date), className);

                case JsRegExp regExp:
                    return new ValueDescription(ValueKind.RegExp, ValueSlotReader.RegExpText(regExp), className);

                case JsArrayBuffer buffer:
                    return DescribeArrayBuffer(buffer, className);

                case JsDataView view:
                    return new ValueDescription(ValueKind.DataView, "DataView(" + Text(view._byteLength) + ")", className);

                case JsWeakMap:
                    return new ValueDescription(ValueKind.WeakMap, "WeakMap", className);
                case JsWeakSet:
                    return new ValueDescription(ValueKind.WeakSet, "WeakSet", className);
                case JsWeakRef:
                    // Reaching the target is what WeakRef.prototype.deref exists to gate.
                    return new ValueDescription(ValueKind.WeakRef, "WeakRef", className);

                case JsPromise promise:
                    return DescribePromise(promise, className, depth);

                case JsArray array:
                    return DescribeArray(array, className, depth);

                case JsTypedArray typedArray:
                    return DescribeTypedArray(typedArray, className, depth);

                case JsMap map:
                    return DescribeMap(map, className, depth);

                case JsSet set:
                    return DescribeSet(set, className, depth);

                default:
                    return DescribeOrdinary(obj, className, depth);
            }
        }

        private static ValueDescription DescribeArrayBuffer(JsArrayBuffer buffer, string? className)
        {
            var size = buffer.IsDetachedBuffer ? "detached" : Text(buffer.ArrayBufferByteLength);
            return new ValueDescription(
                ValueKind.ArrayBuffer,
                ValueSlotReader.ArrayBufferTypeName(buffer) + "(" + size + ")",
                className);
        }

        private ValueDescription DescribeOrdinary(ObjectInstance obj, string? className, int depth)
        {
            var kind = KindOf(obj);
            string description;

            if (kind == ValueKind.Error)
            {
                ValueSlotReader.ErrorText(obj, out var name, out var message);
                description = message.Length == 0 ? name : name + ": " + Truncate(message);
            }
            else if (kind == ValueKind.Arguments)
            {
                description = "Arguments";
            }
            else if (kind == ValueKind.Generator)
            {
                description = className ?? "Generator";
            }
            else if (obj is IJsPrimitive boxed)
            {
                description = "[" + ValueSlotReader.BoxedPrimitiveTypeName(boxed) + ": "
                    + Describe(boxed.PrimitiveValue, depth + 1).Description + "]";
            }
            else
            {
                description = className ?? "Object";
            }

            var entries = Properties(obj, depth, out var overflow);
            return new ValueDescription(kind, description, className, entries, overflow);
        }

        private ValueDescription DescribePromise(JsPromise promise, string? className, int depth)
        {
            var state = promise.State switch
            {
                PromiseState.Fulfilled => ValuePromiseState.Fulfilled,
                PromiseState.Rejected => ValuePromiseState.Rejected,
                _ => ValuePromiseState.Pending,
            };

            if (state == ValuePromiseState.Pending)
            {
                return new ValueDescription(ValueKind.Promise, "Promise { <pending> }", className, promiseState: state);
            }

            var result = Describe(promise.Value, depth + 1);
            var label = state == ValuePromiseState.Fulfilled ? "<fulfilled>" : "<rejected>";
            return new ValueDescription(
                ValueKind.Promise,
                "Promise { " + label + ": " + result.Description + " }",
                className,
                promiseState: state,
                promiseResult: result);
        }

        private ValueDescription DescribeArray(JsArray array, string? className, int depth)
        {
            var length = array.Length;
            var description = "Array(" + Text(length) + ")";
            if (depth >= _maxDepth)
            {
                return new ValueDescription(ValueKind.Array, description, className, overflow: length > 0);
            }

            var rendered = System.Math.Min(length, (uint) _maxEntries);
            var entries = new List<ValueEntry>((int) rendered);
            for (var i = 0u; i < rendered; i++)
            {
                entries.Add(EntryFor(Text(i), array.GetOwnProperty(JsString.Create((int) i)), depth));
            }

            return new ValueDescription(ValueKind.Array, description, className, entries, length > rendered);
        }

        private ValueDescription DescribeTypedArray(JsTypedArray array, string? className, int depth)
        {
            // Length answers 0 for a detached or out-of-bounds view, so nothing below reads a buffer that is
            // no longer there.
            var length = array.Length;
            var description = array._arrayElementType.GetTypedArrayName() + "(" + Text(length) + ")";
            if (depth >= _maxDepth)
            {
                return new ValueDescription(ValueKind.TypedArray, description, className, overflow: length > 0);
            }

            var rendered = System.Math.Min(length, (uint) _maxEntries);
            var entries = new List<ValueEntry>((int) rendered);
            for (var i = 0u; i < rendered; i++)
            {
                entries.Add(new ValueEntry(Text(i), Describe(array[i], depth + 1)));
            }

            return new ValueDescription(ValueKind.TypedArray, description, className, entries, length > rendered);
        }

        private ValueDescription DescribeMap(JsMap map, string? className, int depth)
        {
            var size = map.Size;
            var description = "Map(" + Text(size) + ")";
            if (depth >= _maxDepth)
            {
                return new ValueDescription(ValueKind.Map, description, className, overflow: size > 0);
            }

            var entries = new List<ValueEntry>();
            foreach (var entry in map)
            {
                if (entries.Count >= _maxEntries)
                {
                    break;
                }

                entries.Add(new ValueEntry(
                    Text(entries.Count),
                    Describe(entry.Value, depth + 1),
                    entryKey: Describe(entry.Key, depth + 1)));
            }

            return new ValueDescription(ValueKind.Map, description, className, entries, size > entries.Count);
        }

        private ValueDescription DescribeSet(JsSet set, string? className, int depth)
        {
            var size = set.Size;
            var description = "Set(" + Text(size) + ")";
            if (depth >= _maxDepth)
            {
                return new ValueDescription(ValueKind.Set, description, className, overflow: size > 0);
            }

            var entries = new List<ValueEntry>();
            foreach (var value in set)
            {
                if (entries.Count >= _maxEntries)
                {
                    break;
                }

                entries.Add(new ValueEntry(Text(entries.Count), Describe(value, depth + 1)));
            }

            return new ValueDescription(ValueKind.Set, description, className, entries, size > entries.Count);
        }

        /// <summary>
        /// The own enumerable properties of an ordinary object: string keys first, then symbol keys, each
        /// read as a descriptor so an accessor is reported rather than invoked.
        /// </summary>
        private List<ValueEntry>? Properties(ObjectInstance obj, int depth, out bool overflow)
        {
            overflow = false;
            if (depth >= _maxDepth)
            {
                return null;
            }

            List<ValueEntry>? entries = null;
            AppendKeys(obj, Types.String, depth, ref entries, ref overflow);
            AppendKeys(obj, Types.Symbol, depth, ref entries, ref overflow);
            return entries;
        }

        private void AppendKeys(ObjectInstance obj, Types type, int depth, ref List<ValueEntry>? entries, ref bool overflow)
        {
            var keys = obj.GetOwnPropertyKeys(type);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var descriptor = obj.GetOwnProperty(key);
                if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined) || !descriptor.Enumerable)
                {
                    continue;
                }

                if ((entries?.Count ?? 0) >= _maxEntries)
                {
                    overflow = true;
                    continue;
                }

                entries ??= new List<ValueEntry>();
                entries.Add(EntryFor(key.ToString(), descriptor, depth));
            }
        }

        private ValueEntry EntryFor(string key, PropertyDescriptor descriptor, int depth)
        {
            if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
            {
                return new ValueEntry(key, UndefinedDescription);
            }

            if (descriptor.IsAccessorDescriptor())
            {
                var label = ValueSlotReader.AccessorLabel(descriptor);
                return new ValueEntry(key, new ValueDescription(ValueKind.Accessor, label), isAccessor: true);
            }

            return new ValueEntry(key, Describe(descriptor.Value, depth + 1));
        }

        private static ValueDescription HostObjectDescription(Type? type)
        {
            var name = type?.Name;
            return new ValueDescription(ValueKind.HostObject, type?.FullName ?? name ?? "HostObject", name);
        }

        private static string FunctionText(Function function)
        {
            var name = function.GetOwnFunctionNameForDisplay();

            if (ValueSlotReader.IsClassConstructor(function))
            {
                return string.IsNullOrEmpty(name) ? "class" : "class " + name;
            }

            var prefix = ValueSlotReader.FunctionKindName(function) switch
            {
                "AsyncGeneratorFunction" => "async ƒ* ",
                "AsyncFunction" => "async ƒ ",
                "GeneratorFunction" => "ƒ* ",
                _ => "ƒ ",
            };

            return prefix + name + "()";
        }

        private static ValueKind KindOf(ObjectInstance obj) => obj switch
        {
            JsArray => ValueKind.Array,
            JsTypedArray => ValueKind.TypedArray,
            JsMap => ValueKind.Map,
            JsSet => ValueKind.Set,
            JsPromise => ValueKind.Promise,
            JsDataView => ValueKind.DataView,
            JsArrayBuffer => ValueKind.ArrayBuffer,
            JsArguments => ValueKind.Arguments,
            JsDate => ValueKind.Date,
            JsRegExp => ValueKind.RegExp,
            JsWeakMap => ValueKind.WeakMap,
            JsWeakSet => ValueKind.WeakSet,
            JsWeakRef => ValueKind.WeakRef,
            ErrorInstance => ValueKind.Error,
            ISuspendable => ValueKind.Generator,
            Native.Iterator.IteratorInstance or Native.Iterator.IteratorHelper => ValueKind.Iterator,
            _ => ValueKind.Object,
        };

        private string Truncate(string text)
        {
            if (text.Length <= _maxStringLength)
            {
                return text;
            }

            return new StringBuilder(_maxStringLength + 1).Append(text, 0, _maxStringLength).Append('…').ToString();
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(uint value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

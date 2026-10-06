#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.IndexedDb;

/// <summary>A live cursor over transaction data: https://w3c.github.io/IndexedDB/#cursor-interface.</summary>
internal sealed class JsIdbCursor : ObjectInstance
{
    private readonly Realm _realm;
    private readonly JsIdbIndex? _index;
    private readonly JsIdbKeyRange? _range;
    private bool _gotValue;
    private JsValue? _key;
    private JsValue? _primaryKey;
    private JsValue? _value;

    internal JsIdbCursor(Engine engine, Realm realm, JsIdbObjectStore store, JsIdbIndex? index,
        JsIdbRequest request, JsIdbKeyRange? range, string direction, bool keysOnly) : base(engine)
    {
        _realm = realm;
        Store = store;
        _index = index;
        Request = request;
        _range = range;
        Direction = direction;
        KeysOnly = keysOnly;
        _prototype = realm.Intrinsics.IndexedDb.Prototype(keysOnly ? "IDBCursor" : "IDBCursorWithValue");
    }

    internal JsIdbObjectStore Store { get; }
    internal JsIdbRequest Request { get; }
    internal JsValue Source => _index is null ? Store : _index;
    internal string Direction { get; }
    internal bool KeysOnly { get; }
    internal IndexEntry? Position { get; private set; }
    internal JsValue Key => _key ??= Position is { } p ? IndexedDbKeyConverter.ToValue(_engine, _realm, p.Key) : Undefined;
    internal JsValue PrimaryKey => _primaryKey ??= Position is { } p ? IndexedDbKeyConverter.ToValue(_engine, _realm, p.PrimaryKey) : Undefined;
    internal JsValue Value => _value ?? Undefined;
    private bool Reverse => Direction.StartsWith("prev", StringComparison.Ordinal);
    private bool Unique => Direction.EndsWith("unique", StringComparison.Ordinal);

    internal void RequireValue(bool write = false)
    {
        Store.Transaction.RequireActive(write);
        Store.RequireData(active: false);
        _index?.RequireData(active: false);
        if (!_gotValue) IndexedDbErrors.Throw(_realm, "InvalidStateError", "The cursor is not positioned on a record.");
    }

    internal JsValue Move(uint count, IndexedDbKey? target, IndexedDbKey? primary)
    {
        var previous = Position;
        IndexedDbKey? lastKey = null;
        foreach (var entry in Store.Entries(_index, Reverse))
        {
            _engine.Constraints.Check();
            if (_range is not null && !_range.Contains(entry.Key)) continue;
            if (previous is { } old)
            {
                var order = Unique ? entry.Key.CompareTo(old.Key) : entry.CompareTo(old);
                if (Reverse ? order >= 0 : order <= 0) continue;
            }
            if (target is not null)
            {
                var order = entry.Key.CompareTo(target);
                if (order == 0 && primary is not null) order = entry.PrimaryKey.CompareTo(primary);
                if (Reverse ? order > 0 : order < 0) continue;
            }
            if (Unique && lastKey is not null && entry.Key.CompareTo(lastKey) == 0) continue;
            lastKey = entry.Key;
            if (--count != 0) continue;
            var selected = entry;
            // prevunique selects the smallest primary key for the chosen index key.
            if (Direction is "prevunique" && _index is not null)
            {
                foreach (var forward in Store.Entries(_index, reverse: false))
                {
                    _engine.Constraints.Check();
                    if (forward.Key.CompareTo(entry.Key) == 0) { selected = forward; break; }
                }
            }
            Position = selected;
            _key = null;
            _primaryKey = null;
            _value = KeysOnly ? null : Store.ReadValue(selected.PrimaryKey);
            _gotValue = true;
            return this;
        }
        Position = null;
        _key = Undefined;
        _primaryKey = Undefined;
        _value = Undefined;
        _gotValue = false;
        return Null;
    }

    internal void Continue(uint count, IndexedDbKey? key, IndexedDbKey? primary, bool primaryOperation = false)
    {
        RequireValue();
        if (primaryOperation && (_index is null || Unique))
        {
            IndexedDbErrors.Throw(_realm, "InvalidAccessError", "continuePrimaryKey requires a nonunique index cursor.");
        }
        if (key is not null && Position is { } position)
        {
            var comparison = key.CompareTo(position.Key);
            if (comparison == 0 && primary is not null) comparison = primary.CompareTo(position.PrimaryKey);
            if (Reverse ? comparison >= 0 : comparison <= 0) IndexedDbErrors.Throw(_realm, "DataError", "The target key must be beyond the cursor.");
        }
        _gotValue = false;
        Store.Transaction.Queue(Source, () => Move(count, key, primary), Request);
    }

    internal JsIdbRequest Update(JsValue value)
    {
        RequireValue(write: true);
        if (KeysOnly) IndexedDbErrors.Throw(_realm, "InvalidStateError", "A key-only cursor cannot update values.");
        return Store.Put(value, Undefined, overwrite: true, this);
    }

    internal JsIdbRequest Delete()
    {
        RequireValue(write: true);
        if (KeysOnly) IndexedDbErrors.Throw(_realm, "InvalidStateError", "A key-only cursor cannot delete records.");
        var key = Position!.Value.PrimaryKey;
        return Store.Delete(new JsIdbKeyRange(_engine, _realm, key, key, false, false), this);
    }
}
#endif

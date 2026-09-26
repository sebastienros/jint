using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.Browser.Events;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Files;

/// <summary>Per-engine interface objects and per-input selected-file state.</summary>
internal sealed class FileTransferRealm
{
    private static readonly ConditionalWeakTable<Engine, FileTransferRealm> _realms = new();

    private readonly Engine _engine;
    private readonly ConditionalWeakTable<Element, InputFileState> _inputFiles = new();
    private ObjectInstance? _fileListPrototype;
    private HostInterfaceObject? _fileListInterface;
    private ObjectInstance? _dataTransferPrototype;
    private HostInterfaceObject? _dataTransferInterface;
    private ObjectInstance? _dataTransferItemListPrototype;
    private HostInterfaceObject? _dataTransferItemListInterface;
    private ObjectInstance? _dataTransferItemPrototype;
    private HostInterfaceObject? _dataTransferItemInterface;

    private FileTransferRealm(Engine engine)
    {
        _engine = engine;
        engine.Disposed += (_, _) => Release();
    }

    internal static FileTransferRealm? IfCreated(Engine engine)
        => _realms.TryGetValue(engine, out var realm) ? realm : null;

    internal static FileTransferRealm Of(Engine engine)
        => _realms.GetValue(engine, static e => new FileTransferRealm(e));

    internal HostInterfaceObject FileListInterface
    {
        get
        {
            if (_fileListInterface is null)
            {
                _fileListPrototype = FileTransferInstaller.Instantiate(
                    _engine,
                    FileTransferInstaller.FileListShape,
                    "FileList",
                    length: 0,
                    construct: null,
                    out var interfaceObject);
                _fileListInterface = interfaceObject;
            }

            return _fileListInterface;
        }
    }

    internal HostInterfaceObject DataTransferInterface
    {
        get
        {
            if (_dataTransferInterface is null)
            {
                _dataTransferPrototype = FileTransferInstaller.Instantiate(
                    _engine,
                    FileTransferInstaller.DataTransferShape,
                    "DataTransfer",
                    length: 0,
                    _ => new JsDataTransfer(this, _dataTransferPrototype!),
                    out var interfaceObject);
                _dataTransferInterface = interfaceObject;
            }

            return _dataTransferInterface;
        }
    }

    internal HostInterfaceObject DataTransferItemListInterface
    {
        get
        {
            if (_dataTransferItemListInterface is null)
            {
                _dataTransferItemListPrototype = FileTransferInstaller.Instantiate(
                    _engine,
                    FileTransferInstaller.DataTransferItemListShape,
                    "DataTransferItemList",
                    length: 0,
                    construct: null,
                    out var interfaceObject);
                _dataTransferItemListInterface = interfaceObject;
            }

            return _dataTransferItemListInterface;
        }
    }

    internal HostInterfaceObject DataTransferItemInterface
    {
        get
        {
            if (_dataTransferItemInterface is null)
            {
                _dataTransferItemPrototype = FileTransferInstaller.Instantiate(
                    _engine,
                    FileTransferInstaller.DataTransferItemShape,
                    "DataTransferItem",
                    length: 0,
                    construct: null,
                    out var interfaceObject);
                _dataTransferItemInterface = interfaceObject;
            }

            return _dataTransferItemInterface;
        }
    }

    internal JsFileList NewFileList()
    {
        _ = FileListInterface;
        return new JsFileList(_engine, _fileListPrototype!);
    }

    internal JsDataTransferItemList NewItemList(JsDataTransfer owner, JsFileList files)
    {
        _ = DataTransferItemListInterface;
        return new JsDataTransferItemList(this, _dataTransferItemListPrototype!, owner, files);
    }

    internal JsDataTransferItem NewFileItem(Jint.WebApi.Files.JsFile file)
    {
        _ = DataTransferItemInterface;
        return new JsDataTransferItem(_engine, _dataTransferItemPrototype!, file);
    }

    internal JsDataTransferItem NewStringItem(string data, string type)
    {
        _ = DataTransferItemInterface;
        return new JsDataTransferItem(_engine, _dataTransferItemPrototype!, data, type);
    }

    private readonly Queue<InputFileState> _pendingChanges = new();
    private readonly HashSet<InputFileState> _queuedChanges = new();
    private InputFileState? _activeState;
    private bool _flushing;
    private readonly List<WeakReference<InputFileState>> _fileStates = [];
    private int _attachmentsUntilSweep = 64;

    internal JsFileList? InputFiles(Element input, bool create)
    {
        FlushChanges();
        if (!IsFileInput(input)) return null;
        if (_inputFiles.TryGetValue(input, out var state)) return state.Files;
        if (!create) return null;
        PruneFileStates();
        return Attach(input, NewFileList());
    }

    internal void PrepareControlFactsRead(Action<int>? checkpoint, CancellationToken token)
    {
        if (_flushing) throw new InvalidOperationException(SelectorMatchWork.AlreadyActive);
        if (_activeState is null && _pendingChanges.Count == 0) return;
        FlushChanges(new DomReadWork(checkpoint, token));
    }

    private void RequirePreparedRead()
    {
        if (_flushing) throw new InvalidOperationException(SelectorMatchWork.AlreadyActive);
        if (_activeState is not null || _pendingChanges.Count != 0)
            throw new InvalidOperationException(SelectorMatchWork.Invalidated);
    }

    // Preparation happened before the selector captured its stamps. This path is a pure read:
    // discovering fresh pending history invalidates the invocation instead of refreshing its view.
    internal JsFileList? InputFiles(Element input, DomReadWork work)
    {
        RequirePreparedRead();
        work.Check();
        RequirePreparedRead();
        if (input is not { NamespaceUri: Namespaces.Html, LocalName: "input" }
            || HtmlInputTypes.Parse(work.Attribute(input, "type")) != HtmlInputType.File)
        {
            work.Check();
            RequirePreparedRead();
            return null;
        }
        var result = _inputFiles.TryGetValue(input, out var state) ? state.Files : null;
        work.Check();
        RequirePreparedRead();
        return result;
    }

    internal void SetInputFiles(Element input, JsFileList files)
    {
        FlushChanges();
        PruneFileStates();
        if (!IsFileInput(input)) return;
        var changed = !_inputFiles.TryGetValue(input, out var previous) || !ReferenceEquals(previous.Files, files);
        Detach(input);
        _ = Attach(input, files, external: true);
        input.OwnerDocument!.MarkMutation();
        if (changed) BrowserSelectorSemanticRevision.Advance(input.OwnerDocument);
    }

    internal JsValue InputValue(Element input)
    {
        _engine.Constraints.Check();
        if (IsFileInput(input))
        {
            var files = InputFiles(input, create: true)!;
            return JsString.Create(files.Length == 0 ? "" : @"C:\fakepath\" + files.Files[0].Name);
        }
        return JsString.Create(input.GetHtmlState()!.InputValue!.GetValue(DomRealm.Of(_engine).CancellationToken));
    }

    internal JsValue SetInputValue(Element input, string value)
    {
        FlushChanges();
        var realm = DomRealm.Of(_engine);
        _engine.Constraints.Check();
        if (!IsFileInput(input))
        {
            var state = input.GetHtmlState()!.InputValue!;
            var selection = state.Selection;
            state.SetValue(value, realm.NativeReadCheckpoint, realm.CancellationToken);
            if (selection != state.Selection) SelectionChange.Schedule(realm, input);
            return JsValue.Undefined;
        }
        if (value.Length != 0)
        {
            return DomFailures.Refuse(_engine, "HTMLInputElement.value", DomExceptionNames.InvalidState,
                "This input element accepts a filename, which may only be programmatically set to the empty string.");
        }
        ClearInput(input, preserveList: true);
        return JsValue.Undefined;
    }

    internal JsValue SetInputType(Element input, string type)
    {
        input.SetAttributeNS(null, "type", type);
        FlushChanges();
        return JsValue.Undefined;
    }

    internal void ResetForm(Element form)
    {
        var realm = DomRealm.Of(_engine);
        foreach (var element in HtmlFormOwner.ControlsOf(form, realm.NativeReadCheckpoint, CustomElements.CustomElementRegistry.Of(_engine), realm.CancellationToken))
        {
            if (IsFileInput(element)) ClearInput(element, preserveList: true);
        }
    }

    private JsFileList Attach(Element input, JsFileList files, bool external = false)
    {
        var subscription = input.OwnerDocument!.ObserveMutations(input,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true, AttributeFilter = ["type"] });
        var weakInput = new WeakReference<Element>(input);
        var invalidation = new SelectedFileInvalidation(weakInput, files, subscription);
        Action changed = invalidation.Changed;
        var state = new InputFileState(weakInput, files, subscription, external, changed);
        files.Changed += changed;
        _fileStates.Add(new WeakReference<InputFileState>(state));
        subscription.PendingRecord = _ =>
        {
            // Trusted scheduling: no script runs inside native attribute mutation.
            if (_queuedChanges.Add(state)) _pendingChanges.Enqueue(state);
        };
        _inputFiles.Add(input, state);
        return files;
    }

    internal void FlushChanges()
        => FlushChanges(null);

    private void FlushChanges(DomReadWork? work)
    {
        if (_flushing) throw new InvalidOperationException(SelectorMatchWork.AlreadyActive);
        _flushing = true;
        try
        {
            while (true)
            {
                // Dequeue transfers ownership into a durable envelope before any throwing check.
                if (_activeState is null && !_pendingChanges.TryDequeue(out _activeState))
                {
                    work?.Check();
                    // Keep the reentrancy guard armed through the final callback. A callback may
                    // enqueue more work; it must be reconciled before callers capture their seeds.
                    if (_pendingChanges.Count == 0) break;
                    continue;
                }
                var state = _activeState!;
                work?.Step();
                if (!_queuedChanges.Contains(state))
                {
                    CompleteActiveState(state);
                    continue;
                }
                work?.Check();
                if (!_queuedChanges.Contains(state)) continue;
                // No callback separates the destructive drain from durable ownership of its result.
                var records = state.PendingRecords ??= state.Subscription.TakeRecordsForDelivery();
                work?.Check();
                if (!state.Input.TryGetTarget(out var input) || records.Count == 0)
                {
                    CompleteActiveState(state);
                    continue;
                }
                while (state.PendingRecordIndex < records.Count)
                {
                    var record = records[state.PendingRecordIndex];
                    if (work is null) _engine.Constraints.Check();
                    else { work.Step(); work.Check(); }
                    if (_inputFiles.TryGetValue(input, out var attached) && ReferenceEquals(attached, state)
                        && (HtmlInputTypes.Parse(record.OldValue) == HtmlInputType.File)
                        != (HtmlInputTypes.Parse(record.AttributeNewValue) == HtmlInputType.File))
                    {
                        ClearInput(input, preserveList: false);
                    }
                    // Commit the record before post-commit checks. Retrying never repeats a clear
                    // against a replacement assignment made after this state was detached.
                    state.PendingRecordIndex++;
                    work?.Check();
                }
                state.PendingRecords = null;
                state.PendingRecordIndex = 0;
                // The active envelope remains until the next drain is empty, covering records
                // appended by checkpoints while this immutable batch was being examined.
            }
        }
        finally { _flushing = false; }
    }

    private void CompleteActiveState(InputFileState state)
    {
        _queuedChanges.Remove(state);
        state.PendingRecords = null;
        state.PendingRecordIndex = 0;
        _activeState = null;
    }

    private void Detach(Element input)
    {
        if (_inputFiles.TryGetValue(input, out var current))
        {
            _queuedChanges.Remove(current);
            current.Detached = true;
            current.Subscription.Dispose();
            current.Files.Changed -= current.Changed;
            _inputFiles.Remove(input);
        }
    }

    private void ClearInput(Element input, bool preserveList)
    {
        if (!_inputFiles.TryGetValue(input, out var state)) return;
        var hadFiles = state.Files.Length != 0;
        if (state.External)
        {
            Detach(input);
            if (preserveList && IsFileInput(input)) _ = Attach(input, NewFileList());
            BrowserSelectorSemanticRevision.Advance(input.OwnerDocument!);
        }
        else
        {
            state.Files.Clear();
            if (!preserveList) Detach(input);
        }
        if (hadFiles && state.External) input.OwnerDocument!.MarkMutation();
    }

    private bool IsFileInput(Element input)
        => input is { NamespaceUri: Namespaces.Html, LocalName: "input" } && HtmlInputTypes.Parse(ReadInputType(input)) == HtmlInputType.File;

    private string? ReadInputType(Element input)
    {
        var realm = DomRealm.Of(_engine);
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        return work.Attribute(input, "type");
    }

    private void Release()
    {
        foreach (var weak in _fileStates)
        {
            if (weak.TryGetTarget(out var state))
            {
                state.Files.Changed -= state.Changed;
                state.Subscription.Dispose();
            }
        }
        _fileStates.Clear();
        _pendingChanges.Clear();
        _queuedChanges.Clear();
        _activeState = null;
        _inputFiles.Clear();
    }

    private void PruneFileStates()
    {
        if (--_attachmentsUntilSweep > 0) return;
        CompactFileStates(_engine.Constraints.Check);
    }

    internal void CompactFileStates(Action checkpoint)
    {
        var survivors = 0;
        var consumed = 0;
        try
        {
            for (; consumed < _fileStates.Count; consumed++)
            {
                if ((consumed & 255) == 0) checkpoint();
                var weak = _fileStates[consumed];
                if (!weak.TryGetTarget(out var state) || state.Detached) continue;
                if (state.Input.TryGetTarget(out _)) { _fileStates[survivors++] = weak; continue; }
                state.Files.Changed -= state.Changed;
                state.Subscription.Dispose();
                _queuedChanges.Remove(state);
            }
        }
        finally
        {
            // Keep the compacted prefix and every unvisited entry if a budget check
            // interrupts the sweep. Discard only the gap already processed.
            _fileStates.RemoveRange(survivors, consumed - survivors);
            _attachmentsUntilSweep = Math.Max(64, _fileStates.Count);
        }
    }

    private sealed class SelectedFileInvalidation(WeakReference<Element> input, JsFileList files, MutationSubscription subscription)
    {
        internal void Changed()
        {
            if (input.TryGetTarget(out var selectedInput))
            {
                selectedInput.OwnerDocument!.MarkMutation();
                BrowserSelectorSemanticRevision.Advance(selectedInput.OwnerDocument);
            }
            else
            {
                // A shared DataTransfer list can outlive every input it was assigned to.
                files.Changed -= Changed;
                subscription.Dispose();
            }
        }
    }

    private sealed record InputFileState(WeakReference<Element> Input, JsFileList Files,
        MutationSubscription Subscription, bool External, Action Changed)
    {
        internal bool Detached { get; set; }
        internal IReadOnlyList<MutationRecord>? PendingRecords;
        internal int PendingRecordIndex;
    }
}

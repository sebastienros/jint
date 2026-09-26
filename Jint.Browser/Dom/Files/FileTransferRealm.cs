using System.Runtime.CompilerServices;
using Jint.HtmlParser;
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
    private readonly List<WeakReference<InputFileState>> _fileStates = [];
    private int _fileAttachments;

    internal JsFileList? InputFiles(Element input, bool create)
    {
        FlushChanges();
        if (!IsFileInput(input)) return null;
        if (_inputFiles.TryGetValue(input, out var state)) return state.Files;
        if (!create) return null;
        PruneFileStates();
        return Attach(input, NewFileList());
    }

    internal void SetInputFiles(Element input, JsFileList files)
    {
        FlushChanges();
        PruneFileStates();
        if (!IsFileInput(input)) return;
        Detach(input);
        _ = Attach(input, files, external: true);
        input.OwnerDocument!.MarkMutation();
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
        foreach (var element in HtmlFormOwner.ControlsOf(form, realm.NativeReadCheckpoint, realm.CancellationToken, CustomElements.CustomElementRegistry.Of(_engine)))
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
    {
        while (_pendingChanges.TryDequeue(out var state))
        {
            if (!_queuedChanges.Remove(state)) continue;
            var records = state.Subscription.TakeRecordsForDelivery();
            if (!state.Input.TryGetTarget(out var input)) continue;
            for (var i = 0; i < records.Count; i++)
            {
                _engine.Constraints.Check();
                var nextType = i + 1 < records.Count ? records[i + 1].OldValue : ReadInputType(input);
                if ((HtmlInputTypes.Parse(records[i].OldValue) == HtmlInputType.File)
                    != (HtmlInputTypes.Parse(nextType) == HtmlInputType.File))
                {
                    ClearInput(input, preserveList: false);
                    break;
                }
            }
        }
    }

    private void Detach(Element input)
    {
        if (_inputFiles.TryGetValue(input, out var current))
        {
            _queuedChanges.Remove(current);
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
        _inputFiles.Clear();
    }

    private void PruneFileStates()
    {
        if ((++_fileAttachments & 63) != 0) return;
        for (var i = _fileStates.Count - 1; i >= 0; i--)
        {
            if ((i & 255) == 0) _engine.Constraints.Check();
            if (!_fileStates[i].TryGetTarget(out var state)) { _fileStates.RemoveAt(i); continue; }
            if (state.Input.TryGetTarget(out _)) continue;
            state.Files.Changed -= state.Changed;
            state.Subscription.Dispose();
            _queuedChanges.Remove(state);
            _fileStates.RemoveAt(i);
        }
    }

    private sealed class SelectedFileInvalidation(WeakReference<Element> input, JsFileList files, MutationSubscription subscription)
    {
        internal void Changed()
        {
            if (input.TryGetTarget(out var selectedInput)) selectedInput.OwnerDocument!.MarkMutation();
            else
            {
                // A shared DataTransfer list can outlive every input it was assigned to.
                files.Changed -= Changed;
                subscription.Dispose();
            }
        }
    }

    private sealed record InputFileState(WeakReference<Element> Input, JsFileList Files,
        MutationSubscription Subscription, bool External, Action Changed);
}

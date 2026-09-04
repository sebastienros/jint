using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Files;

/// <summary>Per-engine interface objects and per-input selected-file state.</summary>
internal sealed class FileTransferRealm
{
    private static readonly ConditionalWeakTable<Engine, FileTransferRealm> _realms = new();

    private readonly Engine _engine;
    private readonly ConditionalWeakTable<IHtmlInputElement, InputFileState> _inputFiles = new();
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
    }

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

    internal JsFileList? InputFiles(IHtmlInputElement input, bool create)
    {
        if (!IsFileInput(input))
        {
            ClearInput(input, preserveList: false);
            return null;
        }

        if (_inputFiles.TryGetValue(input, out var state))
        {
            return state.Files;
        }

        if (!create)
        {
            return null;
        }

        return Attach(input, NewFileList());
    }

    internal void SetInputFiles(IHtmlInputElement input, JsFileList files)
    {
        if (!IsFileInput(input))
        {
            return;
        }

        Detach(input);
        _ = Attach(input, files, external: true);
    }

    internal JsValue InputValue(IHtmlInputElement input)
    {
        if (IsFileInput(input))
        {
            var files = InputFiles(input, create: true)!;
            return JsString.Create(files.Length == 0 ? string.Empty : @"C:\fakepath\" + files.Files[0].Name);
        }

        return JsString.Create(input.Value);
    }

    internal JsValue SetInputValue(IHtmlInputElement input, string value)
    {
        if (!IsFileInput(input))
        {
            input.Value = value;
            return JsValue.Undefined;
        }

        if (value.Length != 0)
        {
            return DomFailures.Refuse(
                _engine,
                "HTMLInputElement.value",
                DomExceptionNames.InvalidState,
                "This input element accepts a filename, which may only be programmatically set to the empty string.");
        }

        ClearInput(input, preserveList: true);
        input.Value = string.Empty;
        return JsValue.Undefined;
    }

    internal JsValue SetInputType(IHtmlInputElement input, string type)
    {
        var wasFile = IsFileInput(input);
        input.Type = type;
        if (wasFile != IsFileInput(input))
        {
            ClearInput(input, preserveList: false);
        }

        return JsValue.Undefined;
    }

    internal void ResetForm(IHtmlFormElement form)
    {
        foreach (var element in form.Elements)
        {
            if (element is IHtmlInputElement input && IsFileInput(input))
            {
                ClearInput(input, preserveList: true);
            }
        }
    }

    internal static void ResetCopiedInputs(INode node)
    {
        if (node is IHtmlInputElement input)
        {
            ResetCopiedInput(input);
        }

        foreach (var descendant in node.Descendants<IHtmlInputElement>())
        {
            ResetCopiedInput(descendant);
        }

        static void ResetCopiedInput(IHtmlInputElement input)
        {
            if (IsFileInput(input))
            {
                input.Files?.Clear();
                input.Value = string.Empty;
            }
        }
    }

    private JsFileList Attach(IHtmlInputElement input, JsFileList files, bool external = false)
    {
        var weakInput = new WeakReference<IHtmlInputElement>(input);
        void Mirror()
        {
            if (weakInput.TryGetTarget(out var target))
            {
                MirrorToAngleSharp(target, files);
            }
            else
            {
                files.Changed -= Mirror;
            }
        }

        var state = new InputFileState(files, Mirror, external);
        _inputFiles.Add(input, state);
        files.Changed += Mirror;
        Mirror();
        return files;
    }

    internal void AttributeChanged(IElement element, string name)
    {
        if (element is IHtmlInputElement input
            && string.Equals(name, "type", StringComparison.OrdinalIgnoreCase)
            && _inputFiles.TryGetValue(input, out _)
            && !IsFileInput(input))
        {
            ClearInput(input, preserveList: false);
        }
    }

    private void Detach(IHtmlInputElement input)
    {
        if (_inputFiles.TryGetValue(input, out var current))
        {
            current.Files.Changed -= current.Mirror;
            _inputFiles.Remove(input);
        }
    }

    private void ClearInput(IHtmlInputElement input, bool preserveList)
    {
        if (_inputFiles.TryGetValue(input, out var state))
        {
            if (state.External)
            {
                Detach(input);
                input.Files?.Clear();
                input.Value = string.Empty;
                if (preserveList && IsFileInput(input))
                {
                    _ = Attach(input, NewFileList());
                }
            }
            else
            {
                state.Files.Clear();
                if (!preserveList)
                {
                    Detach(input);
                }
            }
        }
        else
        {
            input.Files?.Clear();
        }
    }

    private static void MirrorToAngleSharp(IHtmlInputElement input, JsFileList files)
    {
        var target = input.Files;
        if (target is null)
        {
            input.Value = string.Empty;
            return;
        }

        target.Clear();
        foreach (var file in files.Files)
        {
            target.Add(new AngleSharpFileAdapter(file));
        }

        input.Value = files.Length == 0 ? string.Empty : @"C:\fakepath\" + files.Files[0].Name;
    }

    private static bool IsFileInput(IHtmlInputElement input)
        => string.Equals(input.Type, "file", StringComparison.OrdinalIgnoreCase);

    private sealed record InputFileState(
        JsFileList Files,
        Action Mirror,
        bool External);
}

/// <summary>Tracks file-upload state transitions for connected and disconnected inputs.</summary>
internal sealed class FileInputAttributeObserver(Runtime.PageRuntime runtime) : IAttributeObserver
{
    /// <inheritdoc />
    public void NotifyChange(IElement host, string name, string? value)
        => FileTransferRealm.Of(runtime.Engine).AttributeChanged(host, name);
}

namespace Jint.Browser.Tool;

/// <summary>Reads the command line's opt-in HTTP cache configuration.</summary>
internal sealed class HttpCacheSettings
{
    private readonly BrowserHttpCacheOptions _options;

    private HttpCacheSettings(BrowserHttpCacheOptions options) => _options = options;

    internal static void Declare(Dictionary<string, OptionKind> syntax)
    {
        syntax["http-cache"] = OptionKind.Value;
        syntax["http-cache-dir"] = OptionKind.Value;
        syntax["http-cache-partition"] = OptionKind.Value;
        syntax["http-cache-temporary"] = OptionKind.Flag;
        syntax["http-cache-max-bytes"] = OptionKind.Value;
        syntax["http-cache-max-entries"] = OptionKind.Value;
        syntax["http-cache-max-entry-bytes"] = OptionKind.Value;
    }

    internal static HttpCacheSettings Read(CommandLine line)
    {
        var directory = line.Value("http-cache-dir");
        var partition = line.Value("http-cache-partition");
        var temporary = line.Flag("http-cache-temporary");
        var storage = line.Value("http-cache") is { } mode
            ? ValueSyntax.Word("http-cache", mode, ("disabled", BrowserHttpCacheStorage.Disabled),
                ("memory", BrowserHttpCacheStorage.Memory), ("disk", BrowserHttpCacheStorage.Disk))
            : directory is not null || temporary ? BrowserHttpCacheStorage.Disk : BrowserHttpCacheStorage.Disabled;
        if (storage != BrowserHttpCacheStorage.Disk && (directory is not null || partition is not null || temporary))
            throw new ToolUsageException("'--http-cache-dir', '--http-cache-partition' and '--http-cache-temporary' require disk caching");
        if (storage == BrowserHttpCacheStorage.Disk && !temporary
            && (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(partition)))
            throw new ToolUsageException("persistent HTTP caching requires '--http-cache-dir' and '--http-cache-partition'; use '--http-cache-temporary' for disposable storage");
        if (temporary && partition is not null)
            throw new ToolUsageException("'--http-cache-partition' identifies persistent storage and cannot be combined with '--http-cache-temporary'");
        if (storage == BrowserHttpCacheStorage.Disabled && (line.Value("http-cache-max-bytes") is not null
            || line.Value("http-cache-max-entries") is not null || line.Value("http-cache-max-entry-bytes") is not null))
            throw new ToolUsageException("HTTP cache limits require '--http-cache memory' or disk caching");
        if (directory is not null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ToolUsageException("'--http-cache-dir' requires a directory");
            try { directory = Path.GetFullPath(directory); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            { throw new ToolUsageException($"invalid '--http-cache-dir': {ex.Message}"); }
        }
        var options = new BrowserHttpCacheOptions
        {
            Storage = storage,
            Directory = directory,
            PartitionKey = partition,
            Temporary = temporary,
        };
        if (line.Value("http-cache-max-bytes") is { } bytes) options.MaxBytes = ValueSyntax.Size("http-cache-max-bytes", bytes);
        if (line.Value("http-cache-max-entries") is { } entries) options.MaxEntries = ValueSyntax.Integer("http-cache-max-entries", entries, 1);
        if (line.Value("http-cache-max-entry-bytes") is { } entryBytes)
        {
            var size = ValueSyntax.Size("http-cache-max-entry-bytes", entryBytes);
            if (size <= 0 || size > int.MaxValue / 2) throw new ToolUsageException("'--http-cache-max-entry-bytes' must be positive and at most 1073741823 bytes");
            options.MaxEntryBytes = (int) size;
        }
        if (options.MaxBytes <= 0 || options.MaxBytes == long.MaxValue)
            throw new ToolUsageException("'--http-cache-max-bytes' must be finite and positive");
        if (options.MaxEntries == int.MaxValue)
            throw new ToolUsageException("'--http-cache-max-entries' must be less than 2147483647");
        return new HttpCacheSettings(options);
    }

    internal void Apply(BrowserHttpCacheOptions target, bool temporary = false)
    {
        target.Storage = _options.Storage;
        target.Directory = _options.Directory;
        target.PartitionKey = _options.PartitionKey;
        target.Temporary = temporary || _options.Temporary;
        target.MaxBytes = _options.MaxBytes;
        target.MaxEntries = _options.MaxEntries;
        target.MaxEntryBytes = _options.MaxEntryBytes;
        target.TimeProvider = _options.TimeProvider;
    }

    internal void ConfigureBrowser(BrowserOptions options)
    {
        if (_options.Storage == BrowserHttpCacheStorage.Disabled) return;
        var contexts = 0;
        options.ConfigureContext(context => Apply(context.HttpCache, temporary: Interlocked.Increment(ref contexts) != 1));
    }
}

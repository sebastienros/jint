namespace Jint.Browser;

/// <summary>Configures one context's private HTTP response cache, independently of script-visible CacheStorage.</summary>
public sealed class BrowserHttpCacheOptions
{
    /// <summary>Initializes disabled HTTP caching with finite storage limits.</summary>
    public BrowserHttpCacheOptions() { }

    /// <summary>Gets or sets the storage mode. Defaults to disabled.</summary>
    public BrowserHttpCacheStorage Storage { get; set; }

    /// <summary>Gets or sets the disk cache's parent directory.</summary>
    /// <remarks>Persistent disk caching requires both this directory and an explicit <see cref="PartitionKey"/>.</remarks>
    public string? Directory { get; set; }

    /// <summary>Gets or sets the persistent session identity within <see cref="Directory"/>.</summary>
    /// <remarks>Reuse only for the same visitor and trust boundary. Concurrent contexts with the same identity are refused.</remarks>
    public string? PartitionKey { get; set; }

    /// <summary>Whether disk storage is temporary and deleted when the context closes. Defaults to false.</summary>
    public bool Temporary { get; set; }

    /// <summary>Gets or sets the total cache budget, including metadata and pending captures. Defaults to 64 MiB.</summary>
    public long MaxBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Gets or sets the maximum number of stored representations. Defaults to 1024.</summary>
    public int MaxEntries { get; set; } = 1024;

    /// <summary>Gets or sets the maximum stored body size. Defaults to 4 MiB.</summary>
    public int MaxEntryBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Gets or sets the thread-safe clock used for HTTP freshness. Defaults to TimeProvider.System.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}

/// <summary>Chooses storage for a context's automatic HTTP response cache.</summary>
public enum BrowserHttpCacheStorage
{
    /// <summary>Disables HTTP caching.</summary>
    Disabled,
    /// <summary>Keeps representations in context-owned memory.</summary>
    Memory,
    /// <summary>Persists the bounded memory cache in an explicitly selected disk partition.</summary>
    Disk,
}

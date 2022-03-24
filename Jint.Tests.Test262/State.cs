using System;
using System.Collections.Generic;
using Esprima.Ast;

namespace Jint.Tests.Test262;

/// <summary>
/// Custom state for Jint.
/// </summary>
public static partial class State
{
    /// <summary>
    /// Pre-compiled scripts for faster execution.
    /// </summary>
    public static readonly Dictionary<string, Script> Sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Time zone to use by default.
    /// </summary>
    public static TimeZoneInfo TimeZone;
}
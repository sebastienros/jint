// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Reflection;
using Xunit.Sdk;

namespace Jint.Tests;

/// <summary>
/// Replaces the current culture and UI culture for the test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ReplaceCultureAttribute : BeforeAfterTestAttribute
{
    private CultureInfo _originalCulture;
    private CultureInfo _originalUICulture;

    /// <summary>
    /// Replaces the current culture and UI culture based on specified value.
    /// </summary>
    public ReplaceCultureAttribute(string currentCulture) : this(currentCulture, currentCulture)
    {
    }

    /// <summary>
    /// Replaces the current culture and UI culture based on specified values.
    /// </summary>
    public ReplaceCultureAttribute(string currentCulture, string currentUICulture)
    {
        Culture = new CultureInfo(currentCulture);
        UICulture = new CultureInfo(currentUICulture);
    }

#if NETFRAMEWORK
    /// <summary>
    /// The <see cref="Thread.CurrentCulture"/> for the test. Defaults to en-GB.
    /// </summary>
    /// <remarks>
    /// en-GB is used here as the default because en-US is equivalent to the InvariantCulture. We
    /// want to be able to find bugs where we're accidentally relying on the Invariant instead of the
    /// user's culture.
    /// </remarks>
#else
        /// <summary>
        /// The <see cref="CultureInfo.CurrentCulture"/> for the test. Defaults to en-GB.
        /// </summary>
        /// <remarks>
        /// en-GB is used here as the default because en-US is equivalent to the InvariantCulture. We
        /// want to be able to find bugs where we're accidentally relying on the Invariant instead of the
        /// user's culture.
        /// </remarks>
#endif
    public CultureInfo Culture { get; }

#if NETFRAMEWORK
    /// <summary>
    /// The <see cref="Thread.CurrentUICulture"/> for the test. Defaults to en-US.
    /// </summary>
#else
        /// <summary>
        /// The <see cref="CultureInfo.CurrentUICulture"/> for the test. Defaults to en-US.
        /// </summary>
#endif
    public CultureInfo UICulture { get; }

    public override void Before(MethodInfo methodUnderTest)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;

#if NETFRAMEWORK
        System.Threading.Thread.CurrentThread.CurrentCulture = Culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = UICulture;
#else
            CultureInfo.CurrentCulture = Culture;
            CultureInfo.CurrentUICulture = UICulture;
#endif

    }

    public override void After(MethodInfo methodUnderTest)
    {
#if NETFRAMEWORK
        System.Threading.Thread.CurrentThread.CurrentCulture = _originalCulture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = _originalUICulture;
#else
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture;
#endif
    }
}
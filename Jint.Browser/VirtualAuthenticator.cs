using System.Security.Cryptography;

namespace Jint.Browser;

/// <summary>
/// What a <see cref="VirtualAuthenticator"/> can do: the options of the DevTools protocol's
/// <c>WebAuthn.addVirtualAuthenticator</c> that a page's ceremonies depend on.
/// </summary>
public sealed record VirtualAuthenticatorOptions
{
    /// <summary>Whether it stores discoverable (resident) credentials, which a sign-in without a user name finds.</summary>
    public bool HasResidentKey { get; init; } = true;

    /// <summary>Whether it can verify the user — a fingerprint, a face, a PIN.</summary>
    public bool HasUserVerification { get; init; } = true;

    /// <summary>Whether the verification succeeds, when it has one.</summary>
    public bool IsUserVerified { get; init; } = true;

    /// <summary>The transport its credentials report, <c>internal</c> for one built into the device.</summary>
    public string Transport { get; init; } = "internal";
}

/// <summary>A credential a <see cref="VirtualAuthenticator"/> holds: an ES256 key pair registered with a relying party.</summary>
public sealed class VirtualAuthenticatorCredential
{
    internal VirtualAuthenticatorCredential(byte[] credentialId, string rpId, byte[]? userHandle, ECDsa key, bool isResidentCredential)
    {
        CredentialId = credentialId;
        RpId = rpId;
        UserHandle = userHandle;
        Key = key;
        IsResidentCredential = isResidentCredential;
    }

    /// <summary>The credential's id, which the relying party stores.</summary>
    public ReadOnlyMemory<byte> CredentialId { get; }

    /// <summary>The relying party it was registered with: a domain, such as <c>localhost</c>.</summary>
    public string RpId { get; }

    /// <summary>The relying party's id of the user, kept for a discoverable credential; <see langword="null"/> otherwise.</summary>
    public ReadOnlyMemory<byte>? UserHandle { get; }

    /// <summary>Whether it is discoverable: a sign-in that names no credential can use it.</summary>
    public bool IsResidentCredential { get; }

    /// <summary>How many assertions it has signed, which a relying party checks only ever grows.</summary>
    public uint SignCount { get; internal set; }

    internal ECDsa Key { get; }
}

/// <summary>
/// An authenticator that answers a page's WebAuthn ceremonies — <c>navigator.credentials.create()</c> and
/// <c>get()</c> — without a prompt, as the DevTools protocol's virtual authenticator does for Chromium.
/// </summary>
/// <remarks>
/// <para>
/// Add it to a <see cref="BrowserContext"/> with <see cref="BrowserContext.AddVirtualAuthenticator"/>; its pages'
/// ceremonies then use it, and the same instance can be added to another context, so a passkey registered in one
/// signs in in the next — what reading a credential from one virtual authenticator and adding it to another does.
/// </para>
/// <para>
/// The user is present on every ceremony, and verified as <see cref="VirtualAuthenticatorOptions"/> say. Keys are
/// ES256 (P-256), attestation is <c>none</c>, and a conditional request — the passkeys an autofill offers — is
/// answered with the first discoverable credential of the relying party, as when the user picks it.
/// </para>
/// </remarks>
public sealed class VirtualAuthenticator
{
    private readonly object _gate = new();
    private readonly List<VirtualAuthenticatorCredential> _credentials = [];

    /// <summary>Creates an authenticator with the given options, or the defaults: a verifying platform authenticator.</summary>
    /// <param name="options">What it can do.</param>
    public VirtualAuthenticator(VirtualAuthenticatorOptions? options = null)
    {
        Options = options ?? new VirtualAuthenticatorOptions();
    }

    /// <summary>What it can do.</summary>
    public VirtualAuthenticatorOptions Options { get; }

    /// <summary>The credentials it holds, in the order they were registered.</summary>
    public IReadOnlyList<VirtualAuthenticatorCredential> Credentials
    {
        get
        {
            lock (_gate)
            {
                return [.. _credentials];
            }
        }
    }

    /// <summary>Forgets every credential it holds.</summary>
    public void RemoveAllCredentials()
    {
        lock (_gate)
        {
            _credentials.Clear();
        }
    }

    internal bool Holds(string rpId, IReadOnlyList<byte[]> credentialIds)
    {
        lock (_gate)
        {
            return _credentials.Exists(credential => credential.RpId == rpId
                && credentialIds.Any(id => credential.CredentialId.Span.SequenceEqual(id)));
        }
    }

    internal VirtualAuthenticatorCredential Register(string rpId, byte[] userHandle, bool resident)
    {
        var credential = new VirtualAuthenticatorCredential(
            RandomNumberGenerator.GetBytes(16), rpId, resident ? userHandle : null, ECDsa.Create(ECCurve.NamedCurves.nistP256), resident);
        lock (_gate)
        {
            _credentials.Add(credential);
        }

        return credential;
    }

    // The credential an assertion uses, its sign count moved on: one the relying party named, or, when it named
    // none, the first discoverable one.
    internal (VirtualAuthenticatorCredential Credential, uint SignCount)? Assert(string rpId, IReadOnlyList<byte[]> allowed)
    {
        lock (_gate)
        {
            var credential = _credentials.Find(candidate => candidate.RpId == rpId
                && (allowed.Count == 0 ? candidate.IsResidentCredential : allowed.Any(id => candidate.CredentialId.Span.SequenceEqual(id))));
            if (credential is null)
            {
                return null;
            }

            credential.SignCount++;
            return (credential, credential.SignCount);
        }
    }
}

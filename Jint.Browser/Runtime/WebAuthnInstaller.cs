using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// Web Authentication (https://www.w3.org/TR/webauthn-3/) over the context's <see cref="VirtualAuthenticator"/>s:
/// <c>navigator.credentials</c>, <c>PublicKeyCredential</c> and the authenticator responses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interfaces are script; the ceremonies are not.</b> The interface objects, their brands and their
/// <c>ArrayBuffer</c> members are a small script installed in every window, so <c>instanceof</c> and the
/// prototype chain are what a page expects; it reaches the authenticator through one native function it holds in a
/// closure, never through a global. Everything that makes a ceremony verifiable is built here: the client data,
/// the authenticator data, the COSE key, the CBOR attestation object and the ES256 signature.
/// </para>
/// <para>
/// They exist only in a window whose context has a <see cref="VirtualAuthenticator"/> when the window is created: a
/// page elsewhere sees a browser without WebAuthn. A ceremony settles in a later task, as one that waits for a user does. A conditional request with no credential
/// to offer stays pending until its signal aborts it, as an autofill with nothing to show does.
/// </para>
/// </remarks>
internal static class WebAuthnInstaller
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte AttestedCredentialData = 0x40;

    private static readonly Prepared<Acornima.Ast.Script> s_script = Engine.PrepareScript(Source, "webauthn.js");

    internal static void Install(PageRuntime runtime)
    {
        // Only where a host gave the context an authenticator: a page elsewhere sees a browser without WebAuthn, and
        // pays nothing for it, whatever its budgets.
        if (runtime.Page.Context.VirtualAuthenticators.Length == 0)
        {
            return;
        }

        var engine = runtime.Engine;
        if (engine.Evaluate(s_script) is not ICallable install)
        {
            return;
        }

        var bridge = new ClrFunction(engine, "webauthn", (_, arguments) => Bridge(runtime, arguments));
        install.Call(JsValue.Undefined, bridge);
    }

    private static JsValue Bridge(PageRuntime runtime, JsValue[] arguments)
    {
        var authenticators = runtime.Page.Context.VirtualAuthenticators;
        var operation = TypeConverter.ToString(arguments.At(0));
        if (operation == "available")
        {
            return authenticators.Length != 0 ? JsBoolean.True : JsBoolean.False;
        }

        using var request = JsonDocument.Parse(TypeConverter.ToString(arguments.At(1)));
        return JsString.Create(operation == "create"
            ? Create(authenticators, request.RootElement)
            : Get(authenticators, request.RootElement));
    }

    // https://www.w3.org/TR/webauthn-3/#sctn-createCredential and the authenticatorMakeCredential operation.
    private static string Create(VirtualAuthenticator[] authenticators, JsonElement request)
    {
        var rpId = request.GetProperty("rpId").GetString()!;
        var algorithms = request.GetProperty("algorithms").EnumerateArray().Select(algorithm => algorithm.GetInt32()).ToArray();
        if (algorithms.Length != 0 && !algorithms.Contains(-7))
        {
            return Error("NotSupportedError", "The authenticator supports ES256 only.");
        }

        var excluded = Ids(request.GetProperty("exclude"));
        var residentKey = request.GetProperty("residentKey").GetString();
        var verification = request.GetProperty("userVerification").GetString();
        var authenticator = authenticators.FirstOrDefault(candidate =>
            (residentKey != "required" || candidate.Options.HasResidentKey)
            && (verification != "required" || (candidate.Options.HasUserVerification && candidate.Options.IsUserVerified)));
        if (authenticator is null)
        {
            return Error("NotAllowedError", "The operation either timed out or was not allowed.");
        }

        if (authenticators.Any(candidate => candidate.Holds(rpId, excluded)))
        {
            return Error("InvalidStateError", "The user attempted to register an authenticator that contains one of the credentials already registered with the relying party.");
        }

        var resident = authenticator.Options.HasResidentKey && residentKey is "required" or "preferred";
        var credential = authenticator.Register(rpId, Convert.FromBase64String(request.GetProperty("userId").GetString()!), resident);
        var clientData = ClientData("webauthn.create", request);
        var parameters = credential.Key.ExportParameters(includePrivateParameters: false);
        var coseKey = Cbor.Map(
            (Cbor.Integer(1), Cbor.Integer(2)),
            (Cbor.Integer(3), Cbor.Integer(-7)),
            (Cbor.Integer(-1), Cbor.Integer(1)),
            (Cbor.Integer(-2), Cbor.Bytes(parameters.Q.X!)),
            (Cbor.Integer(-3), Cbor.Bytes(parameters.Q.Y!)));

        var id = credential.CredentialId.ToArray();
        var attested = new byte[16 + 2 + id.Length + coseKey.Length];
        BinaryPrimitives.WriteUInt16BigEndian(attested.AsSpan(16), (ushort) id.Length);
        id.CopyTo(attested, 18);
        coseKey.CopyTo(attested, 18 + id.Length);
        var authenticatorData = AuthenticatorData(rpId, (byte) (Flags(authenticator, verification) | AttestedCredentialData), 0, attested);
        var attestation = Cbor.Map(
            (Cbor.Text("fmt"), Cbor.Text("none")),
            (Cbor.Text("attStmt"), Cbor.Map()),
            (Cbor.Text("authData"), Cbor.Bytes(authenticatorData)));

        return Result(writer =>
        {
            writer.WriteString("id", Base64Url(id));
            writer.WriteString("rawId", Convert.ToBase64String(id));
            writer.WriteString("clientDataJSON", Convert.ToBase64String(clientData));
            writer.WriteString("attestationObject", Convert.ToBase64String(attestation));
            writer.WriteString("authenticatorData", Convert.ToBase64String(authenticatorData));
            writer.WriteString("publicKey", Convert.ToBase64String(credential.Key.ExportSubjectPublicKeyInfo()));
            writer.WriteStartArray("transports");
            writer.WriteStringValue(authenticator.Options.Transport);
            writer.WriteEndArray();
        });
    }

    // https://www.w3.org/TR/webauthn-3/#sctn-getAssertion and the authenticatorGetAssertion operation.
    private static string Get(VirtualAuthenticator[] authenticators, JsonElement request)
    {
        var rpId = request.GetProperty("rpId").GetString()!;
        var allowed = Ids(request.GetProperty("allow"));
        var verification = request.GetProperty("userVerification").GetString();
        foreach (var authenticator in authenticators)
        {
            if (verification == "required" && !(authenticator.Options.HasUserVerification && authenticator.Options.IsUserVerified)
                || authenticator.Assert(rpId, allowed) is not { } assertion)
            {
                continue;
            }

            var clientData = ClientData("webauthn.get", request);
            var authenticatorData = AuthenticatorData(rpId, Flags(authenticator, verification), assertion.SignCount, []);
            var signed = new byte[authenticatorData.Length + 32];
            authenticatorData.CopyTo(signed, 0);
            SHA256.HashData(clientData, signed.AsSpan(authenticatorData.Length));
            var signature = assertion.Credential.Key.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            var id = assertion.Credential.CredentialId.ToArray();
            return Result(writer =>
            {
                writer.WriteString("id", Base64Url(id));
                writer.WriteString("rawId", Convert.ToBase64String(id));
                writer.WriteString("clientDataJSON", Convert.ToBase64String(clientData));
                writer.WriteString("authenticatorData", Convert.ToBase64String(authenticatorData));
                writer.WriteString("signature", Convert.ToBase64String(signature));
                if (assertion.Credential.UserHandle is { } userHandle)
                {
                    writer.WriteString("userHandle", Convert.ToBase64String(userHandle.Span));
                }
                else
                {
                    writer.WriteNull("userHandle");
                }
            });
        }

        // A conditional request offers what there is, and with nothing waits; a modal one tells the page.
        return request.GetProperty("conditional").GetBoolean()
            ? Result(writer => writer.WriteBoolean("pending", true))
            : Error("NotAllowedError", "The operation either timed out or was not allowed.");
    }

    private static byte Flags(VirtualAuthenticator authenticator, string? verification)
        => (byte) (UserPresent
            | (verification != "discouraged" && authenticator.Options.HasUserVerification && authenticator.Options.IsUserVerified ? UserVerified : 0));

    // https://www.w3.org/TR/webauthn-3/#sctn-authenticator-data
    private static byte[] AuthenticatorData(string rpId, byte flags, uint signCount, ReadOnlySpan<byte> attested)
    {
        var data = new byte[37 + attested.Length];
        SHA256.HashData(Encoding.UTF8.GetBytes(rpId), data);
        data[32] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(33), signCount);
        attested.CopyTo(data.AsSpan(37));
        return data;
    }

    // https://www.w3.org/TR/webauthn-3/#clientdatajson-serialization: the members in this order, as a browser
    // writes them.
    private static byte[] ClientData(string type, JsonElement request)
    {
        var challenge = Base64Url(Convert.FromBase64String(request.GetProperty("challenge").GetString()!));
        var origin = request.GetProperty("origin").GetString()!;
        return Encoding.UTF8.GetBytes(
            "{\"type\":" + JsonSerializer.Serialize(type) + ",\"challenge\":" + JsonSerializer.Serialize(challenge)
            + ",\"origin\":" + JsonSerializer.Serialize(origin) + ",\"crossOrigin\":false}");
    }

    private static List<byte[]> Ids(JsonElement ids)
        => [.. ids.EnumerateArray().Select(id => Convert.FromBase64String(id.GetString()!))];

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Error(string name, string message)
        => Result(writer =>
        {
            writer.WriteString("error", name);
            writer.WriteString("message", message);
        });

    private static string Result(Action<Utf8JsonWriter> write)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The few CBOR items (RFC 8949) an attestation object and a COSE key are made of.</summary>
    private static class Cbor
    {
        internal static byte[] Integer(long value)
            => value >= 0 ? Head(0, (ulong) value) : Head(1, (ulong) (-1 - value));

        internal static byte[] Bytes(ReadOnlySpan<byte> value) => [.. Head(2, (ulong) value.Length), .. value];

        internal static byte[] Text(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return [.. Head(3, (ulong) bytes.Length), .. bytes];
        }

        internal static byte[] Map(params (byte[] Key, byte[] Value)[] entries)
        {
            var map = new List<byte>(Head(5, (ulong) entries.Length));
            foreach (var (key, value) in entries)
            {
                map.AddRange(key);
                map.AddRange(value);
            }

            return [.. map];
        }

        private static byte[] Head(byte major, ulong value)
        {
            var type = (byte) (major << 5);
            if (value < 24) return [(byte) (type | value)];
            if (value <= byte.MaxValue) return [(byte) (type | 24), (byte) value];
            if (value <= ushort.MaxValue) return [(byte) (type | 25), (byte) (value >> 8), (byte) value];
            if (value <= uint.MaxValue) return [(byte) (type | 26), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value];
            var head = new byte[9];
            head[0] = (byte) (type | 27);
            BinaryPrimitives.WriteUInt64BigEndian(head.AsSpan(1), value);
            return head;
        }
    }

    private const string Source = """
        (function (bridge) {
          'use strict';
          const token = Symbol('webauthn');
          const illegal = (name) => { throw new TypeError("Failed to construct '" + name + "': Illegal constructor"); };
          const bufferOf = (base64) => {
            const text = atob(base64);
            const bytes = new Uint8Array(text.length);
            for (let i = 0; i < text.length; i++) bytes[i] = text.charCodeAt(i);
            return bytes.buffer;
          };
          const base64Of = (source, member) => {
            let bytes;
            if (source instanceof ArrayBuffer) bytes = new Uint8Array(source);
            else if (ArrayBuffer.isView(source)) bytes = new Uint8Array(source.buffer, source.byteOffset, source.byteLength);
            else throw new TypeError("Failed to read the '" + member + "' property: The provided value is not of type '(ArrayBuffer or ArrayBufferView)'.");
            let text = '';
            for (let i = 0; i < bytes.length; i++) text += String.fromCharCode(bytes[i]);
            return btoa(text);
          };
          const base64Url = (buffer) => base64Of(buffer, 'buffer').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
          const fromBase64Url = (text) => bufferOf(text.replace(/-/g, '+').replace(/_/g, '/') + '==='.slice((text.length + 3) % 4));
          const aborted = (signal) => signal.reason !== undefined ? signal.reason : new DOMException('The operation was aborted.', 'AbortError');

          class Credential {
            #id;
            constructor(key, id) { if (key !== token) illegal('Credential'); this.#id = id; }
            get id() { return this.#id; }
            get type() { return 'public-key'; }
          }

          class AuthenticatorResponse {
            #clientDataJSON;
            constructor(key, clientDataJSON) { if (key !== token) illegal('AuthenticatorResponse'); this.#clientDataJSON = clientDataJSON; }
            get clientDataJSON() { return this.#clientDataJSON; }
          }

          class AuthenticatorAttestationResponse extends AuthenticatorResponse {
            #attestationObject; #authenticatorData; #publicKey; #transports;
            constructor(key, data) {
              super(key, data && bufferOf(data.clientDataJSON));
              this.#attestationObject = bufferOf(data.attestationObject);
              this.#authenticatorData = bufferOf(data.authenticatorData);
              this.#publicKey = bufferOf(data.publicKey);
              this.#transports = data.transports;
            }
            get attestationObject() { return this.#attestationObject; }
            getTransports() { return this.#transports.slice(); }
            getAuthenticatorData() { return this.#authenticatorData.slice(0); }
            getPublicKey() { return this.#publicKey.slice(0); }
            getPublicKeyAlgorithm() { return -7; }
          }

          class AuthenticatorAssertionResponse extends AuthenticatorResponse {
            #authenticatorData; #signature; #userHandle;
            constructor(key, data) {
              super(key, data && bufferOf(data.clientDataJSON));
              this.#authenticatorData = bufferOf(data.authenticatorData);
              this.#signature = bufferOf(data.signature);
              this.#userHandle = data.userHandle === null ? null : bufferOf(data.userHandle);
            }
            get authenticatorData() { return this.#authenticatorData; }
            get signature() { return this.#signature; }
            get userHandle() { return this.#userHandle; }
          }

          class PublicKeyCredential extends Credential {
            #rawId; #response;
            constructor(key, data, response) { super(key, data && data.id); this.#rawId = bufferOf(data.rawId); this.#response = response; }
            get rawId() { return this.#rawId; }
            get response() { return this.#response; }
            get authenticatorAttachment() { return 'platform'; }
            getClientExtensionResults() { return {}; }
            toJSON() {
              const response = this.#response;
              const json = { id: this.id, rawId: base64Url(this.#rawId), type: this.type, authenticatorAttachment: this.authenticatorAttachment, clientExtensionResults: {} };
              json.response = response instanceof AuthenticatorAttestationResponse
                ? { clientDataJSON: base64Url(response.clientDataJSON), authenticatorData: base64Url(response.getAuthenticatorData()), transports: response.getTransports(),
                    publicKey: base64Url(response.getPublicKey()), publicKeyAlgorithm: response.getPublicKeyAlgorithm(), attestationObject: base64Url(response.attestationObject) }
                : { clientDataJSON: base64Url(response.clientDataJSON), authenticatorData: base64Url(response.authenticatorData), signature: base64Url(response.signature),
                    userHandle: response.userHandle === null ? null : base64Url(response.userHandle) };
              return json;
            }
            static isConditionalMediationAvailable() { return Promise.resolve(true); }
            static isUserVerifyingPlatformAuthenticatorAvailable() { return Promise.resolve(bridge('available')); }
            static parseCreationOptionsFromJSON(options) {
              return { ...options, challenge: fromBase64Url(options.challenge), user: { ...options.user, id: fromBase64Url(options.user.id) },
                excludeCredentials: (options.excludeCredentials || []).map((descriptor) => ({ ...descriptor, id: fromBase64Url(descriptor.id) })) };
            }
            static parseRequestOptionsFromJSON(options) {
              return { ...options, challenge: fromBase64Url(options.challenge),
                allowCredentials: (options.allowCredentials || []).map((descriptor) => ({ ...descriptor, id: fromBase64Url(descriptor.id) })) };
            }
          }

          // https://www.w3.org/TR/webauthn-3/#sctn-createCredential and #sctn-getAssertion, the steps a page can observe.
          const ceremony = (kind, options) => {
            const publicKey = options && options.publicKey;
            if (!publicKey) return Promise.reject(new DOMException('Only public key credentials are supported.', 'NotSupportedError'));
            const signal = options.signal;
            if (signal && signal.aborted) return Promise.reject(aborted(signal));
            const host = location.hostname;
            if (/^[0-9.]+$/.test(host) || host.startsWith('[')) return Promise.reject(new DOMException('This is an invalid domain.', 'SecurityError'));
            const rpId = kind === 'create' ? (publicKey.rp && publicKey.rp.id) || host : publicKey.rpId || host;
            if (rpId !== host && !host.endsWith('.' + rpId)) {
              return Promise.reject(new DOMException('The relying party ID is not a registrable domain suffix of, nor equal to the current domain.', 'SecurityError'));
            }

            let request;
            try {
              const selection = publicKey.authenticatorSelection || {};
              request = kind === 'create'
                ? { rpId, origin: location.origin, challenge: base64Of(publicKey.challenge, 'challenge'), userId: base64Of(publicKey.user.id, 'id'),
                    exclude: (publicKey.excludeCredentials || []).map((descriptor) => base64Of(descriptor.id, 'id')),
                    residentKey: selection.residentKey || (selection.requireResidentKey ? 'required' : 'discouraged'),
                    userVerification: selection.userVerification || 'preferred', algorithms: (publicKey.pubKeyCredParams || []).map((parameter) => parameter.alg) }
                : { rpId, origin: location.origin, challenge: base64Of(publicKey.challenge, 'challenge'),
                    allow: (publicKey.allowCredentials || []).map((descriptor) => base64Of(descriptor.id, 'id')),
                    userVerification: publicKey.userVerification || 'preferred', conditional: options.mediation === 'conditional' };
            } catch (error) {
              return Promise.reject(error);
            }

            return new Promise((resolve, reject) => {
              let settled = false;
              const abort = () => { if (!settled) { settled = true; reject(aborted(signal)); } };
              if (signal) signal.addEventListener('abort', abort, { once: true });
              setTimeout(() => {
                if (settled) return;
                const result = JSON.parse(bridge(kind, JSON.stringify(request)));
                if (result.pending) return;
                settled = true;
                if (signal) signal.removeEventListener('abort', abort);
                if (result.error) reject(new DOMException(result.message, result.error));
                else resolve(new PublicKeyCredential(token, result, kind === 'create'
                  ? new AuthenticatorAttestationResponse(token, result)
                  : new AuthenticatorAssertionResponse(token, result)));
              }, 0);
            });
          };

          class CredentialsContainer {
            constructor(key) { if (key !== token) illegal('CredentialsContainer'); }
            create(options) { return ceremony('create', options); }
            get(options) { return ceremony('get', options); }
            store() { return Promise.reject(new DOMException('Storing a public key credential is not supported.', 'NotSupportedError')); }
            preventSilentAccess() { return Promise.resolve(); }
          }

          const expose = (name, value) => {
            Object.defineProperty(value.prototype, Symbol.toStringTag, { value: name, configurable: true });
            Object.defineProperty(globalThis, name, { value, writable: true, configurable: true, enumerable: false });
          };
          expose('Credential', Credential);
          expose('PublicKeyCredential', PublicKeyCredential);
          expose('AuthenticatorResponse', AuthenticatorResponse);
          expose('AuthenticatorAttestationResponse', AuthenticatorAttestationResponse);
          expose('AuthenticatorAssertionResponse', AuthenticatorAssertionResponse);
          expose('CredentialsContainer', CredentialsContainer);

          const container = new CredentialsContainer(token);
          if (typeof navigator === 'object') {
            Object.defineProperty(Object.getPrototypeOf(navigator), 'credentials', { get() { return container; }, enumerable: true, configurable: true });
          }
        })
        """;
}

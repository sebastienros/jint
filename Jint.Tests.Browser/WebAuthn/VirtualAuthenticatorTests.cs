using System.Security.Cryptography;
using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.WebAuthn;

/// <summary>
/// https://www.w3.org/TR/webauthn-3/ — a page registers a passkey and signs in with it against a context's
/// <see cref="VirtualAuthenticator"/>, and the relying party can verify what it receives.
/// </summary>
public sealed class VirtualAuthenticatorTests
{
    private const string Register = """
        async () => {
          const credential = await navigator.credentials.create({ publicKey: {
            rp: { name: 'Site' }, user: { id: new Uint8Array([1, 2, 3]), name: 'ada', displayName: 'Ada' },
            challenge: new Uint8Array([9, 8, 7]), pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
            authenticatorSelection: { residentKey: 'required', userVerification: 'required' } } });
          return JSON.stringify({ attestation: credential.response instanceof AuthenticatorAttestationResponse,
            credential: credential instanceof PublicKeyCredential, json: credential.toJSON(),
            transports: credential.response.getTransports() });
        }
        """;

    private const string SignIn = """
        async (conditional) => {
          const credential = await navigator.credentials.get({ mediation: conditional ? 'conditional' : undefined,
            publicKey: { challenge: new Uint8Array([4, 5, 6]), userVerification: 'required' } });
          return JSON.stringify(credential.toJSON());
        }
        """;

    private static async Task<LoopbackPage> SiteAsync(VirtualAuthenticator? authenticator)
    {
        var site = await LoopbackPage.CreateAsync(server => server.Map("/", _ => LoopbackResponse.Html("<!doctype html>")));
        if (authenticator is not null)
        {
            site.Context.AddVirtualAuthenticator(authenticator);
        }

        // WebAuthn refuses an IP address as a relying party, so the site is browsed by name.
        await site.Page.NavigateAsync(site.Url("/").Replace("127.0.0.1", "localhost", StringComparison.Ordinal));
        return site;
    }

    [Test]
    public async Task APasskeyRegisteredOnAPageSignsInWithAVerifiableSignature()
    {
        var authenticator = new VirtualAuthenticator();
        await using var site = await SiteAsync(authenticator);

        using var registration = JsonDocument.Parse((await site.Page.EvaluateAndAwaitAsync<string>(Register, null))!);
        registration.RootElement.GetProperty("attestation").GetBoolean().Should().BeTrue();
        registration.RootElement.GetProperty("credential").GetBoolean().Should().BeTrue();
        registration.RootElement.GetProperty("transports")[0].GetString().Should().Be("internal");
        var created = registration.RootElement.GetProperty("json").GetProperty("response");
        ClientData(created).GetProperty("type").GetString().Should().Be("webauthn.create");
        ClientData(created).GetProperty("challenge").GetString().Should().Be("CQgH");
        authenticator.Credentials.Should().ContainSingle().Which.RpId.Should().Be("localhost");

        // A discoverable credential, found by a request that names none, signs with the key the page was given.
        using var assertion = JsonDocument.Parse((await site.Page.EvaluateAndAwaitAsync<string>(SignIn, true))!);
        var response = assertion.RootElement.GetProperty("response");
        ClientData(response).GetProperty("type").GetString().Should().Be("webauthn.get");
        response.GetProperty("userHandle").GetString().Should().Be("AQID");
        var authenticatorData = Bytes(response.GetProperty("authenticatorData"));
        authenticatorData[32].Should().Be(0x05, "the user is present and verified");
        authenticatorData.AsSpan(0, 32).ToArray().Should().Equal(SHA256.HashData("localhost"u8));

        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Bytes(created.GetProperty("publicKey")), out _);
        var signed = authenticatorData.Concat(SHA256.HashData(Bytes(response.GetProperty("clientDataJSON")))).ToArray();
        key.VerifyData(signed, Bytes(response.GetProperty("signature")), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
            .Should().BeTrue();
        authenticator.Credentials[0].SignCount.Should().Be(1);
    }

    [Test]
    public async Task WithoutAnAuthenticatorACreateIsNotAllowedAndAConditionalGetWaitsForItsSignal()
    {
        var authenticator = new VirtualAuthenticator();
        await using var site = await SiteAsync(authenticator);
        site.Context.RemoveVirtualAuthenticator(authenticator).Should().BeTrue();
        (await site.Page.EvaluateAndAwaitAsync<string>("async () => { try { await (" + Register + ")(); return 'created'; } catch (e) { return e.name; } }", null))
            .Should().Be("NotAllowedError");
        (await site.Page.EvaluateAndAwaitAsync<string>(
            """
            async () => {
              const controller = new AbortController();
              const request = navigator.credentials.get({ mediation: 'conditional', signal: controller.signal,
                publicKey: { challenge: new Uint8Array([1]) } }).then(() => 'resolved', (e) => e.name);
              await new Promise((resolve) => setTimeout(resolve, 50));
              controller.abort();
              return (await PublicKeyCredential.isConditionalMediationAvailable()) + ':' + await request;
            }
            """, null)).Should().Be("true:AbortError");
    }

    [Test]
    public async Task AContextWithoutAnAuthenticatorHasNoWebAuthn()
    {
        await using var site = await SiteAsync(authenticator: null);
        (await site.Page.EvaluateAsync<string>("typeof PublicKeyCredential + ':' + ('credentials' in navigator)")).Should().Be("undefined:false");
    }

    [Test]
    public async Task TheInterfacesCannotBeConstructedAndAnIpAddressIsNoRelyingParty()
    {
        await using var site = await LoopbackPage.CreateAsync(server => server.Map("/", _ => LoopbackResponse.Html("<!doctype html>")));
        site.Context.AddVirtualAuthenticator(new VirtualAuthenticator());
        await site.Page.NavigateAsync(site.Url("/"));
        (await site.Page.EvaluateAsync<string>("(() => { try { new PublicKeyCredential(); return 'constructed'; } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
        (await site.Page.EvaluateAndAwaitAsync<string>("async () => { try { await (" + Register + ")(); return 'created'; } catch (e) { return e.name; } }", null))
            .Should().Be("SecurityError");
    }

    private static byte[] Bytes(JsonElement base64Url)
    {
        var text = base64Url.GetString()!.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text + new string('=', (4 - (text.Length % 4)) % 4));
    }

    private static JsonElement ClientData(JsonElement response)
        => JsonDocument.Parse(Bytes(response.GetProperty("clientDataJSON"))).RootElement;
}

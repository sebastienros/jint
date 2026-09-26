#nullable enable
using System.Security.Cryptography;
using System.Text;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

public class XmlPreparedInputsTests
{
    [Test]
    public void SixPinnedInputsBindArchiveMetadataAndPreparedStrings()
    {
        XmlCorpus.Lock.PreparedInputsSha256.Should().Be("e464013f47b65c96fe7633c302f81c319449a078df179108ae8329d802dc823a");
        var rows = XmlPreparedInputs.Rows;
        var expectedOutcomes = new Dictionary<string, XmlOutcomeKind>(StringComparer.Ordinal)
        {
            ["xmlconf/japanese/japanese.xml#pr-xml-euc-jp"] = XmlOutcomeKind.OptionalPolicyVerified,
            ["xmlconf/japanese/japanese.xml#pr-xml-iso-2022-jp"] = XmlOutcomeKind.OptionalPolicyVerified,
            ["xmlconf/japanese/japanese.xml#pr-xml-shift_jis"] = XmlOutcomeKind.OptionalPolicyVerified,
            ["xmlconf/japanese/japanese.xml#weekly-euc-jp"] = XmlOutcomeKind.OptionalPolicyVerified,
            ["xmlconf/japanese/japanese.xml#weekly-iso-2022-jp"] = XmlOutcomeKind.OptionalPolicyVerified,
            ["xmlconf/japanese/japanese.xml#weekly-shift_jis"] = XmlOutcomeKind.OptionalPolicyVerified
        };
        rows.Keys.Should().BeEquivalentTo(expectedOutcomes.Keys);
        foreach (var entry in rows.Values)
        {
            var row = XmlCorpus.Case(entry.Key);
            var raw = XmlCorpus.Bytes(entry.InputPath);
            Hash(raw).Should().Be(entry.RawSha256);
            var artifact = Artifact(entry);
            Hash(artifact).Should().Be(entry.DecodedUtf8Sha256);
            var (text, decision) = XmlPreparedInputs.DecodePrepared(row, raw, entry, artifact);
            text.Length.Should().Be(entry.Utf16Length);
            text.Should().StartWith("<?xml version=\"1.0\" encoding=\"" + entry.Declared + "\"?>\r\n");
            text.Should().Contain("\r\n");
            decision.Decision.Should().Be(entry.Decision);
            decision.Declared.Should().Be(entry.Declared);
            XmlConformanceRunner.Run(row).Kind.Should().Be(expectedOutcomes[entry.Key]);
        }
    }

    [Test]
    public void MissingChangedAndSwappedArtifactsAreHarnessFailures()
    {
        var entries = XmlPreparedInputs.Rows.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var row = XmlCorpus.Case(entries[0].Key);
        var good = Artifact(entries[0]);
        AssertFailure(row, null, "prepared-artifact-missing");
        AssertFailure(row, good[..^1], "prepared-artifact-hash");
        AssertFailure(row, [.. good, 0xff], "prepared-artifact-utf8");
        AssertFailure(row, [0xef, 0xbb, 0xbf, .. good], "prepared-artifact-bom");
        AssertFailure(row, Artifact(entries[1]), "prepared-artifact-hash");
        AssertFailure(row, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(good).Replace("\r\n", "\n", StringComparison.Ordinal)),
            "prepared-artifact-hash");
    }

    [Test]
    public void RawIdentityAndRegistryAreRequiredBeforeDecoding()
    {
        var entries = XmlPreparedInputs.Rows.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var entry = entries[0];
        var row = XmlCorpus.Case(entry.Key);
        var raw = XmlCorpus.Bytes(entry.InputPath);
        var artifact = Artifact(entry);
        Signature(() => XmlPreparedInputs.DecodePrepared(row, [.. raw, 0], entry, artifact))
            .Should().Be("prepared-raw-hash");
        Signature(() => XmlPreparedInputs.DecodePrepared(row, [0xef, 0xbb, 0xbf, .. raw], entry, artifact))
            .Should().Be("prepared-raw-signature");
        Signature(() => XmlPreparedInputs.DecodePrepared(row, raw, entry with { InputPath = entries[1].InputPath }, artifact))
            .Should().Be("prepared-case-identity");
        Signature(() => XmlPreparedInputs.ValidateRegistry(entries[..^1], XmlCorpus.Lock))
            .Should().Be("prepared-registry");
        Signature(() => XmlPreparedInputs.ValidateRegistry([.. entries[..^1], entries[0]], XmlCorpus.Lock))
            .Should().Be("prepared-registry");
        Signature(() => XmlPreparedInputs.ValidateRegistry([entries[0] with { Key = "other" }, .. entries[1..]], XmlCorpus.Lock))
            .Should().Be("prepared-registry");
        Signature(() => XmlPreparedInputs.ValidateRegistry([entries[0] with { RawSha256 = entries[1].RawSha256 }, .. entries[1..]], XmlCorpus.Lock))
            .Should().Be("prepared-registry-identity");
        Signature(() => XmlPreparedInputs.ValidateRegistry([entries[0] with { InputPath = entries[1].InputPath }, .. entries[1..]], XmlCorpus.Lock))
            .Should().Be("prepared-registry-identity");
    }

    [Test]
    public void DigestLengthAndDeclarationChecksUseSuppliedBytes()
    {
        var entry = XmlPreparedInputs.Rows.Values.First();
        var row = XmlCorpus.Case(entry.Key);
        var raw = XmlCorpus.Bytes(entry.InputPath);
        var artifact = Artifact(entry);
        Signature(() => XmlPreparedInputs.DecodePrepared(row, raw,
            entry with { DecodedUtf8Sha256 = new string('0', 64) }, artifact))
            .Should().Be("prepared-artifact-hash");
        Signature(() => XmlPreparedInputs.DecodePrepared(row, raw,
            entry with { Utf16Length = entry.Utf16Length + 1 }, artifact))
            .Should().Be("prepared-artifact-length");
        Signature(() => XmlPreparedInputs.DecodePrepared(row, raw,
            entry with { Declared = "UTF-8" }, artifact))
            .Should().Be("prepared-declaration");
        var changedDeclaration = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(artifact)
            .Replace("encoding=\"" + entry.Declared + "\"", "encoding=\"UTF-8\"", StringComparison.Ordinal));
        Signature(() => XmlPreparedInputs.DecodePrepared(row, raw,
            entry with { DecodedUtf8Sha256 = Hash(changedDeclaration),
                Utf16Length = Encoding.UTF8.GetString(changedDeclaration).Length }, changedDeclaration))
            .Should().Be("prepared-declaration");
    }

    [Test]
    public void GenericByteDecoderStillOwnsOtherInputs()
    {
        foreach (var key in new[]
        {
            "xmlconf/japanese/japanese.xml#pr-xml-utf-8",
            "xmlconf/japanese/japanese.xml#pr-xml-utf-16",
            "xmlconf/japanese/japanese.xml#pr-xml-little"
        })
        {
            var row = XmlCorpus.Case(key);
            var raw = XmlCorpus.Bytes(row.InputPath);
            var preparedRoute = XmlPreparedInputs.Load(row, raw);
            var generic = XmlByteDecoder.Decode(raw);
            preparedRoute.Text.Should().Be(generic.Text);
            preparedRoute.Decision.Decision.Should().Be(generic.Decision.Decision);
            preparedRoute.Decision.Status.Should().Be(generic.Decision.Status);
            preparedRoute.Decision.Declared.Should().Be(generic.Decision.Declared);
        }
        var latin = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><é/>";
        var latinBytes = Encoding.Latin1.GetBytes(latin);
        XmlByteDecoder.Decode(latinBytes).Decision.Decision.Should().Be("iso-8859-1-declaration");
        var synthetic = new XmlCorpusCase { Key = "synthetic", InputPath = "xmlconf/japanese/pr-xml-euc-jp.xml" };
        XmlPreparedInputs.Load(synthetic, XmlCorpus.Bytes(synthetic.InputPath)).Decision.Status
            .Should().Be("strict-decode-error");
    }

    private static byte[] Artifact(XmlPreparedInputRow entry) => File.ReadAllBytes(Path.Combine(
        XmlCorpus.Root, "Cache", "DecodedJapanese", entry.RawSha256 + ".utf8"));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Signature(Action action)
    {
        return Assert.Throws<XmlPreparedInputException>(() => action())!.Signature;
    }

    private static void AssertFailure(XmlCorpusCase row, byte[]? artifact, string expected)
    {
        var outcome = XmlConformanceRunner.Run(row, usePreparedOverride: true, testPrepared: artifact);
        outcome.Kind.Should().Be(XmlOutcomeKind.HarnessFailure);
        outcome.Signature.Should().Be(expected);
    }
}

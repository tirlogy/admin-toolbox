// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AdminToolbox.Core.Helpers;

namespace AdminToolbox.Core.Tests;

public class HelperTests
{
    [Fact]
    public void Password_UsesSelectedClassesAndLength()
    {
        for (var i = 0; i < 50; i++)
        {
            var pw = PasswordGenerator.Generate(new PasswordOptions(12, Upper: true, Lower: false, Digits: true, Symbols: false, ExcludeAmbiguous: true));
            Assert.Equal(12, pw.Length);
            Assert.Contains(pw, char.IsAsciiLetterUpper);
            Assert.Contains(pw, char.IsAsciiDigit);
            Assert.DoesNotContain(pw, c => char.IsAsciiLetterLower(c) || "Il1O0o|".Contains(c));
        }

        var all = PasswordGenerator.Generate(new PasswordOptions(4));
        Assert.Contains(all, c => !char.IsAsciiLetterOrDigit(c));
        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(new PasswordOptions(10, false, false, false, false)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(new PasswordOptions(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(new PasswordOptions(257)));
    }

    [Fact]
    public void Password_EntropyAndRating()
    {
        Assert.Equal(20 * Math.Log2(26 + 26 + 10 + 28), PasswordGenerator.EntropyBits(new PasswordOptions()), 6);
        Assert.Equal(0, PasswordGenerator.EntropyBits(new PasswordOptions(10, false, false, false, false)));
        Assert.Equal(Strength.VeryWeak, PasswordGenerator.Rate(20));
        Assert.Equal(Strength.Weak, PasswordGenerator.Rate(30));
        Assert.Equal(Strength.Fair, PasswordGenerator.Rate(50));
        Assert.Equal(Strength.Strong, PasswordGenerator.Rate(70));
        Assert.Equal(Strength.VeryStrong, PasswordGenerator.Rate(120));
        var distinct = Enumerable.Range(0, 20).Select(_ => PasswordGenerator.Generate(new PasswordOptions())).Distinct().Count();
        Assert.Equal(20, distinct);
    }

    [Fact]
    public void Passphrase()
    {
        Assert.Equal(1296, PasswordGenerator.Words.Count);
        var p = PasswordGenerator.GeneratePassphrase(new PassphraseOptions(4, ".", Capitalize: true, AppendNumber: true));
        var parts = p.Split('.');
        Assert.Equal(5, parts.Length);
        Assert.All(parts.Take(4), w => Assert.True(char.IsUpper(w[0])));
        Assert.True(int.Parse(parts[4]) < 100);
        var plain = PasswordGenerator.GeneratePassphrase(new PassphraseOptions(3, " ", false, false));
        Assert.Equal(3, plain.Split(' ').Length);
        Assert.All(plain.Split(' '), w => Assert.Contains(w, PasswordGenerator.Words));
        Assert.Equal(5 * Math.Log2(1296) + Math.Log2(100), PasswordGenerator.PassphraseEntropyBits(new PassphraseOptions()), 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.GeneratePassphrase(new PassphraseOptions(2)));
    }

    private static string B64Url(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Jwt_DecodesAndChecksTimes()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var token = B64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "." + B64Url("{\"sub\":\"42\",\"name\":\"Jürgen\",\"iat\":1699990000,\"exp\":1700003600}") + ".sig";
        var info = JwtDecoder.Decode("Bearer " + token, now);
        Assert.Equal("HS256", info.Algorithm);
        Assert.Equal("JWT", info.Type);
        Assert.Equal(JwtStatus.Valid, info.Status);
        Assert.Contains("\"name\": \"Jürgen\"", info.PayloadJson);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700003600), info.Expires);
        Assert.False(info.Unsigned);

        Assert.Equal(JwtStatus.Expired, JwtDecoder.Decode(token, now.AddHours(2)).Status);
        var nbf = B64Url("{\"alg\":\"none\"}") + "." + B64Url("{\"nbf\":1800000000,\"exp\":1900000000.5}") + ".";
        var nbfInfo = JwtDecoder.Decode(nbf, now);
        Assert.Equal(JwtStatus.NotYetValid, nbfInfo.Status);
        Assert.True(nbfInfo.Unsigned);
        var noExp = B64Url("{\"alg\":\"RS256\"}") + "." + B64Url("{\"exp\":\"soon\",\"iat\":1e20}") + ".x";
        var noExpInfo = JwtDecoder.Decode(noExp, now);
        Assert.Equal(JwtStatus.NoExpiry, noExpInfo.Status);
        Assert.Null(noExpInfo.IssuedAt);
    }

    [Theory]
    [InlineData("abc", "jwt.format")]
    [InlineData("a.b.c.d.e", "jwt.jwe")]
    [InlineData("!!!.e30.x", "jwt.base64")]
    [InlineData("a.e30.x", "jwt.base64")]
    [InlineData("bm9wZQ.e30.x", "jwt.json")]
    [InlineData("WzFd.e30.x", "jwt.json")]
    public void Jwt_Errors(string token, string code)
    {
        var ex = Assert.Throws<FormatException>(() => JwtDecoder.Decode(token, DateTimeOffset.UnixEpoch));
        Assert.Equal(code, ex.Message);
    }

    [Theory]
    [InlineData(EncodingKind.Base64, "Grüße/?", "R3LDvMOfZS8/")]
    [InlineData(EncodingKind.Base64Url, "Grüße/?", "R3LDvMOfZS8_")]
    [InlineData(EncodingKind.Url, "a b&c=ü", "a%20b%26c%3D%C3%BC")]
    [InlineData(EncodingKind.Hex, "Hi!", "486921")]
    public void Encoders_RoundTrip(EncodingKind kind, string text, string encoded)
    {
        Assert.Equal(encoded, Encoders.Encode(kind, text));
        var decoded = Encoders.Decode(kind, encoded);
        Assert.Equal(text, decoded.Text);
        Assert.False(decoded.IsBinary);
    }

    [Fact]
    public void Encoders_EdgeCases()
    {
        Assert.Equal("a b", Encoders.Decode(EncodingKind.Url, "a+b").Text);
        Assert.Equal("Hi!", Encoders.Decode(EncodingKind.Hex, "0x48:69-21").Text);
        Assert.Equal("Hi", Encoders.Decode(EncodingKind.Base64, "SG k=\n").Text);
        var binary = Encoders.Decode(EncodingKind.Base64, "/w==");
        Assert.True(binary.IsBinary);
        Assert.Equal("ff", binary.Text);
        Assert.Throws<FormatException>(() => Encoders.Decode(EncodingKind.Base64, "abcde"));
        Assert.Throws<FormatException>(() => Encoders.Decode(EncodingKind.Hex, "xyz1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Encoders.Encode((EncodingKind)99, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Encoders.Decode((EncodingKind)99, "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    [InlineData("Ünïcödé 🚀 with a length that crosses the 56 byte padding boundary of md5....")]
    public void Md5_MatchesPlatform(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
#pragma warning disable CA5351 // reference implementation for the test
        Assert.Equal(MD5.HashData(bytes), Md5.HashData(bytes));
#pragma warning restore CA5351
    }

    [Fact]
    public void Hashing_KnownVectors()
    {
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Hashing.Hex(HashKind.Md5, "abc"));
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", Hashing.Hex(HashKind.Sha1, "abc"));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hashing.Hex(HashKind.Sha256, "abc"));
        Assert.StartsWith("ddaf35a193617aba", Hashing.Hex(HashKind.Sha512, "abc"));
        Assert.True(Hashing.IsWeak(HashKind.Md5));
        Assert.False(Hashing.IsWeak(HashKind.Sha256));
        Assert.Equal(4, Hashing.All.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => Hashing.Hex((HashKind)9, "x"));
    }

    private static X509Certificate2 CreateCert(bool ec)
    {
        var now = DateTimeOffset.UtcNow;
        var subject = new X500DistinguishedName("CN=www.example.test, O=Beispiel GmbH, C=DE");
        CertificateRequest req;
        X509SignatureGenerator generator;
        if (ec)
        {
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
            generator = X509SignatureGenerator.CreateForECDsa(key);
        }
        else
        {
            var key = RSA.Create(2048);
            req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            generator = X509SignatureGenerator.CreateForRSA(key, RSASignaturePadding.Pkcs1);
        }

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("www.example.test");
        san.AddDnsName("example.test");
        san.AddIpAddress(System.Net.IPAddress.Parse("192.0.2.1"));
        san.AddEmailAddress("admin@example.test");
        san.AddUri(new Uri("https://example.test/"));
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));

        // Create() without a private key avoids the macOS keychain.
        return req.Create(subject, generator, now.AddDays(-1), now.AddDays(90), new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89 });
    }

    [Fact]
    public void Certificate_DecodesRsaPem()
    {
        using var cert = CreateCert(ec: false);
        var pem = "junk before\n" + cert.ExportCertificatePem() + "\n" + cert.ExportCertificatePem();
        var infos = CertificateDecoder.DecodePem(pem);
        Assert.Equal(2, infos.Count);
        var info = infos[0];
        Assert.Equal(3, info.Version);
        Assert.Equal("CN=www.example.test, O=Beispiel GmbH, C=DE", Reverse(info.Subject));
        Assert.True(info.SelfIssued);
        Assert.Equal("RSA", info.PublicKey.Algorithm);
        Assert.Equal(2048, info.PublicKey.Bits);
        Assert.Equal("sha256WithRSAEncryption", info.SignatureAlgorithm);
        Assert.Equal(new[] { "DNS:www.example.test", "DNS:example.test", "IP:192.0.2.1", "Email:admin@example.test", "URI:https://example.test/" }, info.SubjectAltNames);
        Assert.True(info.IsCa);
        Assert.Equal(1, info.PathLength);
        Assert.Equal(new[] { "digitalSignature", "keyCertSign" }, info.KeyUsages);
        Assert.Equal(new[] { "serverAuth", "clientAuth" }, info.ExtendedKeyUsages);
        Assert.Equal(cert.SerialNumber, info.SerialNumber.Replace(":", string.Empty, StringComparison.Ordinal));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(cert.RawData)), info.Sha256Fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
        Assert.Equal(cert.Thumbprint, info.Sha1Fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
        Assert.Equal(new DateTimeOffset(cert.NotAfter.ToUniversalTime()), info.NotAfter);
        Assert.InRange(info.DaysRemaining(DateTimeOffset.UtcNow), 88, 90);
    }

    private static string Reverse(string dn) => string.Join(", ", dn.Split(", ").Reverse());

    [Fact]
    public void Certificate_DecodesEcAndRawBase64()
    {
        using var cert = CreateCert(ec: true);
        var info = Assert.Single(CertificateDecoder.DecodePem(Convert.ToBase64String(cert.RawData)));
        Assert.Equal("EC", info.PublicKey.Algorithm);
        Assert.Equal("P-256", info.PublicKey.Curve);
        Assert.Equal(256, info.PublicKey.Bits);
        Assert.Equal("ecdsa-with-SHA256", info.SignatureAlgorithm);
    }

    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----", "cert.privatekey")]
    [InlineData("-----BEGIN CERTIFICATE REQUEST-----\nabc", "cert.csr")]
    [InlineData("   ", "cert.none")]
    [InlineData("-----BEGIN CERTIFICATE-----\nabc", "cert.invalid")]
    [InlineData("-----BEGIN CERTIFICATE-----\n!!!\n-----END CERTIFICATE-----", "cert.invalid")]
    [InlineData("-----BEGIN CERTIFICATE-----\nMAMCAQE=\n-----END CERTIFICATE-----", "cert.invalid")]
    public void Certificate_Errors(string pem, string code) =>
        Assert.Equal(code, Assert.Throws<FormatException>(() => CertificateDecoder.DecodePem(pem)).Message);

    [Fact]
    public void PublicKey_OtherAlgorithms()
    {
        using var ed = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.Equal(384, PublicKeyInfo.Describe(ed.ExportSubjectPublicKeyInfo()).Bits);
        Assert.Equal("Ed25519", PublicKeyInfo.DescribeSpki(Convert.FromBase64String("MCowBQYDK2VwAyEAGb9ECWmEzf6FQbrBZ9w7lshQhqowtrbLDFw4rXAxZuE=")).Algorithm);
        Assert.Equal("RSA", Oids.Name(Oids.Rsa));
        Assert.Equal("1.2.3", Oids.Name("1.2.3"));
        Assert.Null(Oids.CurveBits("1.2.3"));
    }
}

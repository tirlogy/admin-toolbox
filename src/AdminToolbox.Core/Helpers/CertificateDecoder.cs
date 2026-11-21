// SPDX-License-Identifier: MIT
using System.Formats.Asn1;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AdminToolbox.Core.Helpers;

public sealed record CertificateInfo(
    int Version,
    string SerialNumber,
    string SignatureAlgorithm,
    string Issuer,
    string Subject,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    PublicKeyDescription PublicKey,
    IReadOnlyList<string> SubjectAltNames,
    bool? IsCa,
    int? PathLength,
    IReadOnlyList<string> KeyUsages,
    IReadOnlyList<string> ExtendedKeyUsages,
    string Sha256Fingerprint,
    string Sha1Fingerprint)
{
    public bool SelfIssued => Issuer == Subject;

    /// <summary>Whole days until expiry (negative when expired).</summary>
    public int DaysRemaining(DateTimeOffset now) => (int)Math.Floor((NotAfter - now).TotalDays);
}

/// <summary>X.509 decoder for PEM/DER based on System.Formats.Asn1 (X509Certificate2 is not available in the browser).</summary>
public static class CertificateDecoder
{
    private const string Begin = "-----BEGIN CERTIFICATE-----";
    private const string End = "-----END CERTIFICATE-----";

    private static readonly string[] KeyUsageNames =
    {
        "digitalSignature", "nonRepudiation", "keyEncipherment", "dataEncipherment", "keyAgreement",
        "keyCertSign", "cRLSign", "encipherOnly", "decipherOnly",
    };

    /// <summary>
    /// Decodes all certificates in a PEM text (or a single base64 DER blob).
    /// Throws <see cref="FormatException"/> with code: cert.privatekey, cert.csr, cert.none, cert.invalid.
    /// </summary>
    public static IReadOnlyList<CertificateInfo> DecodePem(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
        {
            throw new FormatException("cert.privatekey");
        }

        if (text.Contains("CERTIFICATE REQUEST-----", StringComparison.Ordinal))
        {
            throw new FormatException("cert.csr");
        }

        var blocks = new List<string>();
        var pos = 0;
        while (true)
        {
            var start = text.IndexOf(Begin, pos, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = text.IndexOf(End, start, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new FormatException("cert.invalid");
            }

            blocks.Add(text[(start + Begin.Length)..end]);
            pos = end + End.Length;
        }

        if (blocks.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException("cert.none");
            }

            blocks.Add(text);
        }

        var list = new List<CertificateInfo>();
        foreach (var block in blocks)
        {
            byte[] der;
            try
            {
                der = Convert.FromBase64String(new string(block.Where(c => !char.IsWhiteSpace(c)).ToArray()));
            }
            catch (FormatException)
            {
                throw new FormatException("cert.invalid");
            }

            list.Add(DecodeDer(der));
        }

        return list;
    }

    public static CertificateInfo DecodeDer(byte[] der)
    {
        ArgumentNullException.ThrowIfNull(der);
        try
        {
            return Decode(der);
        }
        catch (Exception ex) when (ex is AsnContentException or CryptographicException or ArgumentException or InvalidOperationException)
        {
            throw new FormatException("cert.invalid", ex);
        }
    }

    private static CertificateInfo Decode(byte[] der)
    {
        var reader = new AsnReader(der, AsnEncodingRules.DER);
        var cert = reader.ReadSequence();
        var tbs = cert.ReadSequence();

        var version = 1;
        var v0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        if (tbs.PeekTag().HasSameClassAndValue(v0))
        {
            var vSeq = tbs.ReadSequence(v0);
            version = (int)vSeq.ReadInteger() + 1;
        }

        var serial = Convert.ToHexString(tbs.ReadIntegerBytes().Span);
        var sigAlg = tbs.ReadSequence().ReadObjectIdentifier();
        var issuer = ReadName(tbs.ReadSequence());
        var validity = tbs.ReadSequence();
        var notBefore = ReadTime(validity);
        var notAfter = ReadTime(validity);
        var subject = ReadName(tbs.ReadSequence());
        var spki = tbs.ReadEncodedValue();
        var publicKey = PublicKeyInfo.DescribeSpki(spki);

        var sans = new List<string>();
        var keyUsages = new List<string>();
        var ekus = new List<string>();
        bool? isCa = null;
        int? pathLength = null;
        var extTag = new Asn1Tag(TagClass.ContextSpecific, 3, true);
        while (tbs.HasData)
        {
            var tag = tbs.PeekTag();
            if (!tag.HasSameClassAndValue(extTag))
            {
                tbs.ReadEncodedValue(); // issuer/subject unique IDs
                continue;
            }

            var extensions = tbs.ReadSequence(extTag).ReadSequence();
            while (extensions.HasData)
            {
                var ext = extensions.ReadSequence();
                var oid = ext.ReadObjectIdentifier();
                if (ext.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean))
                {
                    ext.ReadBoolean();
                }

                var value = ext.ReadOctetString();
                switch (oid)
                {
                    case Oids.SubjectAltName:
                        ReadSans(value, sans);
                        break;
                    case Oids.BasicConstraints:
                        var bc = new AsnReader(value, AsnEncodingRules.DER).ReadSequence();
                        isCa = bc.HasData && bc.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean) && bc.ReadBoolean();
                        if (bc.HasData && bc.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
                        {
                            pathLength = (int)bc.ReadInteger();
                        }

                        break;
                    case Oids.KeyUsage:
                        var bits = new AsnReader(value, AsnEncodingRules.DER).ReadBitString(out _);
                        for (var i = 0; i < KeyUsageNames.Length; i++)
                        {
                            if (i / 8 < bits.Length && ((bits[i / 8] >> (7 - (i % 8))) & 1) == 1)
                            {
                                keyUsages.Add(KeyUsageNames[i]);
                            }
                        }

                        break;
                    case Oids.ExtendedKeyUsage:
                        var eku = new AsnReader(value, AsnEncodingRules.DER).ReadSequence();
                        while (eku.HasData)
                        {
                            ekus.Add(Oids.Name(eku.ReadObjectIdentifier()));
                        }

                        break;
                }
            }
        }

        return new CertificateInfo(
            version,
            Colons(serial),
            Oids.Name(sigAlg),
            issuer,
            subject,
            notBefore,
            notAfter,
            publicKey,
            sans,
            isCa,
            pathLength,
            keyUsages,
            ekus,
            Colons(Convert.ToHexString(SHA256.HashData(der))),
            Colons(Convert.ToHexString(SHA1.HashData(der))));
    }

    private static string Colons(string hex)
    {
        var sb = new StringBuilder(hex.Length * 3 / 2);
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (i > 0)
            {
                sb.Append(':');
            }

            sb.Append(hex, i, Math.Min(2, hex.Length - i));
        }

        return sb.ToString();
    }

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag().HasSameClassAndValue(Asn1Tag.UtcTime) ? reader.ReadUtcTime() : reader.ReadGeneralizedTime();

    private static string ReadName(AsnReader name)
    {
        var parts = new List<string>();
        while (name.HasData)
        {
            var rdn = name.ReadSetOf();
            while (rdn.HasData)
            {
                var atv = rdn.ReadSequence();
                var oid = atv.ReadObjectIdentifier();
                parts.Add(Oids.Name(oid) + "=" + ReadDirectoryString(atv));
            }
        }

        return string.Join(", ", parts);
    }

    private static string ReadDirectoryString(AsnReader reader)
    {
        var tag = reader.PeekTag();
        if (tag.TagClass == TagClass.Universal)
        {
            switch ((UniversalTagNumber)tag.TagValue)
            {
                case UniversalTagNumber.UTF8String:
                case UniversalTagNumber.PrintableString:
                case UniversalTagNumber.IA5String:
                case UniversalTagNumber.T61String:
                case UniversalTagNumber.BMPString:
                case UniversalTagNumber.UniversalString:
                case UniversalTagNumber.VisibleString:
                case UniversalTagNumber.NumericString:
                    return reader.ReadCharacterString((UniversalTagNumber)tag.TagValue);
            }
        }

        return "#" + Convert.ToHexString(reader.ReadEncodedValue().Span);
    }

    private static void ReadSans(byte[] value, List<string> sans)
    {
        var seq = new AsnReader(value, AsnEncodingRules.DER).ReadSequence();
        while (seq.HasData)
        {
            var tag = seq.PeekTag();
            if (tag.TagClass != TagClass.ContextSpecific)
            {
                seq.ReadEncodedValue();
                continue;
            }

            switch (tag.TagValue)
            {
                case 1:
                    sans.Add("Email:" + seq.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                    break;
                case 2:
                    sans.Add("DNS:" + seq.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                    break;
                case 6:
                    sans.Add("URI:" + seq.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                    break;
                case 7:
                    var ip = seq.ReadOctetString(tag);
                    sans.Add("IP:" + (ip.Length is 4 or 16 ? new IPAddress(ip).ToString() : Convert.ToHexString(ip)));
                    break;
                default:
                    seq.ReadEncodedValue();
                    break;
            }
        }
    }
}

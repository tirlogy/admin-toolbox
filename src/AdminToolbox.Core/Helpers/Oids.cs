// SPDX-License-Identifier: MIT
namespace AdminToolbox.Core.Helpers;

/// <summary>Object identifiers used by the certificate and key decoders.</summary>
public static class Oids
{
    public const string Rsa = "1.2.840.113549.1.1.1";
    public const string Ec = "1.2.840.10045.2.1";
    public const string Ed25519 = "1.3.101.112";
    public const string Ed448 = "1.3.101.113";
    public const string SubjectAltName = "2.5.29.17";
    public const string BasicConstraints = "2.5.29.19";
    public const string KeyUsage = "2.5.29.15";
    public const string ExtendedKeyUsage = "2.5.29.37";

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        // Name attributes
        ["2.5.4.3"] = "CN",
        ["2.5.4.4"] = "SN",
        ["2.5.4.5"] = "serialNumber",
        ["2.5.4.6"] = "C",
        ["2.5.4.7"] = "L",
        ["2.5.4.8"] = "ST",
        ["2.5.4.9"] = "street",
        ["2.5.4.10"] = "O",
        ["2.5.4.11"] = "OU",
        ["2.5.4.12"] = "title",
        ["2.5.4.15"] = "businessCategory",
        ["2.5.4.17"] = "postalCode",
        ["2.5.4.42"] = "GN",
        ["2.5.4.97"] = "organizationIdentifier",
        ["1.2.840.113549.1.9.1"] = "E",
        ["0.9.2342.19200300.100.1.25"] = "DC",
        ["1.3.6.1.4.1.311.60.2.1.3"] = "jurisdictionC",

        // Signature algorithms
        ["1.2.840.113549.1.1.4"] = "md5WithRSAEncryption",
        ["1.2.840.113549.1.1.5"] = "sha1WithRSAEncryption",
        ["1.2.840.113549.1.1.10"] = "RSASSA-PSS",
        ["1.2.840.113549.1.1.11"] = "sha256WithRSAEncryption",
        ["1.2.840.113549.1.1.12"] = "sha384WithRSAEncryption",
        ["1.2.840.113549.1.1.13"] = "sha512WithRSAEncryption",
        ["1.2.840.10045.4.1"] = "ecdsa-with-SHA1",
        ["1.2.840.10045.4.3.2"] = "ecdsa-with-SHA256",
        ["1.2.840.10045.4.3.3"] = "ecdsa-with-SHA384",
        ["1.2.840.10045.4.3.4"] = "ecdsa-with-SHA512",

        // Key algorithms and curves
        [Rsa] = "RSA",
        [Ec] = "EC",
        [Ed25519] = "Ed25519",
        [Ed448] = "Ed448",
        ["1.3.101.110"] = "X25519",
        ["1.2.840.10045.3.1.7"] = "P-256",
        ["1.3.132.0.34"] = "P-384",
        ["1.3.132.0.35"] = "P-521",

        // Extended key usages
        ["1.3.6.1.5.5.7.3.1"] = "serverAuth",
        ["1.3.6.1.5.5.7.3.2"] = "clientAuth",
        ["1.3.6.1.5.5.7.3.3"] = "codeSigning",
        ["1.3.6.1.5.5.7.3.4"] = "emailProtection",
        ["1.3.6.1.5.5.7.3.8"] = "timeStamping",
        ["1.3.6.1.5.5.7.3.9"] = "OCSPSigning",
        ["2.5.29.37.0"] = "anyExtendedKeyUsage",
    };

    /// <summary>Friendly name or the dotted OID itself.</summary>
    public static string Name(string oid) => Names.TryGetValue(oid, out var n) ? n : oid;

    public static int? CurveBits(string? curveOid) => curveOid switch
    {
        "1.2.840.10045.3.1.7" => 256,
        "1.3.132.0.34" => 384,
        "1.3.132.0.35" => 521,
        _ => null,
    };
}

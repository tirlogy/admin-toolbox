// SPDX-License-Identifier: MIT
using System.Formats.Asn1;

namespace AdminToolbox.Core.Helpers;

/// <summary>Algorithm and size of a public key.</summary>
public sealed record PublicKeyDescription(string Algorithm, int? Bits, string? Curve);

/// <summary>Reads SubjectPublicKeyInfo (RFC 5280) or a bare PKCS#1 RSA key.</summary>
public static class PublicKeyInfo
{
    public static PublicKeyDescription Describe(ReadOnlyMemory<byte> der)
    {
        try
        {
            return DescribeSpki(der);
        }
        catch (AsnContentException)
        {
            // Some DKIM records publish the PKCS#1 RSAPublicKey without the SPKI wrapper.
            return new PublicKeyDescription("RSA", RsaModulusBits(der), null);
        }
    }

    internal static PublicKeyDescription DescribeSpki(ReadOnlyMemory<byte> der)
    {
        var reader = new AsnReader(der, AsnEncodingRules.BER);
        var spki = reader.ReadSequence();
        var algId = spki.ReadSequence();
        var oid = algId.ReadObjectIdentifier();
        string? param = null;
        if (algId.HasData && algId.PeekTag().HasSameClassAndValue(Asn1Tag.ObjectIdentifier))
        {
            param = algId.ReadObjectIdentifier();
        }

        var key = spki.ReadBitString(out _);
        switch (oid)
        {
            case Oids.Rsa:
                return new PublicKeyDescription("RSA", RsaModulusBits(key), null);
            case Oids.Ec:
                var curve = param is null ? null : Oids.Name(param);
                return new PublicKeyDescription("EC", Oids.CurveBits(param), curve);
            case Oids.Ed25519:
                return new PublicKeyDescription("Ed25519", 256, null);
            case Oids.Ed448:
                return new PublicKeyDescription("Ed448", 456, null);
            default:
                return new PublicKeyDescription(Oids.Name(oid), null, null);
        }
    }

    private static int RsaModulusBits(ReadOnlyMemory<byte> pkcs1)
    {
        var reader = new AsnReader(pkcs1, AsnEncodingRules.BER);
        var seq = reader.ReadSequence();
        var modulus = seq.ReadIntegerBytes().Span;
        var i = 0;
        while (i < modulus.Length - 1 && modulus[i] == 0)
        {
            i++;
        }

        var first = modulus[i];
        var bits = (modulus.Length - i - 1) * 8;
        while (first != 0)
        {
            bits++;
            first >>= 1;
        }

        return bits;
    }
}

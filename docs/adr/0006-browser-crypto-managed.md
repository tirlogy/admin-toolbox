# 0006 · Cryptography in the browser: platform SHA, own MD5 and ASN.1 certificate decoder

- Status: accepted
- Date: 2026-11-21

## Context
.NET on WebAssembly only supports a subset of `System.Security.Cryptography`: SHA-1/256/384/512,
HMAC and `RandomNumberGenerator` work; MD5 and `X509Certificate2` throw `PlatformNotSupportedException`.
The helpers need MD5 (with a warning), SHA hashes, secure random passwords and a PEM certificate decoder.

## Decision
- SHA hashes and `RandomNumberGenerator` use the platform APIs.
- MD5 is implemented in `AdminToolbox.Core.Helpers.Md5` (RFC 1321, about 80 lines) and tested
  against the platform implementation on the desktop runtime.
- Certificates are decoded with `System.Formats.Asn1` (`AsnReader`), which is fully managed. The
  decoder reads version, serial, issuer/subject, validity, public key (RSA bits, EC curve, Ed25519),
  SAN, basic constraints, key usage and extended key usage and computes SHA-256/SHA-1 fingerprints.
  The same SPKI reader determines DKIM key sizes.
- Private keys and CSRs are refused with a clear message.

## Alternatives
- **JavaScript libraries via interop (e.g. a JS MD5 or PKI.js)**: adds third-party code and JS
  interop; the managed code is small and testable.
- **Dropping MD5**: the concept explicitly asks for MD5 with a warning.

## Consequences
- No external crypto dependencies. Signature verification of certificates is out of scope.

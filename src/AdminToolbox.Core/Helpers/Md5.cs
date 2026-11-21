// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Numerics;

namespace AdminToolbox.Core.Helpers;

/// <summary>
/// Managed MD5 (RFC 1321). The browser runtime of .NET has no MD5, so the toolbox brings its own.
/// Only for checksums of legacy systems, never for security.
/// </summary>
public static class Md5
{
    private static readonly int[] Shifts =
    {
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
    };

    private static readonly uint[] K = Enumerable.Range(0, 64)
        .Select(i => (uint)(long)Math.Floor(Math.Abs(Math.Sin(i + 1)) * 4294967296.0))
        .ToArray();

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        var bitLength = (ulong)data.Length * 8;
        var paddedLength = ((data.Length + 8) / 64 + 1) * 64;
        var buffer = new byte[paddedLength];
        data.CopyTo(buffer);
        buffer[data.Length] = 0x80;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(paddedLength - 8), bitLength);

        uint a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
        Span<uint> m = stackalloc uint[16];
        for (var chunk = 0; chunk < paddedLength; chunk += 64)
        {
            for (var i = 0; i < 16; i++)
            {
                m[i] = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(chunk + (i * 4)));
            }

            uint a = a0, b = b0, c = c0, d = d0;
            for (var i = 0; i < 64; i++)
            {
                uint f;
                int g;
                if (i < 16)
                {
                    f = (b & c) | (~b & d);
                    g = i;
                }
                else if (i < 32)
                {
                    f = (d & b) | (~d & c);
                    g = ((5 * i) + 1) % 16;
                }
                else if (i < 48)
                {
                    f = b ^ c ^ d;
                    g = ((3 * i) + 5) % 16;
                }
                else
                {
                    f = c ^ (b | ~d);
                    g = (7 * i) % 16;
                }

                f = f + a + K[i] + m[g];
                a = d;
                d = c;
                c = b;
                b += BitOperations.RotateLeft(f, Shifts[i]);
            }

            a0 += a;
            b0 += b;
            c0 += c;
            d0 += d;
        }

        var result = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(result, a0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), b0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), c0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), d0);
        return result;
    }
}

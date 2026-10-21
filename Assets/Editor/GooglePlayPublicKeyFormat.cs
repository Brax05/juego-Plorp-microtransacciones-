using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

/// <summary>Reads RSA SubjectPublicKeyInfo without APIs unsupported by Unity's Mono runtime.</summary>
public static class GooglePlayPublicKeyFormat
{
    public static void Validate(byte[] encoded)
    {
        if (encoded == null || encoded.Length < 256 || encoded.Length > 1024)
            throw new CryptographicException("Invalid public key size.");
        using (var input = Reader(encoded))
        using (var sequence = Reader(Read(input, 0x30)))
        {
            End(input);
            using (var algorithm = Reader(Read(sequence, 0x30)))
            {
                byte[] rsaOid = { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
                if (!Read(algorithm, 0x06).SequenceEqual(rsaOid) || Read(algorithm, 0x05).Length != 0)
                    throw new CryptographicException("Expected an RSA public key.");
                End(algorithm);
            }
            using (var bits = Reader(Read(sequence, 0x03)))
            {
                End(sequence);
                if (bits.ReadByte() != 0) throw new CryptographicException("Unexpected unused bits.");
                using (var parameters = Reader(Read(bits, 0x30)))
                using (var rsa = RSA.Create())
                {
                    End(bits);
                    var key = new RSAParameters { Modulus = Integer(parameters), Exponent = Integer(parameters) };
                    End(parameters);
                    rsa.ImportParameters(key);
                    if (rsa.KeySize < 2048) throw new CryptographicException("RSA key is too short.");
                }
            }
        }
    }

    static BinaryReader Reader(byte[] data) => new BinaryReader(new MemoryStream(data, false));
    static void End(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new CryptographicException("Unexpected trailing key data.");
    }
    static byte[] Integer(BinaryReader reader)
    {
        byte[] value = Read(reader, 0x02);
        if (value.Length == 0 || (value[0] & 0x80) != 0)
            throw new CryptographicException("Invalid RSA integer.");
        if (value[0] == 0) value = value.Skip(1).ToArray();
        if (value.Length == 0) throw new CryptographicException("Empty RSA integer.");
        return value;
    }
    static byte[] Read(BinaryReader reader, byte tag)
    {
        if (reader.ReadByte() != tag) throw new CryptographicException("Unexpected public key format.");
        int length = reader.ReadByte();
        if ((length & 0x80) != 0)
        {
            int count = length & 0x7f;
            if (count < 1 || count > 2) throw new CryptographicException("Invalid DER length.");
            length = 0;
            for (int i = 0; i < count; i++) length = (length << 8) | reader.ReadByte();
        }
        if (length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new CryptographicException("Truncated public key.");
        return reader.ReadBytes(length);
    }
}

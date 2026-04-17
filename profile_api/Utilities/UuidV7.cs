namespace ProfilesApi.Utilities;

/// <summary>
/// Generates UUID version 7 (time-ordered, random) per RFC 9562.
/// Layout:
///   48 bits  – Unix timestamp in milliseconds
///    4 bits  – version (0x7)
///   12 bits  – random (sub-ms precision fill)
///    2 bits  – variant (0b10)
///   62 bits  – random
/// </summary>
public static class UuidV7
{
    public static Guid NewGuid()
    {
        // 48-bit millisecond timestamp
        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Span<byte> bytes = stackalloc byte[16];
        Random.Shared.NextBytes(bytes);

        // Overwrite bytes 0-5 with timestamp (big-endian)
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)(unixMs);

        // Set version 7 in the high nibble of byte 6
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);

        // Set variant bits (10xx xxxx) in byte 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}

#if TOOLS
using System;
using System.IO;
using Godot;

namespace Ringo;

/// <summary>
/// Reads the sample rate of an audio file directly from its header, for
/// formats whose imported resource does not expose it (OGG Vorbis, MP3).
/// </summary>
public static class AudioSampleRateProbe
{
    private const int HeaderBytesToRead = 65536;

    /// <summary>Return the file's sample rate in Hz, or null if it cannot be determined.</summary>
    public static int? Probe(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        string globalPath = ProjectSettings.GlobalizePath(resourcePath);
        if (!File.Exists(globalPath))
            return null;

        byte[] head;
        int length;
        try
        {
            using var stream = File.OpenRead(globalPath);
            head = new byte[(int)Math.Min(HeaderBytesToRead, stream.Length)];
            length = stream.Read(head, 0, head.Length);
        }
        catch (Exception)
        {
            return null;
        }

        return resourcePath.GetExtension().ToLowerInvariant() switch
        {
            "ogg" => ProbeOggVorbis(head, length),
            "mp3" => ProbeMp3(head, length),
            _ => null,
        };
    }

    // OGG: find the Vorbis identification header packet ("vorbis");
    // the sample rate is a little-endian uint32 at offset 12 within the packet.
    private static int? ProbeOggVorbis(byte[] buffer, int length)
    {
        ReadOnlySpan<byte> magic = stackalloc byte[] { 0x01, (byte)'v', (byte)'o', (byte)'r', (byte)'b', (byte)'i', (byte)'s' };
        for (int i = 0; i + 16 <= length; i++)
        {
            if (buffer.AsSpan(i, magic.Length).SequenceEqual(magic))
                return (int)BitConverter.ToUInt32(buffer, i + 12);
        }
        return null;
    }

    // MP3: find a frame sync (0xFF Ex), then decode MPEG version + sample rate index.
    private static int? ProbeMp3(byte[] buffer, int length)
    {
        for (int i = 0; i + 4 <= length; i++)
        {
            if (buffer[i] != 0xFF || (buffer[i + 1] & 0xE0) != 0xE0)
                continue;

            int version = (buffer[i + 1] >> 3) & 0x03; // 3=MPEG1, 2=MPEG2, 0=MPEG2.5, 1=reserved
            int layer = (buffer[i + 1] >> 1) & 0x03;   // 1=Layer III
            int rateIndex = (buffer[i + 2] >> 2) & 0x03;
            if (version == 1 || layer != 1 || rateIndex == 3)
                continue;

            int[] table = version switch
            {
                3 => new[] { 44100, 48000, 32000 },
                2 => new[] { 22050, 24000, 16000 },
                _ => new[] { 11025, 12000, 8000 },
            };
            return table[rateIndex];
        }
        return null;
    }
}
#endif

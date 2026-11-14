using System.Buffers.Binary;

namespace Legendary.Core.Sound;

/// <summary>
/// A sound recorded for the PC's speaker: a RealSound file, as BIP1.RS. The file is 32 bytes and
/// then the sound. Each byte of the sound is how long the speaker is pushed out for in a pulse 72
/// ticks of the PC's timer long, so from 0 to 72, and a pulse comes 16,572 times a second; a byte
/// is played for 1, 2 or 4 pulses, by the file. A byte of 0 ends the sound.
/// </summary>
public sealed class RealSound
{
    /// <summary>The PC's timer, in ticks a second.</summary>
    public const int TimerHz = 1193182;

    /// <summary>The length of a pulse, in the timer's ticks.</summary>
    public const int PulseTicks = 72;

    private readonly byte[] _widths;

    /// <summary>Reads a sound from its file's bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't a RealSound file.</exception>
    public RealSound(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < 32 || !file.AsSpan(0, 5).SequenceEqual("STEVE"u8) || file[5] == 0)
        {
            throw new InvalidDataException("Not a RealSound file: it doesn't start with STEVE.");
        }

        Repeat = file[5];
        long length = Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(7)), file.Length - 32);
        var widths = file.AsSpan(32, (int)length);

        // the player stops at a zero
        int end = widths.IndexOf((byte)0);
        _widths = (end < 0 ? widths : widths[..end]).ToArray();
    }

    /// <summary>How many pulses each byte is played for.</summary>
    public int Repeat { get; }

    /// <summary>The sound: each byte from 0 to 72, how much of its pulse the speaker is out for.</summary>
    public ReadOnlyMemory<byte> Widths => _widths;

    /// <summary>Bytes played a second.</summary>
    public double Rate => (double)TimerHz / PulseTicks / Repeat;

    /// <summary>How long the sound lasts.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_widths.Length / Rate);

    /// <summary>The sound as samples of eight bits, without a sign, at <see cref="Rate"/>: a pulse's width as a level.</summary>
    public byte[] ToPcm()
    {
        byte[] samples = new byte[_widths.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(Math.Min(_widths[i], (byte)PulseTicks) * 255 / PulseTicks);
        }

        return samples;
    }
}

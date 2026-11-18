namespace Legendary.Pc.Sound;

/// <summary>
/// The sound of what is written to the FM chip, with no machine around it: for making a
/// recording of a song from the registers it writes and when.
/// </summary>
public static class FmRecording
{
    /// <summary>
    /// Plays the writes through the chip, as an AdLib card's OPL2, and gives the sound: 16-bit
    /// samples, one channel.
    /// </summary>
    /// <param name="writes">Each register written, its value, and when in seconds, in order.</param>
    /// <param name="sampleRate">The samples a second.</param>
    /// <param name="tail">How long to go on after the last write, for the last notes to die away.</param>
    public static short[] Render(IEnumerable<(double Seconds, int Register, byte Value)> writes, int sampleRate, double tail = 1.0)
    {
        ArgumentNullException.ThrowIfNull(writes);
        var chip = new DbOplChip();
        chip.Setup((uint)sampleRate);
        var samples = new List<short>();
        int[] block = new int[512];

        void Until(double seconds)
        {
            long want = (long)(seconds * sampleRate);
            while (samples.Count < want)
            {
                int count = (int)Math.Min(want - samples.Count, block.Length);
                chip.GenerateBlock2((uint)count, block);
                for (int i = 0; i < count; i++)
                {
                    samples.Add((short)Math.Clamp(block[i], short.MinValue, short.MaxValue));
                }
            }
        }

        double last = 0;
        foreach (var (seconds, register, value) in writes)
        {
            Until(seconds);
            chip.WriteReg((uint)register, value);
            last = seconds;
        }

        Until(last + tail);
        return [.. samples];
    }
}

using Legendary.Core.Sound;

namespace Legendary.Core.Tests;

/// <summary>
/// Ad Lib's driver and the songs played through it, from files made here. That it plays the
/// games' songs as the games do is tested with the PC, where the games can be run.
/// </summary>
public sealed class AdLibTests
{
    private static List<(int Register, byte Value)> Driver(bool later, Action<AdLibDriver> play)
    {
        var written = new List<(int, byte)>();
        var driver = new AdLibDriver((register, value) => written.Add((register, value)), later);
        driver.Reset();
        written.Clear();
        play(driver);
        return written;
    }

    /// <summary>Middle C, by SOUND.COM's table of frequencies and by the later driver's.</summary>
    [Fact]
    public void A_note_is_a_frequency_number_and_an_octave()
    {
        Assert.Equal([(0xA0, 0x57), (0xB0, 0x31)], Driver(false, driver => driver.NoteOn(0, 60)));
        Assert.Equal([(0xA0, 0xB2), (0xB0, 0x2E)], Driver(true, driver => driver.NoteOn(0, 60)));

        // stopped: the same frequency, with the key up
        var stopped = Driver(false, driver =>
        {
            driver.NoteOn(3, 60);
            driver.NoteOff(3);
        });
        Assert.Equal([(0xA3, 0x57), (0xB3, 0x11)], stopped[2..]);
    }

    /// <summary>A volume turns down the operator that is heard, and the driver rounds it.</summary>
    [Fact]
    public void A_volume_turns_the_carrier_down()
    {
        // the piano every voice starts as: its carrier at full level, which half volume makes 32 of 63
        Assert.Equal([(0x43, 0x1F)], Driver(false, driver => driver.SetVolume(0, 64)));

        // the later driver writes both operators, and the modulator of an FM voice is as it was
        Assert.Equal([(0x40, 0x4F), (0x43, 0x1F)], Driver(true, driver => driver.SetVolume(0, 64)));
    }

    /// <summary>With drums, a drum is a bit of one register, and the tom-tom tunes the snare.</summary>
    [Fact]
    public void A_drum_is_a_bit()
    {
        var written = Driver(false, driver =>
        {
            driver.SetDrums(true);
        });
        Assert.Equal((0xBD, 0x20), written[^1]);

        var tom = Driver(false, driver =>
        {
            driver.SetDrums(true);
            driver.NoteOn(8, 48);
            driver.NoteOff(8);
        });

        // the tom-tom's note, the snare's seven half tones above, and the tom-tom's bit
        Assert.Equal([(0xA8, 0x57), (0xB8, 0x0D), (0xA7, 0x03), (0xB7, 0x0E), (0xBD, 0x24), (0xBD, 0x20)], tom[^6..]);
    }

    /// <summary>A bend of a whole half tone up is the next note.</summary>
    [Fact]
    public void A_bend_moves_a_note()
    {
        var bent = Driver(false, driver =>
        {
            driver.NoteOn(0, 60);
            driver.SetPitch(0, 0x3FFF);
        });
        Assert.Equal((0xA0, 0x6B), bent[^2]);
        var next = Driver(false, driver => driver.NoteOn(0, 61));
        Assert.Equal(0x6C, next[0].Value);
    }

    /// <summary>
    /// A song of the later kind made here: no drums, an instrument, and a note half a second
    /// long at 120 beats a minute.
    /// </summary>
    [Fact]
    public void Plays_a_MIDI_song_made_here()
    {
        byte[] timbre = new byte[AdLibDriver.TimbreSize];
        timbre[3] = 15;
        timbre[13 + 3] = 15;
        timbre[13 + 8] = 0;
        byte[] track =
        [
            0, 0xFF, 0x7F, 6, 0, 0, 0x3F, 0, 2, 0,
            0, 0xFF, 0x7F, 34, 0, 0, 0x3F, 0, 1, 0, .. timbre,
            0, 0xFF, 0x51, 3, 0x07, 0xA1, 0x20,
            0, 0x90, 60, 127,
            96, 0x80, 60, 0,
            0, 0xFF, 0x2F, 0,
        ];
        byte[] file = [.. "MThd"u8, 0, 0, 0, 6, 0, 0, 0, 1, 0, 96, .. "MTrk"u8, 0, 0, 0, (byte)track.Length, .. track];

        var writes = AdLibSong.PlayMidi(file);
        var on = writes.Single(write => write.Register == 0xB0 && (write.Value & 0x20) != 0);
        Assert.Equal((0.0, (byte)0x2E), (on.Seconds, on.Value));
        var off = writes.Last(write => write.Register == 0xB0);
        Assert.Equal((0.5, (byte)0x0E), (off.Seconds, off.Value));

        // the note's velocity is the voice's volume: full, on the instrument's carrier
        Assert.Equal(0x00, writes.Last(write => write.Register == 0x43).Value);
        Assert.Throws<InvalidDataException>(() => AdLibSong.PlayMidi(new byte[30]));
    }

    /// <summary>A bank of one instrument, made here.</summary>
    [Fact]
    public void Reads_a_bank_made_here()
    {
        byte[] file = new byte[28 + 12 + 30];
        "ADLIB-"u8.CopyTo(file.AsSpan(2));
        file[8] = 1;
        file[10] = 1;
        file[12] = 28;
        file[16] = 40;
        file[28 + 2] = 1;
        "Piano1"u8.CopyTo(file.AsSpan(28 + 3));
        file[40 + 2 + 3] = 15;

        var bank = new InstrumentBank(file);
        Assert.Equal(1, bank.Count);
        Assert.Equal(15, bank.Timbre("PIANO1")![3]);
        Assert.Null(bank.Timbre("organ"));
        Assert.Throws<InvalidDataException>(() => new InstrumentBank(new byte[40]));
    }
}

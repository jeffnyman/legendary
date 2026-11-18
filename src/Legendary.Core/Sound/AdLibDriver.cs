namespace Legendary.Core.Sound;

/// <summary>
/// Ad Lib's own way of playing its card, as its programmer's manual gave it in <c>ADLIB.C</c>
/// and as its driver SOUND.COM does it: eleven voices, six of two operators and five drums,
/// or nine of two operators; an instrument as 28 numbers; a volume and a bend of pitch for each
/// voice. What it makes is writes to the FM chip's registers.
/// </summary>
/// <param name="write">Called with each register and the value written to it.</param>
/// <param name="later">
/// Whether it is the driver the games from <i>Timequest</i> have in them, which is Ad Lib's
/// with another table of frequencies, and not SOUND.COM, which <i>Spellcasting 101</i> uses.
/// </param>
public sealed class AdLibDriver(Action<int, byte> write, bool later = false)
{
    /// <summary>The first of the drums, when there are drums: the bass drum.</summary>
    public const int BassDrum = 6;

    /// <summary>How many numbers an instrument is: thirteen for each operator, and a wave for each.</summary>
    public const int TimbreSize = 28;

    private const int SnareDrum = 7, TomTom = 8;
    private const int MaxVolume = 127, MidPitch = 0x2000, StepsPerHalfTone = 25;
    private const int Ksl = 0, Multiple = 1, Feedback = 2, Attack = 3, Sustain = 4, Sustaining = 5, Decay = 6,
        Release = 7, Level = 8, Tremolo = 9, Vibrato = 10, Ksr = 11, Fm = 12, Wave = 13;

    // the two operators of each voice, and of each drum: only the bass drum has two
    private static readonly int[][] VoiceSlots = [[0, 3], [1, 4], [2, 5], [6, 9], [7, 10], [8, 11], [12, 15], [13, 16], [14, 17]];
    private static readonly int[][] DrumSlots = [[12, 15], [16, -1], [14, -1], [17, -1], [13, -1]];
    private static readonly int[] SlotRegister = [0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 16, 17, 18, 19, 20, 21];
    private static readonly int[] SlotVoice = [0, 1, 2, 0, 1, 2, 3, 4, 5, 3, 4, 5, 6, 7, 8, 6, 7, 8];

    // with drums: which voice an operator is of, the hi-hat's 10, the cymbal's 9
    private static readonly int[] SlotVoiceWithDrums = [0, 1, 2, 0, 1, 2, 3, 4, 5, 3, 4, 5, 6, 10, 8, 6, 7, 9];
    private static readonly bool[] SlotIsCarrier =
        [false, false, false, true, true, true, false, false, false, true, true, true, false, false, false, true, true, true];

    private static readonly byte[] DrumBits = [0x10, 0x08, 0x04, 0x02, 0x01];

    // what every operator is until a song says: a piano, and the drums
    private static readonly byte[] PianoModulator = [1, 1, 3, 15, 5, 0, 1, 3, 15, 0, 0, 0, 1, 0];
    private static readonly byte[] PianoCarrier = [0, 1, 1, 15, 7, 0, 2, 4, 0, 0, 0, 1, 0, 0];
    private static readonly byte[] BassDrumModulator = [0, 0, 0, 10, 4, 0, 8, 12, 11, 0, 0, 0, 1, 0];
    private static readonly byte[] BassDrumCarrier = [0, 0, 0, 13, 4, 0, 6, 15, 0, 0, 0, 0, 1, 0];
    private static readonly byte[] SnareDrumOperator = [0, 12, 0, 15, 11, 0, 8, 5, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] TomTomOperator = [0, 4, 0, 15, 11, 0, 7, 5, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] CymbalOperator = [0, 1, 0, 15, 11, 0, 5, 5, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] HiHatOperator = [0, 1, 0, 15, 11, 0, 7, 5, 0, 0, 0, 0, 0, 0];

    // a note's frequency number for each twenty-fifth of a half tone above it
    private static readonly ushort[][] FrequencyNumbers = Frequencies();

    // the later driver's: the twelve notes from G, an octave's worth, with the octave apart.
    // They are the notes of a keyboard tuned to an A of 440 Hz, to the nearest number
    private static readonly ushort[] LaterNumbers = [517, 547, 580, 615, 651, 690, 731, 774, 820, 869, 921, 975];

    private readonly byte[][] _slot = [.. Enumerable.Range(0, 18).Select(_ => new byte[14])];
    private readonly int[] _slotVolume = new int[18];
    private readonly int[] _note = new int[11];
    private readonly bool[] _keyOn = new bool[11];
    private readonly int[] _halfTones = new int[11];
    private readonly int[] _step = new int[11];
    private readonly double[] _bend = new double[11];
    private bool _drums, _deepTremolo, _deepVibrato, _noteSelect, _waves;
    private int _drumBits, _pitchRange = 1;

    /// <summary>Everything as the driver starts it: no drums, pianos, full volume, no bend.</summary>
    public void Reset()
    {
        Array.Fill(_slotVolume, MaxVolume);
        Array.Clear(_halfTones);
        Array.Clear(_step);
        Array.Clear(_bend);
        _deepTremolo = _deepVibrato = _noteSelect = false;
        SetDrums(false);
        _pitchRange = 1;
        SetWaves(true);
    }

    /// <summary>Whether voices 6 to 10 are the five drums, or 6 to 8 are three voices more.</summary>
    public void SetDrums(bool drums)
    {
        if (drums)
        {
            Silence(BassDrum);
            Silence(SnareDrum);
            Silence(TomTom);
            SetFrequency(TomTom, 24, false);
            SetFrequency(SnareDrum, 31, false);
        }

        _drums = drums;
        _drumBits = 0;
        for (int slot = 0; slot < 18; slot++)
        {
            SetSlot(slot, SlotIsCarrier[slot] ? PianoCarrier : PianoModulator, 0);
        }

        if (drums)
        {
            SetSlot(12, BassDrumModulator, 0);
            SetSlot(15, BassDrumCarrier, 0);
            SetSlot(16, SnareDrumOperator, 0);
            SetSlot(14, TomTomOperator, 0);
            SetSlot(17, CymbalOperator, 0);
            SetSlot(13, HiHatOperator, 0);
        }

        WriteRhythm();
    }

    /// <summary>Whether an instrument's waves are used, or every operator is a sine.</summary>
    public void SetWaves(bool waves)
    {
        _waves = waves;
        write(0x01, (byte)(waves ? 0x20 : 0));
        for (int slot = 0; slot < 18; slot++)
        {
            WriteWave(slot);
        }
    }

    /// <summary>How many half tones the furthest bend is, from 1 to 12.</summary>
    public void SetPitchRange(int halfTones) => _pitchRange = Math.Clamp(halfTones, 1, 12);

    /// <summary>The depth of the tremolo and of the vibrato, and how the keyboard is split.</summary>
    public void SetGlobal(bool deepTremolo, bool deepVibrato, bool noteSelect)
    {
        _deepTremolo = deepTremolo;
        _deepVibrato = deepVibrato;
        _noteSelect = noteSelect;
        WriteRhythm();
        write(0x08, (byte)(_noteSelect ? 0x40 : 0));
    }

    /// <summary>Gives a voice its instrument: thirteen numbers for each operator, and a wave for each.</summary>
    public void SetTimbre(int voice, ReadOnlySpan<byte> timbre)
    {
        if (!Has(voice))
        {
            return;
        }

        int[] slots = _drums && voice >= BassDrum ? DrumSlots[voice - BassDrum] : VoiceSlots[voice];
        SetSlot(slots[0], timbre[..13], timbre[26]);
        if (slots[1] >= 0)
        {
            SetSlot(slots[1], timbre[13..26], timbre[27]);
        }
    }

    /// <summary>A voice's volume, from 0 to 127.</summary>
    public void SetVolume(int voice, int volume)
    {
        if (!Has(voice))
        {
            return;
        }

        volume = Math.Min(volume, MaxVolume);
        bool drum = _drums && voice >= BassDrum;
        if (later && (!drum || voice == BassDrum))
        {
            // the later driver turns both operators down, so that an instrument whose two
            // operators are both heard is quieter in both
            int first = drum ? DrumSlots[0][0] : VoiceSlots[voice][0];
            _slotVolume[first] = volume;
            WriteLevel(first);
        }

        int slot = drum ? DrumSlots[voice - BassDrum][voice == BassDrum ? 1 : 0] : VoiceSlots[voice][1];
        _slotVolume[slot] = volume;
        WriteLevel(slot);
    }

    /// <summary>A voice's bend, from 0 to 3FFF, with 2000 none.</summary>
    public void SetPitch(int voice, int bend)
    {
        if (voice > TomTom || !Has(voice))
        {
            return;
        }

        _bend[voice] = (Math.Min(bend, 0x3FFF) - MidPitch) * (double)_pitchRange / MidPitch;
        int steps = (Math.Min(bend, 0x3FFF) - MidPitch) * _pitchRange * StepsPerHalfTone / MidPitch;
        if (steps < 0)
        {
            int down = StepsPerHalfTone - 1 - steps;
            _halfTones[voice] = -(down / StepsPerHalfTone);
            int part = (down - StepsPerHalfTone + 1) % StepsPerHalfTone;
            _step[voice] = part == 0 ? 0 : StepsPerHalfTone - part;
        }
        else
        {
            _halfTones[voice] = steps / StepsPerHalfTone;
            _step[voice] = steps % StepsPerHalfTone;
        }

        // SOUND.COM keeps a drum's bend, where ADLIB.C leaves a drum alone, and it is heard
        // when the drum's frequency is next set: a song tunes its snare drum by it
        if (!_drums || voice <= BassDrum)
        {
            SetFrequency(voice, _note[voice], _keyOn[voice]);
        }
    }

    /// <summary>Starts a note on a voice: 60 is middle C.</summary>
    public void NoteOn(int voice, int note)
    {
        if (!Has(voice))
        {
            return;
        }

        note = Math.Max(note - 12, 0);
        if (!_drums || voice < BassDrum)
        {
            SetFrequency(voice, note, true);
            return;
        }

        if (voice == BassDrum)
        {
            SetFrequency(BassDrum, note, false);
        }
        else if (voice == TomTom)
        {
            // the snare drum is tuned from the tom-tom, seven half tones above
            SetFrequency(TomTom, note, false);
            SetFrequency(SnareDrum, note + 7, false);
        }

        _drumBits |= DrumBits[voice - BassDrum];
        WriteRhythm();
    }

    /// <summary>Stops a voice's note.</summary>
    public void NoteOff(int voice)
    {
        if (!Has(voice))
        {
            return;
        }

        if (!_drums || voice < BassDrum)
        {
            SetFrequency(voice, _note[voice], false);
            return;
        }

        _drumBits &= ~DrumBits[voice - BassDrum];
        WriteRhythm();
    }

    // there are eleven voices with the drums, and nine without: a song may have more
    private bool Has(int voice) => voice >= 0 && voice < (_drums ? 11 : 9);

    private void Silence(int voice)
    {
        write(0xA0 + voice, 0);
        write(0xB0 + voice, 0);
    }

    private void SetFrequency(int voice, int note, bool keyOn)
    {
        _keyOn[voice] = keyOn;
        _note[voice] = note;
        int number, octave;
        if (later)
        {
            // from G: the notes below the first are its octave's, halved. A bend is taken
            // as the part of a half tone it is, which is not from the driver's own code
            double bent = Math.Clamp(note, 0, 95) + _bend[voice];
            int whole = (int)Math.Floor(bent), from = Math.Clamp(whole, 0, 95) - 7;
            number = (int)Math.Round(LaterNumbers[((from % 12) + 12) % 12] * Math.Pow(2, (bent - whole) / 12));
            octave = from < 0 ? 0 : from / 12;
            number = from < 0 ? number >> 1 : number;
            if (number > 1023)
            {
                number >>= 1;
                octave++;
            }
        }
        else
        {
            note = Math.Clamp(note + _halfTones[voice], 0, 95);
            number = FrequencyNumbers[_step[voice]][note % 12];
            octave = note / 12;
        }

        write(0xA0 + voice, (byte)number);
        write(0xB0 + voice, (byte)((keyOn ? 0x20 : 0) + (octave << 2) + ((number >> 8) & 3)));
    }

    private void SetSlot(int slot, ReadOnlySpan<byte> numbers, int wave)
    {
        numbers[..13].CopyTo(_slot[slot]);
        _slot[slot][Wave] = (byte)(wave & 3);
        WriteRhythm();
        write(0x08, (byte)(_noteSelect ? 0x40 : 0));
        WriteLevel(slot);
        if (!SlotIsCarrier[slot])
        {
            write(0xC0 + SlotVoice[slot], (byte)((_slot[slot][Feedback] << 1) | (_slot[slot][Fm] != 0 ? 0 : 1)));
        }

        byte[] of = _slot[slot];
        int at = SlotRegister[slot];
        write(0x60 + at, (byte)((of[Attack] << 4) | (of[Decay] & 0xF)));
        write(0x80 + at, (byte)((of[Sustain] << 4) | (of[Release] & 0xF)));
        write(0x20 + at, (byte)((of[Tremolo] != 0 ? 0x80 : 0) | (of[Vibrato] != 0 ? 0x40 : 0)
            | (of[Sustaining] != 0 ? 0x20 : 0) | (of[Ksr] != 0 ? 0x10 : 0) | (of[Multiple] & 0xF)));
        WriteWave(slot);
    }

    // an operator's level: the instrument's, turned down by the voice's volume where the
    // operator is one that is heard and not one that shapes another
    private void WriteLevel(int slot)
    {
        int level = 63 - (_slot[slot][Level] & 0x3F);
        bool alone = _drums && SlotVoiceWithDrums[slot] > BassDrum;
        if (SlotIsCarrier[slot] || _slot[slot][Fm] == 0 || alone)
        {
            level = ((level * _slotVolume[slot]) + ((MaxVolume + 1) / 2)) >> 7;
        }

        write(0x40 + SlotRegister[slot], (byte)((63 - level) | (_slot[slot][Ksl] << 6)));
    }

    private void WriteWave(int slot) => write(0xE0 + SlotRegister[slot], (byte)(_waves ? _slot[slot][Wave] : 0));

    private void WriteRhythm() => write(0xBD, (byte)((_deepTremolo ? 0x80 : 0) | (_deepVibrato ? 0x40 : 0) | (_drums ? 0x20 : 0) | _drumBits));

    private static ushort[][] Frequencies()
    {
        var table = new ushort[StepsPerHalfTone][];
        for (int step = 0; step < StepsPerHalfTone; step++)
        {
            // the lowest C, 260.4 Hz, raised by so many twenty-fifths of a half tone, and each
            // note after it 6% above the last. These make the table SOUND.COM has in memory, to
            // the number
            long hertz = (10000 + (6L * step * 4)) * (26040L * 8) / (10000 * 25);
            long number = hertz * 4096 * 9 / (179L * 625);
            table[step] = new ushort[12];
            for (int note = 0; note < 12; note++)
            {
                table[step][note] = (ushort)((4 + number) >> 3);
                number = number * 106 / 100;
            }
        }

        return table;
    }
}

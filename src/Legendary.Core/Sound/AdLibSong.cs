using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Sound;

/// <summary>A register of the FM chip written, and when.</summary>
/// <param name="Seconds">How far into the song.</param>
/// <param name="Register">The register.</param>
/// <param name="Value">What is written to it.</param>
public readonly record struct FmWrite(double Seconds, int Register, byte Value);

/// <summary>
/// The instruments of an Ad Lib .BNK file, by name: <i>Spellcasting 101</i>'s S101.BNK. The
/// file has 28 bytes, a list of names of 12 bytes each, and the instruments, 30 bytes each:
/// two bytes a .ROL song doesn't use, and the 28 numbers of <see cref="AdLibDriver.SetTimbre"/>.
/// </summary>
public sealed class InstrumentBank
{
    private readonly Dictionary<string, byte[]> _timbres = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a bank from its bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't a bank.</exception>
    public InstrumentBank(ReadOnlySpan<byte> file)
    {
        if (file.Length < 28 || !file.Slice(2, 6).SequenceEqual("ADLIB-"u8))
        {
            throw new InvalidDataException("Not a bank of instruments: it doesn't start as one does.");
        }

        int used = BinaryPrimitives.ReadUInt16LittleEndian(file[8..]);
        int names = BinaryPrimitives.ReadInt32LittleEndian(file[12..]), data = BinaryPrimitives.ReadInt32LittleEndian(file[16..]);
        for (int entry = 0; entry < used; entry++)
        {
            var name = file.Slice(names + (12 * entry), 12);
            int at = data + (30 * BinaryPrimitives.ReadUInt16LittleEndian(name));
            if (name[2] == 0 || at + 30 > file.Length)
            {
                continue;
            }

            _timbres[AdLibSong.Name(name[3..])] = file.Slice(at + 2, AdLibDriver.TimbreSize).ToArray();
        }
    }

    /// <summary>How many instruments it has.</summary>
    public int Count => _timbres.Count;

    /// <summary>An instrument's 28 numbers, or null if the bank has none of that name.</summary>
    public byte[]? Timbre(string name) => _timbres.GetValueOrDefault(name);
}

/// <summary>
/// A song for the AdLib card, turned into what is written to the FM chip and when, by playing
/// it through <see cref="AdLibDriver"/> as the game does.
/// </summary>
public static class AdLibSong
{
    /// <summary>
    /// Plays an Ad Lib Visual Composer .ROL file, with the instruments of a bank.
    /// </summary>
    /// <remarks>
    /// The file has a tempo, with the times it changes, and eleven voices. Each voice is four
    /// lists: its notes, each with how long it lasts, and the times its instrument, its volume
    /// and its bend of pitch change. Times are in ticks, so many to a beat.
    /// </remarks>
    /// <exception cref="InvalidDataException">The bytes aren't a .ROL file.</exception>
    public static IReadOnlyList<FmWrite> PlayRol(ReadOnlySpan<byte> file, InstrumentBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (file.Length < 0xC9 || BinaryPrimitives.ReadUInt16LittleEndian(file[2..]) != 4)
        {
            throw new InvalidDataException("Not a .ROL file: it doesn't start as one does.");
        }

        int ticksPerBeat = BinaryPrimitives.ReadUInt16LittleEndian(file[44..]);
        bool drums = file[53] == 0;
        int at = 0xB6 + 15;
        float basicTempo = BinaryPrimitives.ReadSingleLittleEndian(file[at..]);
        var tempos = Timed(file, ref at, 4, 4, (span, _) => (double)BinaryPrimitives.ReadSingleLittleEndian(span));

        // what happens at each tick, in the order it is done: for each voice, its instrument,
        // its volume, its bend, and its note
        var events = new SortedDictionary<int, List<Action<AdLibDriver>>>();
        void At(int tick, Action<AdLibDriver> what)
        {
            if (!events.TryGetValue(tick, out var list))
            {
                events[tick] = list = [];
            }

            list.Add(what);
        }

        int length = 0;
        var voices = new List<Action>();
        for (int voice = 0; voice < 11 && at + 17 <= file.Length; voice++)
        {
            int v = voice;
            at += 15;
            int ticks = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
            at += 2;
            var notes = new List<(int Tick, int Note)>();
            for (int tick = 0; tick < ticks; at += 4)
            {
                notes.Add((tick, BinaryPrimitives.ReadUInt16LittleEndian(file[at..])));
                tick += BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 2)..]);
            }

            length = Math.Max(length, ticks);
            at += 15;
            var timbres = Timed(file, ref at, 0, 12, (span, _) => Name(span[..9]));
            at += 15;
            var volumes = Timed(file, ref at, 0, 4, (span, _) => (double)BinaryPrimitives.ReadSingleLittleEndian(span));
            at += 15;
            var bends = Timed(file, ref at, 0, 4, (span, _) => (double)BinaryPrimitives.ReadSingleLittleEndian(span));
            voices.Add(() =>
            {
                foreach (var (tick, name) in timbres)
                {
                    if (bank.Timbre(name) is { } timbre)
                    {
                        At(tick, driver => driver.SetTimbre(v, timbre));
                    }
                }

                foreach (var (tick, volume) in volumes)
                {
                    At(tick, driver => driver.SetVolume(v, Math.Min((int)(volume * 128), 127)));
                }

                foreach (var (tick, bend) in bends)
                {
                    At(tick, driver => driver.SetPitch(v, (int)(bend * 0x1FFF)));
                }

                // a note is stopped as the next starts, and a note of 0 is a rest
                bool sounding = false;
                foreach (var (tick, note) in notes.Append((ticks, 0)))
                {
                    bool stop = sounding;
                    sounding = note != 0;
                    At(tick, driver =>
                    {
                        if (stop)
                        {
                            driver.NoteOff(v);
                        }

                        if (note != 0)
                        {
                            driver.NoteOn(v, note);
                        }
                    });
                }
            });
        }

        // the game does the last voice first
        for (int voice = voices.Count - 1; voice >= 0; voice--)
        {
            voices[voice]();
        }

        var written = new List<FmWrite>();
        double seconds = 0;
        var player = new AdLibDriver((register, value) => written.Add(new FmWrite(seconds, register, value)));
        player.Reset();
        player.SetDrums(drums);
        int last = 0, tempo = 0;
        double multiplier = 1;
        foreach (var (tick, list) in events)
        {
            // the time from the last tick anything happened at, by the tempo as it changed
            for (; last < tick; last++)
            {
                while (tempo < tempos.Count && tempos[tempo].Tick <= last)
                {
                    multiplier = tempos[tempo++].Value;
                }

                seconds += 60.0 / (basicTempo * multiplier * ticksPerBeat);
            }

            foreach (var what in list)
            {
                what(player);
            }
        }

        return written;
    }

    /// <summary>
    /// Plays a MIDI file of the kind Ad Lib called .MDI: one track, a voice of the card to a
    /// channel, with the instruments in it.
    /// </summary>
    /// <remarks>
    /// A note's velocity is its voice's volume, a channel's pressure changes the volume, and a
    /// pitch wheel bends the voice. What is the card's own comes as events of the sequencer's
    /// that start <c>00 00 3F 00</c>: 1, a voice and its instrument's 28 numbers; 2, whether
    /// there are drums; 3, how far a bend goes, in half tones.
    /// </remarks>
    /// <exception cref="InvalidDataException">The bytes aren't such a file.</exception>
    public static IReadOnlyList<FmWrite> PlayMidi(ReadOnlySpan<byte> file)
    {
        if (file.Length < 22 || !file[..4].SequenceEqual("MThd"u8) || !file.Slice(14, 4).SequenceEqual("MTrk"u8))
        {
            throw new InvalidDataException("Not a MIDI file: it doesn't start as one does.");
        }

        int division = BinaryPrimitives.ReadUInt16BigEndian(file[12..]);
        int end = Math.Min(file.Length, 22 + BinaryPrimitives.ReadInt32BigEndian(file[18..]));
        var written = new List<FmWrite>();
        double seconds = 0, perTick = 0.5 / division;
        var player = new AdLibDriver((register, value) => written.Add(new FmWrite(seconds, register, value)), later: true);
        player.Reset();

        int[] volume = new int[11];

        int at = 22, status = 0;

        int Length(ReadOnlySpan<byte> from)
        {
            int value = 0;
            while (at < end)
            {
                byte next = from[at++];
                value = (value << 7) | (next & 0x7F);
                if (next < 0x80)
                {
                    break;
                }
            }

            return value;
        }

        while (at < end)
        {
            seconds += Length(file) * perTick;
            if (at >= end)
            {
                break;
            }

            if (file[at] == 0xFF)
            {
                int kind = file[at + 1];
                at += 2;
                int length = Length(file);
                var body = file.Slice(at, Math.Min(length, end - at));
                at += length;
                if (kind == 0x2F)
                {
                    break;
                }

                if (kind == 0x51 && body.Length >= 3)
                {
                    perTick = ((body[0] << 16) | (body[1] << 8) | body[2]) / 1e6 / division;
                }
                else if (kind == 0x7F && body.Length >= 6 && body[..4].SequenceEqual<byte>([0, 0, 0x3F, 0]))
                {
                    switch (body[4])
                    {
                        case 1 when body.Length >= 6 + AdLibDriver.TimbreSize && body[5] < 11:
                            player.SetTimbre(body[5], body.Slice(6, AdLibDriver.TimbreSize));
                            break;
                        case 2:
                            player.SetDrums(body[5] != 0);

                            // a voice is silent until its first note says how loud it is
                            for (int silent = 0; silent < 9; silent++)
                            {
                                player.SetVolume(silent, 0);
                            }

                            break;
                        case 3:
                            player.SetPitchRange(body[5]);
                            break;
                    }
                }

                continue;
            }

            if (file[at] is 0xF0 or 0xF7)
            {
                at++;
                at += Length(file);
                continue;
            }

            if (file[at] >= 0x80)
            {
                status = file[at++];
            }

            int voice = status & 0xF, first = file[at++], second = (status >> 4) is 0xC or 0xD ? 0 : file[at++];
            if (voice >= 11)
            {
                continue;
            }

            switch (status >> 4)
            {
                case 0x9 when second != 0:
                    if (volume[voice] != second)
                    {
                        player.SetVolume(voice, volume[voice] = second);
                    }

                    player.NoteOn(voice, first);
                    break;
                case 0x8 or 0x9:
                    player.NoteOff(voice);
                    break;
                case 0xD:
                    player.SetVolume(voice, volume[voice] = first);
                    break;
                case 0xE:
                    player.SetPitch(voice, (second << 7) | first);
                    break;
            }
        }

        return written;
    }

    /// <summary>A name as the files keep it: letters, up to a zero.</summary>
    internal static string Name(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private delegate T Reader<T>(ReadOnlySpan<byte> span, int tick);

    // a list of a .ROL file: after some bytes, a count, and so many of a tick and a value
    private static List<(int Tick, T Value)> Timed<T>(ReadOnlySpan<byte> file, ref int at, int before, int size, Reader<T> read)
    {
        at += before;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
        at += 2;
        var list = new List<(int, T)>(count);
        for (int index = 0; index < count; index++, at += 2 + size)
        {
            int tick = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
            list.Add((tick, read(file.Slice(at + 2, size), tick)));
        }

        return list;
    }
}

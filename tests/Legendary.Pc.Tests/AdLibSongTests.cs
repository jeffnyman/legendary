using Legendary.Core.Sound;

namespace Legendary.Pc.Tests;

/// <summary>
/// The songs for the AdLib card, played by <see cref="AdLibSong"/>, against the games playing
/// them: what each writes to the FM chip. Without a game (in LEGENDARY_GAMES, or in legend
/// above the tests), its test passes without checking.
/// </summary>
public sealed class AdLibSongTests : IDisposable
{
    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-songs").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static bool StartsNote(int register, byte value) =>
        (register is >= 0xB0 and <= 0xB8 && (value & 0x20) != 0) || (register == 0xBD && (value & 0x1F) != 0);

    // what the game writes to the chip in its first so many seconds
    private List<FmWrite> Played(string folder, double seconds, string program, string? driver = null)
    {
        // the clock from midnight, so that every run is the same
        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000, TickCount = 0 };
        var written = new List<FmWrite>();
        machine.FmWritten = (register, value, when) => written.Add(new FmWrite(when, register, value));
        if (driver is null)
        {
            machine.Load(program, "");
        }
        else
        {
            machine.LoadCommands(driver, program);
        }

        var buffer = new short[16384];
        for (double t = 0; t < seconds && !machine.Exited; t += 0.05)
        {
            machine.Run(machine.InstructionsPerSecond / 20);
            while (machine.ReadSamples(buffer) > 0)
            {
            }
        }

        return written;
    }

    // each register's values in turn, from the first note, with what it held before it
    private static Dictionary<int, List<byte>> InTurn(IReadOnlyList<FmWrite> writes)
    {
        int first = Enumerable.Range(0, writes.Count).First(index => StartsNote(writes[index].Register, writes[index].Value));
        var held = new Dictionary<int, byte>();
        var turns = new Dictionary<int, List<byte>>();
        for (int index = 0; index < writes.Count; index++)
        {
            var (_, register, value) = writes[index];
            if (index < first)
            {
                held[register] = value;
                continue;
            }

            if (!turns.TryGetValue(register, out var values))
            {
                turns[register] = values = [held.GetValueOrDefault(register)];
            }

            if (values[^1] != value)
            {
                values.Add(value);
            }
        }

        return turns;
    }

    // the registers of the voices: not the timers', the test register, or the keyboard split
    private static bool OfAVoice(int register) => register is >= 0x20 and not 0xBD;

    // a file of the game's folder, its case aside
    private static string? FileIn(string folder, string name) =>
        Directory.EnumerateFiles(folder).FirstOrDefault(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string? SongOf(string folder, string file, int number, out byte[] song)
    {
        song = [];
        if (FileIn(folder, file) is not { } path)
        {
            return null;
        }

        song = new MusicFile(File.ReadAllBytes(path)).Songs.First(found => found.Number == number).Data.ToArray();
        return path;
    }

    /// <summary>
    /// <i>Timequest</i>'s title, a song of the later kind with no drums: every register of a
    /// voice takes the values the game gives it, in the game's order, for as long as the game
    /// is run.
    /// </summary>
    [Fact]
    public void Timequests_title_is_played_as_the_game_plays_it()
    {
        if (TestData.Game("tquest/game") is not { } folder || SongOf(folder, "time_m9.mus", 1, out byte[] song) is null)
        {
            return;
        }

        var game = InTurn(Played(folder, 25, "TQ.EXE"));
        var player = InTurn(AdLibSong.PlayMidi(song));
        Assert.True(game.Values.Sum(values => values.Count) > 1000);
        foreach (var (register, values) in game.Where(pair => pair.Key >= 0x20))
        {
            Assert.True(player.ContainsKey(register), $"register {register:X2} is never written");
            Assert.Equal(values, player[register].Take(values.Count));
        }
    }

    /// <summary>
    /// <i>Gateway</i>'s title, a song of the later kind with drums.
    /// </summary>
    [Fact]
    public void Gateways_title_is_played_as_the_game_plays_it()
    {
        if (TestData.Game("gateway/game") is not { } folder || SongOf(folder, "gate_004.mus", 1, out byte[] song) is null)
        {
            return;
        }

        var game = InTurn(Played(folder, 30, "GATE.EXE"));
        var player = InTurn(AdLibSong.PlayMidi(song));
        Assert.True(game.Values.Sum(values => values.Count) > 500);
        Assert.Contains(0xBD, game.Keys);
        foreach (var (register, values) in game.Where(pair => pair.Key >= 0x20))
        {
            Assert.True(player.ContainsKey(register), $"register {register:X2} is never written");
            Assert.Equal(values, player[register].Take(values.Count));
        }
    }

    /// <summary>
    /// <i>Spellcasting 101</i>'s title, a .ROL song with drums, played by SOUND.COM with the
    /// instruments of S101.BNK. The game does the voices of one tick in an order of its own,
    /// so the drums' register, which they share, is compared between ticks: at the middle of
    /// each, the chip holds what the game's holds. The game's ticks are half a percent longer
    /// than the file's tempo makes them.
    /// </summary>
    [Fact]
    public void Spellcasting_101s_title_is_played_as_the_game_plays_it()
    {
        if (TestData.Game("sc101/game") is not { } folder || SongOf(folder, "s101_m1.mus", 1, out byte[] song) is null
            || FileIn(folder, "s101.bnk") is not { } bank)
        {
            return;
        }

        var game = Played(folder, 30, "S101.EXE", "SOUND.COM");
        var player = AdLibSong.PlayRol(song, new InstrumentBank(File.ReadAllBytes(bank)));

        // every voice's own registers, in turn
        var gameTurns = InTurn(game);
        var playerTurns = InTurn(player);
        Assert.True(gameTurns.Values.Sum(values => values.Count) > 400);
        foreach (var (register, values) in gameTurns.Where(pair => OfAVoice(pair.Key)))
        {
            Assert.Equal(values, playerTurns[register].Take(values.Count));
        }

        // and the drums, at the middle of each tick: 115 beats a minute, 8 ticks to a beat
        const double Tick = 60.0 / (115 * 8), Slower = 1.00523;
        static List<int> Drums(IReadOnlyList<FmWrite> writes, double tick, int ticks)
        {
            int first = Enumerable.Range(0, writes.Count).First(index => StartsNote(writes[index].Register, writes[index].Value));
            double start = writes[first].Seconds;
            var at = new List<int>();
            int next = 0, held = 0;
            for (int number = 0; number < ticks; number++)
            {
                for (; next < writes.Count && writes[next].Seconds - start <= (number + 0.5) * tick; next++)
                {
                    held = writes[next].Register == 0xBD ? writes[next].Value : held;
                }

                at.Add(held);
            }

            return at;
        }

        int ticks = (int)((game[^1].Seconds - game.First(write => StartsNote(write.Register, write.Value)).Seconds) / (Tick * Slower)) - 1;
        Assert.True(ticks > 300);
        Assert.Equal(Drums(game, Tick * Slower, ticks), Drums(player, Tick, ticks));
    }
}

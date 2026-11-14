using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on a game's world, with <i>Spellcasting 101</i> played under it. Without the game
/// (in LEGENDARY_GAMES, or legend/sc101/game above the tests), its test passes without
/// checking.
/// </summary>
public sealed class WorldWatchTests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "sc101", "game")) ? Path.Combine(root, "sc101", "game") : null;

        if (Environment.GetEnvironmentVariable("LEGENDARY_GAMES") is { Length: > 0 } given && Has(given) is { } found)
        {
            return found;
        }

        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (Has(Path.Combine(folder.FullName, "legend")) is { } above)
            {
                return above;
            }
        }

        return null;
    }

    private static byte Scan(char key) => key switch
    {
        ' ' => 0x39,
        '\n' => Enter,
        _ when Top.Contains(key) => (byte)(0x10 + Top.IndexOf(key)),
        _ when Home.Contains(key) => (byte)(0x1E + Home.IndexOf(key)),
        _ => (byte)(0x2C + Bottom.IndexOf(key)),
    };

    [Theory]
    [InlineData(0, "12:00 am")]
    [InlineData(935, "3:35 pm")]
    [InlineData(725, "12:05 pm")]
    [InlineData(59, "12:59 am")]
    [InlineData(1439, "11:59 pm")]
    public void A_time_is_said_as_the_game_says_it(int minutes, string said) => Assert.Equal(said, WorldWatch.Time(minutes));

    [Fact]
    public void Why_a_routine_is_asked_is_said_in_words()
    {
        Assert.Equal("where it happens", WorldWatch.Why(1));
        Assert.Equal("the second object", WorldWatch.Why(4));
        Assert.Equal("the object", WorldWatch.Why(6));
        Assert.Equal("15", WorldWatch.Why(15));
    }

    [Fact]
    public void It_says_the_room_and_each_turn_as_the_game_is_played()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000 };
        machine.Load("S101.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var words = Legendary.Core.Text.Vocabulary.Read(File.ReadAllBytes(Directory.EnumerateFiles(folder).First(file => Path.GetFileName(file).Equals("vocab.dat", StringComparison.OrdinalIgnoreCase))));
        var watch = new WorldWatch(machine, WorldMap.Spellcasting101, said, words);

        // a sixtieth of a second at a time, with a look after each, as the player does it
        void Run(double seconds)
        {
            for (int frame = 0; frame < seconds * 60; frame++)
            {
                machine.Run(machine.InstructionsPerSecond / 60);
                watch.Look();
            }
        }

        void Type(string text)
        {
            foreach (char key in text)
            {
                machine.PressKey(Scan(key));
                Run(0.05);
                machine.ReleaseKey(Scan(key));
                Run(0.05);
            }

            Run(4);
        }

        // the launcher's title, with nothing to say; then a key, the game, and no to restoring
        Run(6);
        Assert.Equal("", said.ToString());
        Type("\n");
        Run(8);
        Type("n");
        Type("examine rat\n");
        Type("climb chimney\n");
        Type("n\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 427 objects", lines[0], StringComparison.Ordinal);
        Assert.Equal("world: 3:35 pm, turn 0, in Bedchamber (1)", lines[1]);
        Assert.StartsWith("world:   here: rat (352), you (153) [", lines[2], StringComparison.Ordinal);
        // the rat's own routine has something to say about being examined, so the asking ends there
        Assert.Equal("world:   action 60: examine rat (352)", lines[3]);
        Assert.Equal("world:     answered by rat (352), asked as the object: 1", lines[4]);
        Assert.Equal("world: 3:40 pm, turn 1, in Bedchamber (1)", lines[5]);

        // CLIMB CHIMNEY, which the chimney's routine starts over as entering the fireplace: both
        // are performed, and both are said, in their order. The first is never answered: the
        // routine that started over didn't come back to say anything
        int climb = Array.IndexOf(lines, "world:   action 25: climb chimney (185)");
        Assert.True(climb > 5);
        Assert.Equal("world:   action 27: climb in fireplace (62)", lines[climb + 1]);
        Assert.Equal("world:     answered by fireplace (62), asked as the object: 1", lines[climb + 2]);
        Assert.Equal("world: 3:45 pm, turn 2, in Bedchamber (1)", lines[climb + 3]);

        // going north: the action, then the room as the game moves you, then the turn counted
        int go = Array.IndexOf(lines, "world:   action 197: go");
        Assert.True(go > climb);
        Assert.Equal("world: 3:45 pm, turn 2, in Potty Chamber (2)", lines[go + 1]);
        Assert.Equal("world:   here: you (153) [cloak (82)], tiny door (66)", lines[go + 2]);
        Assert.Equal("world: 3:50 pm, turn 3, in Potty Chamber (2)", lines[^2]);

        // three commands, four actions, and nothing said twice
        Assert.Equal(4, lines.Count(line => line.StartsWith("world:   action ", StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(line => line.Contains("answered by", StringComparison.Ordinal)));
    }
}

using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Spellcasting 301</i>'s world, with the game played under it: what it is,
/// what changes, each command as it is parsed, each action and each timer. Without the game (in LEGENDARY_GAMES, or
/// legend/sc301/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatch301Tests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-301").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "sc301", "game")) ? Path.Combine(root, "sc301", "game") : null;

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

    private static byte[] FileIn(string folder, string name) =>
        File.ReadAllBytes(Directory.EnumerateFiles(folder).First(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static byte Scan(char key) => key switch
    {
        ' ' => 0x39,
        '\n' => Enter,
        _ when Top.Contains(key) => (byte)(0x10 + Top.IndexOf(key)),
        _ when Home.Contains(key) => (byte)(0x1E + Home.IndexOf(key)),
        _ => (byte)(0x2C + Bottom.IndexOf(key)),
    };

    [Fact]
    public void It_says_the_room_and_what_changes_as_the_game_is_played()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000 };
        machine.Load("S301.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.Spellcasting301, said,
            Legendary.Core.Text.Vocabulary.Read(FileIn(folder, "vocab.dat")), FileIn(folder, "object.dat"));

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

        Run(5);
        for (int key = 0; key < 12; key++)
        {
            Type("\n");
        }

        // the title and the story so far take what's typed until the game is ready for a command
        Type("inventory\n");
        Type("wait\n");
        Type("\n");

        Type("take off glasses then drop them\n");
        Type("take all\n");
        Type("north\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 517 objects", lines[0], StringComparison.Ordinal);

        // as it starts: the names from OBJECT.DAT, the flags by the game's table
        Assert.Equal("world: 1:10 pm, turn 0, in Beach (1)", lines[1]);
        Assert.Equal("world:   here: spell book (368) [BLERK spell (374)], you (255) [coin (86), eyeglasses (89), bathing suit (87)]", lines[2]);
        Assert.Equal("world:   exits: none", lines[3]);

        // a command as the parser takes it. The routine that gathers the words and the one
        // that takes each off the line are both in an overlay here
        int doff = Array.IndexOf(lines, "world:   words: take [verb] off [preposition] glasses [noun] then");
        Assert.True(doff > 0);
        Assert.Equal(
            [
                "world:   form 377: take off OBJECT, which is action 57",
                "world:   object \"glasses\", looked for held: eyeglasses (89)",
                "world:   action 57: doff eyeglasses (89)",
                "world:     answered by eyeglasses (89), asked as the object: 1",
            ],
            lines[(doff + 1)..(doff + 5)]);
        Assert.Contains("world:   object \"them\", looked for held: eyeglasses (89)", lines);
        Assert.Contains("world: 1:15 pm, turn 1, in Beach (1)", lines);

        // the timers are numbers, named by them: the one the game starts with runs, and puts
        // itself back to run every turn
        Assert.Contains("world:   timer 10 (the world's most beautiful woman passes) started: 1 turn to go", lines);
        int runs = Array.IndexOf(lines, "world:   timer 10 (the world's most beautiful woman passes) runs");
        Assert.True(runs > 0);
        Assert.Equal("world:   timer 10 (the world's most beautiful woman passes) started: every turn", lines[runs + 1]);
        Assert.Contains("world:   the world's most beautiful woman (256) moved: from nowhere to Beach (1)", lines);

        // taking: the thing moves, and is marked touched
        Assert.Contains("world:   action 93: get spell book (368)", lines);
        Assert.Contains("world:   spell book (368) moved: from Beach (1) to you (255)", lines);
        Assert.Contains("world:   spell book (368): flag 6, touched, set", lines);
        Assert.Contains("world:   action 236: go north", lines);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(60, 60).Select(line => line.Text)];
        Assert.Equal("Beach (1)", panel[1]);
        Assert.Contains("  the world's most beautiful woman (256)", panel);
        int timers = Array.IndexOf(panel, "Timers");
        Assert.True(timers > 0);
        Assert.StartsWith("  the world's most beautiful woman passes: ", panel[timers + 1], StringComparison.Ordinal);
    }
}

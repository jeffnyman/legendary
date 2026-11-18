using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Eric the Unready</i>'s world, with the game played under it: what it is,
/// what changes, each command as it is parsed, each action and each timer. Without the game (in LEGENDARY_GAMES, or
/// legend/ericun/game/ERIC above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatchEricTests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-eric").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "ericun", "game", "ERIC")) ? Path.Combine(root, "ericun", "game", "ERIC") : null;

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
        machine.Load("ERIC.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.EricTheUnready, said,
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
        // the title and the story so far take what's typed until the game is ready for a command
        for (int key = 0; key < 44; key++)
        {
            Type("\n");
        }

        Type("look\n");
        Type("examine farmer\n");
        Type("west\n");
        Type("wait\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 537 objects", lines[0], StringComparison.Ordinal);

        // as it starts: noon, in the farmyard, with the farmer's two timers going
        Assert.Equal("world: 12:00 pm, turn 0, in Farmyard (259)", lines[1]);
        Assert.Equal("world:   exits: west to Barn (260); northeast to Privy (261) by door (277); in: routine 46 decides", lines[3]);
        Assert.Contains("world:   timer 2 (the farmer wants an answer) started: 5 turns to go", lines);
        Assert.Contains("world:   timer 1 (the farmer fires you) started: 660 turns to go", lines);

        // a command as the parser takes it, and who answers the action
        int examine = Array.IndexOf(lines, "world:   words: examine [verb] farmer [noun]");
        Assert.True(examine > 0);
        Assert.Equal(
            [
                "world:   form 88: examine OBJECT, which is action 56",
                "world:   object \"farmer\", looked for anywhere near: farmer (263)",
                "world:   action 56: examine farmer (263)",
                "world:     answered by farmer (263), asked as the object: 1",
            ],
            lines[(examine + 1)..(examine + 5)]);

        // a turn is a minute here, and the game counts both
        Assert.Contains("world: 12:01 pm, turn 1, in Farmyard (259)", lines);

        // going: you move, and the place is marked visited
        Assert.Contains("world:   action 159: go west", lines);
        Assert.Contains("world:   you (284) moved: from Farmyard (259) to Barn (260)", lines);
        Assert.Contains("world:   Barn (260): flag 5, visited, set", lines);

        // waiting is ten minutes and no turn
        Assert.Contains("world:   action 170: wait", lines);
        Assert.Contains("world: 12:03 pm, turn 3, in Barn (260)", lines);
        Assert.Contains("world: 12:13 pm, turn 3, in Barn (260)", lines);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(60, 60).Select(line => line.Text)];
        Assert.EndsWith("   turn 3", panel[0], StringComparison.Ordinal);
        Assert.Equal("Barn (260)", panel[1]);
        Assert.Contains("  east  to Farmyard (259)", panel);

        // a timer's count goes down with the clock: 13 minutes have passed in three turns
        Assert.Contains("  the farmer fires you: 647 turns to go", panel);
    }
}

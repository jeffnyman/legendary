using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Gateway</i>'s world, with the game played under it: what it is,
/// what changes, each command as it is parsed, each action and each timer. Without the game (in LEGENDARY_GAMES, or
/// legend/gateway/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatchGatewayTests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-gateway").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "gateway", "game")) ? Path.Combine(root, "gateway", "game") : null;

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
        machine.Load("GATE.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.Gateway, said,
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
        for (int key = 0; key < 14; key++)
        {
            Type("\n");
        }

        // the title and the story so far take what's typed until the game is ready for a command
        Type("inventory\n");
        Type("wait\n");
        Type("\n");
        Type("examine bed\n");
        Type("take all\n");

        Type("open door then out\n");
        Type("wait\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 734 objects", lines[0], StringComparison.Ordinal);

        // the game counts days and minutes, and no turns
        Assert.Contains("world: 12:00 pm, day 1, in Your Quarters (238)", lines);
        Assert.Contains("world:   exits: east to Heecheetown (241) by room door (427); out to Heecheetown (241) by room door (427)", lines);

        // the timers it starts with, numbered and named
        Assert.Contains("world:   timer 23 (Thom Seldridge comes into the bar) started: 480 turns to go", lines);

        // a command as the parser takes it. The call that finds a phrase's objects is in an
        // overlay here, and what it finds is in the stack's segment
        int open = Array.IndexOf(lines, "world:   words: open [verb or adjective] door [adjective or noun] then");
        Assert.True(open > 0);
        Assert.Equal(
            [
                "world:   form 205: open OBJECT, which is action 113",
                "world:   object \"door\", looked for anywhere near: room door (427)",
                "world:   action 113: open room door (427)",
            ],
            lines[(open + 1)..(open + 4)]);

        // going: you move, the place is marked visited, and a minute passes
        Assert.Contains("world:   action 152: go out", lines);
        Assert.Contains("world:   you (211) moved: from Your Quarters (238) to Heecheetown (241)", lines);
        Assert.Contains("world:   Heecheetown (241): flag 2, visited, set", lines);
        Assert.Contains("world: 12:02 pm, day 1, in Heecheetown (241)", lines);

        // waiting is half an hour
        Assert.Contains("world:   action 172: wait", lines);
        Assert.Contains("world: 12:32 pm, day 1, in Heecheetown (241)", lines);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(60, 60).Select(line => line.Text)];
        Assert.Equal("12:32 pm   day 1", panel[0]);
        Assert.Equal("Heecheetown (241)", panel[1]);
        Assert.Contains("  east       to Blue Hell Bar (244)", panel);
        int timers = Array.IndexOf(panel, "Timers");
        Assert.True(timers > 0);
        Assert.Contains(panel, line => line.StartsWith("  Thom Seldridge comes into the bar: ", StringComparison.Ordinal));
    }
}

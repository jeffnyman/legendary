using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Spellcasting 201</i>'s world, with the game played under it: what it is,
/// what changes, each action and each timer, without yet how a command is parsed. Without the game (in LEGENDARY_GAMES, or
/// legend/sc201/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatch201Tests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-201").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "sc201", "game")) ? Path.Combine(root, "sc201", "game") : null;

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
        machine.Load("S201.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.Spellcasting201, said,
            Legendary.Core.Text.Vocabulary.Read(FileIn(folder, "vocab.dat")), FileIn(folder, "object.dat"));

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

        // the launcher's title and the story so far, with nothing to say, a key at a time
        Run(5);
        Assert.Equal("", said.ToString());
        for (int key = 0; key < 6; key++)
        {
            Type("\n");
        }

        Type("take bucket\n");
        Type("up\n");
        Type("wait\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 482 objects", lines[0], StringComparison.Ordinal);
        Assert.Equal("world: how the game parses a command isn't followed yet", lines[1]);

        // as it starts: the names from OBJECT.DAT, and the exits from the one table of them
        Assert.Equal("world: 1:35 pm, turn 0, in The Sorcerer's Lab (46)", lines[2]);
        Assert.StartsWith("world:   here: skull (478), globe light (477), you (217) [registration form (85), ", lines[3], StringComparison.Ordinal);
        Assert.Equal("world:   exits: northwest to The Sorcerer's Yard (47); up to The Sorcerer's Yard (47); out to The Sorcerer's Yard (47)", lines[4]);

        // the timers the game starts with, which are numbers here and are named by them
        Assert.Contains("world:   timer 27 (the Sorcerer returns) started: 24 turns to go", lines);
        Assert.Contains("world:   timer 5 (sleepiness) started: 99 turns to go", lines);

        // each action as it is performed, named by the grammar, and who answered it
        int got = Array.IndexOf(lines, "world:   action 83: get small wooden bucket (193)");
        Assert.True(got > 0);
        Assert.Equal("world:     answered by the action's own routine: 1", lines[got + 1]);
        Assert.Contains("world:   action 211: go up", lines);
        Assert.Contains("world:   action 206: wait", lines);

        // waiting lets the clock's timer come due: it runs, and puts itself back
        int ran = Array.IndexOf(lines, "world:   timer 7 (the tower clock chimes) runs");
        Assert.True(ran > 0);
        Assert.Equal("world:   timer 7 (the tower clock chimes) started: 1 turn to go", lines[ran + 1]);

        // what the room's description shows is marked as seen, by the game's table of flags
        Assert.Contains("world:   workbench (188): flag 5, seen, set", lines);

        // taking the bucket moves it and marks it touched, and takes a turn of five minutes
        Assert.Contains("world:   small wooden bucket (193) moved: from workbench (188), its list 2 to you (217)", lines);
        Assert.Contains("world:   small wooden bucket (193): flag 6, touched, set", lines);
        Assert.Contains("world: 1:40 pm, turn 1, in The Sorcerer's Lab (46)", lines);

        // going up: you move, and the place is marked visited, which for a place is flag 6
        Assert.Contains("world:   you (217) moved: from The Sorcerer's Lab (46) to The Sorcerer's Yard (47)", lines);
        Assert.Contains("world:   The Sorcerer's Yard (47): flag 6, visited, set", lines);

        // the panel: the world as it is now, with the timers by the names of their numbers
        string[] panel = [.. watch.Panel(46, 60).Select(line => line.Text)];
        Assert.Equal("The Sorcerer's Yard (47)", panel[1]);
        Assert.Contains("    small wooden bucket (193), open", panel);
        Assert.Contains("  southeast  to The Sorcerer's Lab (46)", panel);
        int timers = Array.IndexOf(panel, "Timers");
        Assert.True(timers > 0);
        Assert.StartsWith("  the Sorcerer returns: ", panel[timers + 1], StringComparison.Ordinal);
        Assert.Contains(panel, line => line.StartsWith("  sleepiness: ", StringComparison.Ordinal));
    }
}

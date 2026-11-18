using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Gateway II</i>'s world, with the game played under it: what it is,
/// what changes, each command as it is parsed, each action and each timer. Without the game (in LEGENDARY_GAMES, or
/// legend/gateway2/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatchGateway2Tests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-gateway2").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "gateway2", "game")) ? Path.Combine(root, "gateway2", "game") : null;

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
        var opened = new List<string>();
        machine.Log = line =>
        {
            if (line.Contains("RESTART.DAT", StringComparison.OrdinalIgnoreCase))
            {
                opened.Add(line);
            }
        };

        // as the player sets it up, from the game's LEGEND.INI: the CD's folder and the folder
        // the game was installed to are both the game's own
        machine.ServeCdFolder("GW2");
        machine.Mount("GATEWAY2", folder);
        machine.Load("HOME.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.Gateway2, said,
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
        // the title, its animations and the story so far take what's typed until the game is
        // ready for a command
        for (int key = 0; key < 60; key++)
        {
            Type("\n");
        }

        Type("look\n");
        Type("examine sofa\n");
        Type("take vase\n");
        Type("southeast\n");

        // as it starts the game writes the state it starts from, in the folder its settings
        // name: with no such folder it says it is unable to open the file
        Assert.Contains(opened, line => line.Contains("open C:\\GATEWAY2\\RESTART.DAT (write)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(opened, line => line.Contains("write 512 bytes", StringComparison.Ordinal));
        Assert.DoesNotContain(opened, line => line.Contains("not found", StringComparison.Ordinal));

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 521 objects", lines[0], StringComparison.Ordinal);

        // as it starts: eight in the morning, at home, with records of the new design read
        Assert.Equal("world: 8:00 am, turn 0, in Living Room (288)", lines[1]);
        Assert.StartsWith("world:   here: control unit (335), PV console (334), potted plant (330), lamp (326), ", lines[2], StringComparison.Ordinal);
        Assert.Equal("world:   exits: southwest to Bedroom (289); southeast to Foyer (290)", lines[3]);
        Assert.Contains("world:   timer 50 (the PV goes dormant) started: 10 turns to go", lines);

        // a command as the parser takes it: the action and its objects are in the data segment
        // here, and the ten places a routine answers are closer together
        int examine = Array.IndexOf(lines, "world:   words: examine [verb] sofa [noun]");
        Assert.True(examine > 0);
        Assert.Equal(
            [
                "world:   form 68: examine OBJECT, which is action 46",
                "world:   object \"sofa\", looked for anywhere near: white sofa (306)",
                "world:   action 46: examine white sofa (306)",
                "world:     answered by white sofa (306), asked as the object: 1",
            ],
            lines[(examine + 1)..(examine + 5)]);
        Assert.Contains("world:   object \"vase\", looked for carried, in the room, in something there: vase (310)", lines);

        // a command takes a minute and a turn
        int look = Array.IndexOf(lines, "world:   action 83: look");
        Assert.Equal("world:     answered by the action's own routine: 1", lines[look + 1]);
        Assert.Contains("world: 8:01 am, turn 1, in Living Room (288)", lines);

        // going: you move, the exits there are read, three of them a routine's to decide
        Assert.Contains("world:   action 134: go southeast", lines);
        Assert.Contains("world:   you (1) moved: from Living Room (288) to Foyer (290)", lines);
        Assert.Contains("world:   exits: northwest to Living Room (288); south to Hallway (291); east: routine 38 decides; up: routine 38 decides; out: routine 38 decides", lines);
        Assert.Contains("world:   Foyer (290): flag 7, visited, set", lines);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(60, 60).Select(line => line.Text)];
        Assert.Equal("8:04 am   turn 4", panel[0]);
        Assert.Equal("Foyer (290)", panel[1]);
        Assert.Contains("  south      to Hallway (291)", panel);
        Assert.Contains(panel, line => line.StartsWith("  the PV goes dormant: ", StringComparison.Ordinal));
    }
}

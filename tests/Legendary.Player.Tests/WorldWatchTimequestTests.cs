using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Timequest</i>'s world, with the game played under it: what it is,
/// what changes, each command as it is parsed, each action and each timer. Without the game (in LEGENDARY_GAMES, or
/// legend/tquest/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatchTimequestTests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-timequest").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "tquest", "game")) ? Path.Combine(root, "tquest", "game") : null;

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
    public void It_says_the_place_the_time_and_what_changes_as_the_game_is_played()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000 };
        machine.Load("TQ.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.Timequest, said,
            Legendary.Core.Text.Vocabulary.Read(FileIn(folder, "vocab.dat")));

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
        for (int key = 0; key < 6; key++)
        {
            Type("\n");
        }

        Type("wait\n");
        Type("\n");
        Type("wait\n");
        Type("\n");
        Type("\n");
        Type("west\n");
        Type("open drawer\n");
        Type("take card then examine it\n");
        Type("drop all\n");
        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 518 objects", lines[0], StringComparison.Ordinal);

        // as it starts: at home, which has a clock of its own, with a minute to a turn
        Assert.Equal("world: home, 12:00 pm, turn 0, in Drexler's office (483)", lines[1]);
        Assert.Equal("world:   exits: west to Vettenmyer's quarters (482); out to Vettenmyer's quarters (482)", lines[3]);
        Assert.Contains("world: home, 12:01 pm, turn 1, in Drexler's office (483)", lines);

        // the game sets up every place and time before the first turn, and starts the timers
        // each will open with: they are numbers, named by them
        Assert.Contains("world:   timer 28 (Churchill's broadcast) started: 13 turns to go", lines);
        Assert.Contains("world:   timer 54 (the chariot race) started: 60 turns to go", lines);

        // waiting brings on the opening: the timers run, and start the ones that follow
        int steps = Array.IndexOf(lines, "world:   timer 87 (Vettenmyer steps out of the wall) runs");
        Assert.True(steps > 0);
        Assert.Equal("world:   timer 88 (the general's orders) started: 1 turn to go", lines[steps + 1]);
        Assert.Contains("world:   timer 88 (the general's orders) runs", lines);

        // a command as the parser takes it, and the action it makes, by the first game's steps
        int open = Array.IndexOf(lines, "world:   words: open [verb] drawer [noun]");
        Assert.True(open > 0);
        Assert.Equal(
            [
                "world:   form 183: open OBJECT, which is action 94",
                "world:   object \"drawer\", looked for anywhere near: drawer (491)",
                "world:   action 94: open drawer (491)",
            ],
            lines[(open + 1)..(open + 4)]);
        Assert.Contains("world:   drawer (491): flag 11, open, set", lines);
        Assert.Contains("world:   drawer (491): flag 2, touched, set", lines);

        // going: you move, and the place is marked visited, which for a place is flag 2
        Assert.Contains("world:   action 144: go west", lines);
        Assert.Contains("world:   you (485) moved: from Drexler's office (483) to Vettenmyer's quarters (482)", lines);
        Assert.Contains("world:   Vettenmyer's quarters (482): flag 2, visited, set", lines);
        Assert.Contains("world:   exits: in: routine 3 decides; east to Drexler's office (483); out to Drexler's office (483)", lines);

        // taking, and then IT; and who answered each
        Assert.Contains("world:   card (498) moved: from drawer (491) to you (485)", lines);
        int examined = Array.IndexOf(lines, "world:   action 48: examine card (498)");
        Assert.True(examined > 0);
        Assert.Equal("world:     answered by card (498), asked as the object: 1", lines[examined + 1]);

        // ALL is gathered by the parser, and the wristlet's dropping is answered before the action's routine
        Assert.Contains("world:   object \"all\", looked for held: card (498) or pill (496) or briefing papers (488) or wristlet (495)", lines);
        int wristlet = Array.IndexOf(lines, "world:   action 38: drop wristlet (495)");
        Assert.True(wristlet > 0);
        Assert.Contains("world:     answered by the routine before the action's: 1", lines[wristlet..]);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(46, 60).Select(line => line.Text)];
        Assert.Equal("Vettenmyer's quarters (482), home", panel[1]);
        Assert.Contains("  card (498)", panel);
        Assert.Contains("  east  to Drexler's office (483)", panel);
        int timers = Array.IndexOf(panel, "Timers");
        Assert.True(timers > 0);
        Assert.Contains(panel, line => line.StartsWith("  the surroundings melt away: ", StringComparison.Ordinal));
    }
}

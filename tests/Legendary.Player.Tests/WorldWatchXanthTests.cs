using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Companions of Xanth</i>'s world, with the game played under it by the
/// mouse: what it is, what changes, each action by its verb and each timer. Without the game
/// (in LEGENDARY_GAMES, or legend/xanth/game above the tests), its test passes without checking.
/// </summary>
public sealed class WorldWatchXanthTests : IDisposable
{
    private const byte Escape = 0x01;

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-xanth").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "xanth", "game")) ? Path.Combine(root, "xanth", "game") : null;

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

    [Fact]
    public void It_says_the_room_and_each_action_as_the_game_is_played_by_the_mouse()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000, Mouse = true };

        // as the player sets it up, from the game's LEGEND.INI: the CD's folder and the folder
        // the game was installed to are both the game's own
        // and the Sound Blaster's IRQ is the one its settings say, 7, which it waits on for each
        // block of a line of speech to end
        Assert.Equal(7, Games.SoundBlasterInterruptOf(folder));
        machine.SoundBlasterInterrupt = 7;
        int blocks = 0;
        machine.Log = line => blocks += line.StartsWith("sound blaster: 16384 bytes at 22222 Hz", StringComparison.Ordinal) ? 1 : 0;
        machine.ServeCdFolder("XANTH");
        machine.Mount("XANTH", folder);
        machine.Load("XANTH.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(machine, WorldMap.Xanth, said, null, FileIn(folder, "object.dat"));

        var samples = new short[16384];

        void Run(double seconds)
        {
            for (int frame = 0; frame < seconds * 60; frame++)
            {
                machine.Run(machine.InstructionsPerSecond / 60);

                // the sound is taken as it is made, as a player takes it
                while (machine.ReadSamples(samples) > 0)
                {
                }

                watch.Look();
            }
        }

        // the pointer is taken to the top left corner, where it stops, and from there to the
        // place: the driver counts 640 across a screen of 320, and two mickeys to a row
        void Click(int x, int y)
        {
            machine.MoveMouse(-2000, -2000);
            Run(0.2);
            machine.MoveMouse(2 * x, 2 * y);
            Run(0.5);
            machine.SetMouseButton(0, true);
            Run(0.1);
            machine.SetMouseButton(0, false);
            Run(3);
        }

        // the demons' wager, which Escape cuts short, and then the titles: a third Escape
        // would ask whether to quit
        // which they are left to speak the start of
        Run(52);
        for (int key = 0; key < 2; key++)
        {
            machine.PressKey(Escape);
            Run(0.05);
            machine.ReleaseKey(Escape);
            Run(5);
        }

        Run(8);

        // as the game's own tour does it: the verb, then the thing in the picture
        Click(18, 9);
        Click(235, 71);

        // and the compass, to the northwest
        Click(12, 135);

        // the demons speak: a line is more than its first block, three quarters of a second
        Assert.True(blocks > 10, $"{blocks} blocks of speech were played");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 498 objects", lines[0], StringComparison.Ordinal);

        // as it starts: no time of day, in the bedroom, with what is in and on what
        Assert.Equal("world: turn 0, in bedroom (34)", lines[1]);
        Assert.StartsWith("world:   here: bookshelf (48), window (45), desk (35) [pen holder (46) [pencils (47)], personal computer (39) [", lines[2], StringComparison.Ordinal);
        Assert.Equal("world:   exits: northwest to entrance (51)", lines[3]);
        Assert.Contains("world:   timer 3 (the telephone rings) started: 12 turns to go", lines);

        // an action is said by its verb, and performed as the games with a parser perform one
        int take = Array.IndexOf(lines, "world:   action 68: Take Post-It(tm) (44)");
        Assert.True(take > 0);
        Assert.Contains("world:     answered by the action's own routine: 1", lines[take..]);
        Assert.Contains("world:   Post-It(tm) (44) moved: from personal computer (39), its list 2 to You (1)", lines);
        Assert.Contains("world:   Post-It(tm) (44): flag 5, touched, set", lines);
        Assert.Contains("world: turn 1, in bedroom (34)", lines);

        // walking is an action too, with a direction where its object would be
        Assert.Contains("world:   action 81: go northwest", lines);
        Assert.Contains("world:   You (1) moved: from bedroom (34) to entrance (51)", lines);
        Assert.Contains("world:   exits: southeast to bedroom (34); west to kitchen (64); north: routine 0 decides", lines);
        Assert.Contains("world:   entrance (51): flag 5, visited, set", lines);

        // the panel: the world as it is now
        string[] panel = [.. watch.Panel(60, 60).Select(line => line.Text)];
        Assert.Equal("turn 2", panel[0]);
        Assert.Equal("entrance (51)", panel[1]);
        Assert.Contains("    Post-It(tm) (44)", panel);
        Assert.Contains("  west       to kitchen (64)", panel);
        Assert.Contains("  the telephone rings: 10 turns to go", panel);
    }
}

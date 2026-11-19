using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on <i>Mission Critical</i>'s world, a 32-bit program's, with the game played under
/// it by the mouse. Without the game (in LEGENDARY_GAMES, or legend/mcritic/game above the
/// tests) with its CDs' speech and films copied in, its test passes without checking.
/// </summary>
public sealed class WorldWatchMissionCriticalTests : IDisposable
{
    private const byte Escape = 0x01;

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch-mcritic").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        // the game looks for its CD's speech before anything else
        string? Has(string root) => File.Exists(Path.Combine(root, "mcritic", "game", "MC001.VOC")) ? Path.Combine(root, "mcritic", "game") : null;

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

    [Fact]
    public void It_finds_the_game_above_the_first_megabyte_and_says_the_room_and_each_action()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 16_000_000, Mouse = true };
        machine.ServeCdFolder("MISSION");
        machine.Mount("MCRITIC", folder);
        machine.Load("MISSION.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var watch = new WorldWatch(
            machine, WorldMap.MissionCritical, said, null,
            File.ReadAllBytes(Directory.EnumerateFiles(folder).First(file => Path.GetFileName(file).Equals("object.dat", StringComparison.OrdinalIgnoreCase))));

        var samples = new short[16384];

        void Run(double seconds)
        {
            for (int frame = 0; frame < seconds * 60; frame++)
            {
                machine.Run(machine.InstructionsPerSecond / 60);
                while (machine.ReadSamples(samples) > 0)
                {
                }

                watch.Look();
            }
        }

        // the pointer to the top left corner, where it stops, and from there to the place
        void Click(int x, int y)
        {
            machine.MoveMouse(-4000, -4000);
            Run(0.2);
            machine.MoveMouse(x, 2 * y);
            Run(0.5);
            machine.SetMouseButton(0, true);
            Run(0.1);
            machine.SetMouseButton(0, false);
            Run(4);
        }

        // the opening film, its parts cut short by Escape, and the story so far
        Run(6);
        for (int key = 0; key < 4; key++)
        {
            machine.PressKey(Escape);
            Run(0.05);
            machine.ReleaseKey(Escape);
            Run(4);
        }

        Run(6);

        // as the game's own tour does it: forward, down the corridor
        Click(318, 212);

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, its tables at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 872 objects", lines[0], StringComparison.Ordinal);

        // as it starts: in a corridor of the Lexington, with the Captain's note
        Assert.Equal("world: in Corridor (147)", lines[1]);
        Assert.Equal("world:   here: you (1) [Captain's note (148)]", lines[2]);
        Assert.Equal("world:   exits: north to Corridor (136); south to Corridor (145); east to Corridor (149)", lines[3]);

        // the click performs an action, its arguments in registers, and you are moved
        int walk = Array.IndexOf(lines, "world:   action 15: Walk on a routine alone (65)");
        Assert.True(walk > 0, said.ToString());
        Assert.Contains("world:   you (1) moved: from Corridor (147) to Corridor (136)", lines[walk..]);
        Assert.Contains("world: in Corridor (136)", lines[walk..]);
        Assert.Contains(lines[walk..], line => line.StartsWith("world:   here: you (1) [Captain's note (148)], Emergency Stores Cabinet (137) [", StringComparison.Ordinal));
    }
}

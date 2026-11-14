using Legendary.Core.Pictures;

namespace Legendary.Pc.Tests;

/// <summary>
/// <i>Spellcasting 101</i> (Legend Entertainment, 1990), from the player's copy, started as the
/// player starts it: its launcher's title, and the game the launcher loads over itself. The
/// pictures go to <c>screenshots</c> beside the tests. Without the game (in LEGENDARY_GAMES, or
/// legend/sc101/game above the tests), these pass without checking.
/// </summary>
public sealed class Spellcasting101Tests : IDisposable
{
    private const byte Enter = 0x1C;

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-saves").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string Shot(string name)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name);
    }

    /// <summary>The launcher started at an AT's pace, from midnight, and left to draw its title.</summary>
    private Machine? Start()
    {
        if (TestData.Game("sc101/game") is not { } folder)
        {
            return null;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add, InstructionsPerSecond = 3_000_000, TickCount = 0 };
        machine.Load("S101.EXE");
        Run(machine, 6);
        return machine;
    }

    private static void Run(Machine machine, double seconds)
    {
        var buffer = new short[16384];
        for (double t = 0; t < seconds; t += 0.05)
        {
            machine.Run(machine.InstructionsPerSecond / 20);
            while (machine.ReadSamples(buffer) > 0)
            {
            }
        }
    }

    /// <summary>
    /// The launcher finds a VGA, takes the EGA's 640 by 350 in 16 colors, and draws the title
    /// from S101_E1.PIC. What's on the screen is, pixel for pixel, what Legendary's own reader
    /// of the picture files makes of picture 38: the game's unpacking and ours agree.
    /// </summary>
    [Fact]
    public void The_launcher_draws_the_title_as_our_reader_unpacks_it()
    {
        if (Start() is not { } machine)
        {
            return;
        }

        Assert.Contains("10/00 set video mode 10h", _log);
        Assert.Contains(_log, line => line.Contains("S101_E1.PIC", StringComparison.Ordinal));
        Assert.Equal((640, 350), (machine.DisplayWidth, machine.DisplayHeight));
        Screenshot.WritePng(machine, Shot("s101-title.png"));

        string folder = TestData.Game("sc101/game")!;
        string file = Directory.EnumerateFiles(folder).First(path => path.EndsWith("s101_e1.pic", StringComparison.OrdinalIgnoreCase));
        var pictures = new PictureFile(File.ReadAllBytes(file), ega: true);
        var title = pictures.Read(pictures.Entries[38]);

        uint[] screen = new uint[640 * 350];
        machine.Render(screen);
        int different = 0;
        for (int i = 0; i < screen.Length; i++)
        {
            var (red, green, blue) = PictureFile.EgaColor(title.Palette![title.Pixels[i]]);
            if ((screen[i] & 0xFFFFFF) != (uint)(red << 16 | green << 8 | blue))
            {
                different++;
            }
        }

        Assert.Equal(0, different);
    }

    /// <summary>
    /// At a key, the launcher frees its own memory by hand, through DOS's list of lists, and
    /// loads S101MAIN.EXE over itself as an overlay. The game then starts: PLINK86 loads its
    /// data, it reads its vocabulary and opens its text, writes the world's first state to
    /// RESTART.DAT, and asks whether to restore a saved game.
    /// </summary>
    [Fact]
    public void A_key_loads_the_game_over_the_launcher_and_it_starts()
    {
        if (Start() is not { } machine)
        {
            return;
        }

        machine.PressKey(Enter);
        Run(machine, 0.1);
        machine.ReleaseKey(Enter);
        Run(machine, 11);
        Screenshot.WritePng(machine, Shot("s101-restore.png"));

        Assert.Contains("21/52 list of lists", _log);
        Assert.Contains(_log, line => line.StartsWith("21/4B load overlay", StringComparison.Ordinal) && line.Contains("S101MAIN.EXE", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.StartsWith("21/3D open vocab.dat", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.StartsWith("21/3D open S101STR.DAT", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.Contains("restart.dat (write)", StringComparison.Ordinal));

        // the game itself writes the file its notes took for its makers', 13,463 bytes of it
        Assert.Equal(13463, new FileInfo(Path.Combine(_saves, "RESTART.DAT")).Length);
        Assert.False(machine.Exited);
    }
}

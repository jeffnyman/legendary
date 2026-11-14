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
    private Machine? Start(string arguments = "")
    {
        if (TestData.Game("sc101/game") is not { } folder)
        {
            return null;
        }

        var machine = new Machine(folder, _saves) { Log = _log.Add, InstructionsPerSecond = 3_000_000, TickCount = 0 };
        machine.Load("S101.EXE", arguments);
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

    private void PressEnter(Machine machine)
    {
        machine.PressKey(Enter);
        Run(machine, 0.1);
        machine.ReleaseKey(Enter);
        Run(machine, 11);
    }

    /// <summary>The video modes set, in order, from the log.</summary>
    private string[] Modes() => [.. _log.Where(line => line.StartsWith("10/00 set video mode", StringComparison.Ordinal)).Select(line => line[^3..])];

    /// <summary>
    /// The game asks Microsoft's graphics library for the EGA's 640 by 350, and takes the CGA's
    /// 640 by 200 only if the library won't set it. The library goes by the VGA's state, INT 10h
    /// function 1Bh, and the table of modes it points at: with those as a VGA has them, the game
    /// draws for the EGA, as its launcher did.
    /// </summary>
    [Fact]
    public void The_game_draws_for_the_EGA_as_its_launcher_did()
    {
        if (Start() is not { } machine)
        {
            return;
        }

        PressEnter(machine);
        Screenshot.WritePng(machine, Shot("s101-restore-ega.png"));

        Assert.Equal(["10h", "10h"], Modes());
        Assert.Equal(0x10, machine.VideoMode);
        Assert.DoesNotContain(_log, line => line.Contains("S101_C1.PIC", StringComparison.Ordinal));
    }

    /// <summary>
    /// With CGA after its name, the launcher draws for the CGA, and hands the word on to the
    /// game after the ">" that says the launcher started it.
    /// </summary>
    [Fact]
    public void CGA_on_the_command_line_makes_both_draw_for_the_CGA()
    {
        if (Start("CGA") is not { } machine)
        {
            return;
        }

        PressEnter(machine);
        Screenshot.WritePng(machine, Shot("s101-restore-cga.png"));

        Assert.Equal(["06h", "06h"], Modes());
        Assert.Contains(_log, line => line.Contains("S101_C1.PIC", StringComparison.Ordinal));
        Assert.DoesNotContain(_log, line => line.Contains("S101_E1.PIC", StringComparison.Ordinal));
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

        PressEnter(machine);

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

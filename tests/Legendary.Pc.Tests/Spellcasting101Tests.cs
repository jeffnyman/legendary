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

    // the keys' scan codes, a row of the keyboard at a time
    private const string Digits = "1234567890-=", Top = "qwertyuiop[]", Home = "asdfghjkl;'", Bottom = "zxcvbnm,./";

    // where the notes have things, as segments from where the game loads
    private const int DataSegment = 0x4517, FarData = 0x3B00, YouSegment = 0x27F5;

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
    private Machine? Start(string arguments = "", bool adLib = false, bool mouse = false, int disk = 0)
    {
        if (TestData.Game("sc101/game") is not { } folder)
        {
            return null;
        }

        var machine = new Machine(folder, _saves)
        {
            Log = _log.Add, InstructionsPerSecond = 3_000_000, TickCount = 0, Mouse = mouse, DiskBytesPerSecond = disk,
        };
        if (adLib)
        {
            machine.LoadCommands("SOUND.COM", "S101.EXE");
        }
        else
        {
            machine.Load("S101.EXE", arguments);
        }

        Run(machine, 6);
        return machine;
    }

    // how loud what the machine has played since this was last cleared is: the sum of its
    // samples' squares, and their count
    private static double _energy;
    private static long _samples;

    private static void Run(Machine machine, double seconds)
    {
        var buffer = new short[16384];
        for (double t = 0; t < seconds; t += 0.05)
        {
            machine.Run(machine.InstructionsPerSecond / 20);
            int count;
            while ((count = machine.ReadSamples(buffer)) > 0)
            {
                foreach (short sample in buffer.AsSpan(0, count))
                {
                    _energy += (double)sample * sample;
                }

                _samples += count;
            }
        }
    }

    /// <summary>The loudness of what has played since the last call, as a root mean square, and a fresh start.</summary>
    private static double Loudness()
    {
        double loudness = Math.Sqrt(_energy / Math.Max(_samples, 1));
        (_energy, _samples) = (0, 0);
        return loudness;
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

    private static byte Scan(char key) => key switch
    {
        ' ' => 0x39,
        '\n' => Enter,
        _ when Digits.Contains(key) => (byte)(0x02 + Digits.IndexOf(key)),
        _ when Top.Contains(key) => (byte)(0x10 + Top.IndexOf(key)),
        _ when Home.Contains(key) => (byte)(0x1E + Home.IndexOf(key)),
        _ when Bottom.Contains(key) => (byte)(0x2C + Bottom.IndexOf(key)),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key these tests press."),
    };

    /// <summary>Types some text, a key at a time, and gives the game four seconds to answer.</summary>
    private static void Type(Machine machine, string text)
    {
        foreach (char key in text)
        {
            machine.PressKey(Scan(key));
            Run(machine, 0.05);
            machine.ReleaseKey(Scan(key));
            Run(machine, 0.05);
        }

        Run(machine, 4);
    }

    /// <summary>The segment the game was loaded at, from the log's line for its loading.</summary>
    private ushort LoadSegment()
    {
        string line = _log.Last(line => line.StartsWith("21/4B load overlay", StringComparison.Ordinal));
        return Convert.ToUInt16(line[^5..^1], 16);
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

    /// <summary>
    /// The game, played: no to restoring, and then three commands. What it does is read from its
    /// own memory, at the places the notes found by taking the program apart, and is what the
    /// notes say it should be.
    /// </summary>
    [Fact]
    public void Three_commands_do_what_the_notes_say_they_do()
    {
        if (Start() is not { } machine)
        {
            return;
        }

        PressEnter(machine);
        Type(machine, "n");
        Screenshot.WritePng(machine, Shot("s101-bedchamber.png"));

        ushort load = LoadSegment();
        ushort data = (ushort)(load + DataSegment), far = (ushort)(load + FarData);
        int Room() => machine.ReadWord(data, 0x42);
        int Moves() => machine.ReadWord(data, 0x46);
        int Clock() => machine.ReadWord(data, 0x48);
        (int Action, int Object) Performed() => (machine.ReadByte(far, 0x58), machine.ReadWord(far, 0x00));

        // the game starts in the Bedchamber, object 1, with you, object 153, in it, at 3:35 in
        // the afternoon: the clock counts five minutes at a time
        Assert.Equal((1, 153, 0), (Room(), machine.ReadWord(data, 0x44), Moves()));
        Assert.Equal(1, machine.ReadWord((ushort)(load + YouSegment), 0x0E));
        Assert.Equal((15 * 60 + 35) / 5, Clock());

        // EXAMINE is action 60, and the rat object 352
        Type(machine, "examine rat\n");
        Assert.Equal((60, 352), Performed());
        Assert.Equal((1, 1), (Room(), Moves()));

        // CLIMB CHIMNEY is action 25 on object 185, but the chimney's own routine, while the fire
        // is in the fireplace, starts over with action 27 on the fireplace, object 62
        Type(machine, "climb chimney\n");
        Assert.Equal((27, 62), Performed());
        Assert.Equal((1, 2), (Room(), Moves()));

        // north from the Bedchamber is the Potty Chamber, object 2; going is action 197
        Type(machine, "n\n");
        Screenshot.WritePng(machine, Shot("s101-potty-chamber.png"));
        Assert.Equal(197, Performed().Action);
        Assert.Equal((2, 3), (Room(), Moves()));
        Assert.Equal(2, machine.ReadWord((ushort)(load + YouSegment), 0x0E));
        Assert.Equal((15 * 60 + 50) / 5, Clock());

        // and the picture now on the screen is picture 51 of the first file, as Legendary's
        // reader unpacks it, pixel for pixel
        string folder = TestData.Game("sc101/game")!;
        string file = Directory.EnumerateFiles(folder).First(path => path.EndsWith("s101_e1.pic", StringComparison.OrdinalIgnoreCase));
        var pictures = new PictureFile(File.ReadAllBytes(file), ega: true);
        var room = pictures.Read(pictures.Entries[51]);
        uint[] screen = new uint[640 * 350];
        machine.Render(screen);
        int different = 0;
        for (int y = 0; y < room.Height; y++)
        {
            for (int x = 0; x < room.Width; x++)
            {
                var (red, green, blue) = PictureFile.EgaColor(room.Palette![room.Pixels[y * room.Width + x]]);
                if ((screen[y * 640 + 261 + x] & 0xFFFFFF) != (uint)(red << 16 | green << 8 | blue))
                {
                    different++;
                }
            }
        }

        Assert.Equal(0, different);
    }

    /// <summary>
    /// With AdLib's driver, SOUND.COM, run first and left in memory, the launcher finds it with
    /// no word said: it opens the first music file and the instruments, and plays the title's
    /// music by the FM chip, where without the driver it plays MUSIC.RS on the speaker.
    /// </summary>
    [Fact]
    public void With_AdLibs_driver_loaded_the_title_plays_by_FM()
    {
        Loudness();
        if (Start(adLib: true) is not { } machine)
        {
            return;
        }

        Assert.Contains(_log, line => line.StartsWith("stays resident", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.Contains("S101_m1.mus", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_log, line => line.Contains("S101.BNK", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_log, line => line.Contains("MUSIC.RS", StringComparison.OrdinalIgnoreCase));
        Assert.True(Loudness() > 100, "The title is silent.");
        Assert.False(machine.Exited);
    }

    // a hard disk of the game's day, by what gets through DOS: 200 KB a second
    private const int HardDisk = 200_000;

    /// <summary>How many tenths of a second, of the next so many, have sound in them.</summary>
    private static int LoudTenths(Machine machine, int tenths)
    {
        int loud = 0;
        for (int i = 0; i < tenths; i++)
        {
            Loudness();
            Run(machine, 0.1);
            if (Loudness() > 300)
            {
                loud++;
            }
        }

        return loud;
    }

    /// <summary>
    /// Plays to where Joey Rottenwood laughs, as a player with a mouse and an AdLib card would:
    /// into the game, some seconds doing nothing, three turns of waiting, and a key, held a tenth
    /// of a second, at each of the two prompts for more that the scene stops at. The laugh comes
    /// after the second.
    /// </summary>
    private Machine? ToTheLaugh(int disk, double idle)
    {
        if (Start(adLib: true, mouse: true, disk: disk) is not { } machine)
        {
            return null;
        }

        PressEnter(machine);
        Type(machine, "n");
        Run(machine, idle);
        Type(machine, "wait\n");
        Type(machine, "wait\n");
        Type(machine, "wait\n");
        for (int prompt = 0; prompt < 2; prompt++)
        {
            machine.PressKey(Enter);
            Run(machine, 0.1);
            machine.ReleaseKey(Enter);
            if (prompt == 0)
            {
                Run(machine, 4);
            }
        }

        return machine;
    }

    /// <summary>
    /// The recorded sounds are the speaker's, whatever plays the music: Joey Rottenwood's laugh
    /// is ROTTEN3.RS, played by RealSound, all 2.8 seconds of it.
    /// </summary>
    /// <remarks>
    /// It is heard only on a disk that takes time. The game's player stops a sound at any
    /// interrupt from the keyboard, a key let go as much as a key pressed, and the laugh is
    /// started by a key, at a prompt for more. On a PC of 1990 the file was a fraction of a second
    /// coming off the disk, the key was up again by then, and nobody knew.
    /// </remarks>
    [Fact]
    public void Rottenwoods_laugh_is_a_recording_on_the_speaker()
    {
        if (ToTheLaugh(HardDisk, idle: 70) is not { } machine)
        {
            return;
        }

        int loud = LoudTenths(machine, 40);
        Screenshot.WritePng(machine, Shot("s101-rottenwood.png"));
        Assert.Contains(_log, line => line.Contains("rotten3.rs", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(loud, 25, 32);
    }

    /// <summary>On a disk that takes no time, the key that starts the laugh ends it as it's let go.</summary>
    [Fact]
    public void On_a_disk_that_takes_no_time_the_laugh_is_cut_off_by_its_own_key()
    {
        if (ToTheLaugh(disk: 0, idle: 70) is not { } machine)
        {
            return;
        }

        Assert.Contains(_log, line => line.Contains("rotten3.rs", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(LoudTenths(machine, 40), 0, 2);
    }

    /// <summary>
    /// The game plays no recording while a song is playing: both want the PC's timer. The
    /// song that starts the game lasts most of a minute, and a player who gets to Rottenwood
    /// sooner hears the music and not the laugh, as on the PC.
    /// </summary>
    [Fact]
    public void While_the_opening_music_plays_the_laugh_is_left_out()
    {
        if (ToTheLaugh(HardDisk, idle: 5) is not { } machine)
        {
            return;
        }

        Run(machine, 4);
        Assert.DoesNotContain(_log, line => line.Contains("rotten3.rs", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, _log.Count(line => line.StartsWith("21/3D open", StringComparison.Ordinal) && line.Contains("S101_m1.mus", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The game has no pointer of its own: it asks the mouse's driver to show its arrow and to
    /// hide it. While it waits for typing at its prompt the arrow is hidden, and a click (or Tab)
    /// takes it to its menus and brings the arrow up, anywhere on the EGA's 350 rows.
    /// </summary>
    [Fact]
    public void With_a_mouse_a_click_at_the_prompt_brings_up_the_drivers_arrow()
    {
        if (Start(mouse: true) is not { } machine)
        {
            return;
        }

        PressEnter(machine);
        Type(machine, "n");

        // the game's first two pages, "Another red letter day..." and "Chapter One: Escape!",
        // each wait for a key, and so does the room's description when it has filled its window
        Type(machine, " ");
        Type(machine, " ");
        Type(machine, " ");

        uint[] screen = new uint[640 * 350];
        uint[] typing = new uint[640 * 350];
        machine.Render(typing);

        // over the text window, where a click picks nothing
        machine.MoveMouse(-10_000, 10_000);
        machine.MoveMouse(400, -98);
        Run(machine, 0.5);
        machine.Render(screen);
        Assert.Equal(typing[(301 * 640) + 401], screen[(301 * 640) + 401]);

        machine.SetMouseButton(0, true);
        Run(machine, 0.2);
        machine.SetMouseButton(0, false);
        Run(machine, 0.5);
        Screenshot.WritePng(machine, Shot("s101-arrow.png"));
        machine.Render(screen);
        Assert.Equal(0x000000u, screen[(301 * 640) + 400] & 0xFFFFFF);
        Assert.Equal(0xFFFFFFu, screen[(301 * 640) + 401] & 0xFFFFFF);
        Assert.Equal(0xFFFFFFu, screen[(305 * 640) + 402] & 0xFFFFFF);
    }
}

namespace Legendary.Player.Tests;

/// <summary>
/// Profiles and the command line, with no game needed: a temporary folder holds a stand-in
/// <c>S101.EXE</c>, a few bytes that end at once, and the settings live in a temporary file.
/// </summary>
public sealed class ProfileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("legendary-player").FullName;

    public ProfileTests()
    {
        Spellcasting = Directory.CreateDirectory(Path.Combine(_root, "Spellcasting")).FullName;
        File.WriteAllBytes(Path.Combine(Spellcasting, "S101.EXE"), Exe([0xB8, 0x00, 0x4C, 0xCD, 0x21])); // mov ax, 4C00h; int 21h

        // the file the folder is known by: the game itself, which the launcher loads
        File.WriteAllBytes(Path.Combine(Spellcasting, "S101MAIN.EXE"), [0]);
        SettingsPath = Path.Combine(_root, "home", "legendary.json");
        Saves = Path.Combine(_root, "saves");
    }

    private static KnownGame Spellcasting101 => Games.Named("sc101")!;

    private string Spellcasting { get; }

    private string SettingsPath { get; }

    private string Saves { get; }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private int Run(params string[] args)
        => Program.Run([.. args, "--settings", SettingsPath, "--saves", Saves, "--headless", "--seconds", "1"]);

    [Fact]
    public void No_settings_file_is_empty_settings()
    {
        var settings = Settings.Load(SettingsPath);
        Assert.Empty(settings.Folders);
        Assert.Null(settings.FolderOf(Spellcasting101));
        Assert.True(settings.ProfileOf(Spellcasting101).IsEmpty);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Settings_are_kept_and_read_back_whatever_the_case_of_their_names()
    {
        var settings = Settings.Load(SettingsPath);
        Assert.True(settings.Remember(Spellcasting101, Spellcasting));
        Assert.False(settings.Remember(Spellcasting101, Spellcasting));
        settings.Profiles["SC101"] = new Profile { Scale = 4, Joystick = false };
        Assert.Null(settings.PanelText);
        settings.PanelText = 1.5;
        settings.Save();

        var read = Settings.Load(SettingsPath);
        Assert.Equal(Spellcasting, read.FolderOf(Spellcasting101));
        var profile = read.ProfileOf(Spellcasting101);
        Assert.Equal(4, profile.Scale);
        Assert.False(profile.Joystick);
        Assert.Null(profile.Cycles);
        Assert.Equal("no joystick, scale 4", profile.ToString());
        Assert.Equal(1.5, read.PanelText);

        // plain JSON
        Assert.Contains("\"scale\": 4", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Settings_that_cannot_be_read_say_so()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ not settings");
        var ex = Assert.Throws<InvalidDataException>(() => Settings.Load(SettingsPath));
        Assert.Contains(SettingsPath, ex.Message);
    }

    [Fact]
    public void A_profile_takes_only_what_another_sets()
    {
        var profile = new Profile { Cycles = 4000, Sound = false };
        profile.Take(new Profile { Scale = 2 });
        Assert.Equal(4000, profile.Cycles);
        Assert.False(profile.Sound);
        Assert.Equal(2, profile.Scale);
    }

    [Theory]
    [InlineData("sc101", "Spellcasting 101: Sorcerers Get All the Girls")]
    [InlineData("SC101", "Spellcasting 101: Sorcerers Get All the Girls")]
    [InlineData("tquest", "Timequest")]
    [InlineData("mcritic", "Mission Critical")]
    public void Games_are_known_by_their_short_names(string name, string title)
        => Assert.Equal(title, Games.Named(name)!.Title);

    [Fact]
    public void Every_game_has_a_short_name_of_its_own()
        => Assert.Equal(Games.Known.Length, Games.Known.Select(game => game.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    public void A_folder_is_known_by_its_files()
    {
        Assert.Equal(Spellcasting101, Games.KnownIn(Spellcasting));
        Assert.Null(Games.KnownIn(_root));

        var game = Games.Find(Spellcasting, null);
        Assert.Equal("S101.EXE", game.Program);
        Assert.Equal(Spellcasting101, game.Known);
        Assert.Equal(Spellcasting101.Cycles, game.Cycles);
        Assert.Throws<FileNotFoundException>(() => Games.In(Spellcasting, Games.Named("tquest")!));
    }

    [Fact]
    public void A_game_on_drive_C_has_no_disks_and_a_vga()
    {
        var game = Games.Find(Spellcasting, null);
        Assert.Empty(game.Disks);
        Assert.Equal(Pc.VideoAdapter.Vga, game.Video);
    }

    [Fact]
    public void A_folder_played_once_is_remembered_for_its_short_name()
    {
        // a game that isn't set up can't be played by its short name
        var ex = Assert.Throws<FileNotFoundException>(() => Run("sc101"));
        Assert.Contains("isn't set up", ex.Message);

        // played by its folder, it's remembered, and then by its short name
        Assert.Equal(0, Run(Spellcasting));
        Assert.Equal(Spellcasting, Settings.Load(SettingsPath).FolderOf(Spellcasting101));
        Assert.Equal(0, Run("sc101"));

        // and forgotten, with --list, so nothing plays
        Assert.Equal(0, Run("sc101", "--forget-folder", "--list"));
        Assert.Null(Settings.Load(SettingsPath).FolderOf(Spellcasting101));

        // only by a short name: a folder given would be remembered again at once
        Assert.Contains("short name", Assert.ThrowsAny<Exception>(() => Run(Spellcasting, "--forget-folder")).Message);
    }

    [Fact]
    public void Another_program_in_a_games_folder_is_run_by_the_games_short_name()
    {
        // SETUP.EXE leaves a file named RAN behind: push cs; pop ds; mov dx, 10h; mov ah, 3Ch;
        // xor cx, cx; int 21h; mov ax, 4C00h; int 21h; "RAN"
        File.WriteAllBytes(Path.Combine(Spellcasting, "SETUP.EXE"), Exe(
        [
            0x0E, 0x1F, 0xBA, 0x10, 0x00, 0xB4, 0x3C, 0x31, 0xC9, 0xCD, 0x21, 0xB8, 0x00, 0x4C, 0xCD, 0x21,
            .. "RAN\0"u8,
        ]));
        Assert.Equal(0, Run(Spellcasting));

        Assert.Equal(0, Run("sc101", "SETUP.EXE"));
        Assert.True(File.Exists(Path.Combine(Saves, "RAN")));
        Assert.Contains("no NOTHERE.EXE", Assert.Throws<FileNotFoundException>(() => Run("sc101", "NOTHERE.EXE")).Message);
    }

    [Fact]
    public void Options_are_kept_only_when_asked_and_forgotten_when_asked()
    {
        Assert.Equal(0, Run(Spellcasting, "--cycles", "5000"));
        Assert.True(Settings.Load(SettingsPath).ProfileOf(Spellcasting101).IsEmpty);

        Assert.Equal(0, Run("sc101", "--cycles", "5000", "--no-sound", "--remember"));
        var profile = Settings.Load(SettingsPath).ProfileOf(Spellcasting101);
        Assert.Equal(5000, profile.Cycles);
        Assert.False(profile.Sound);

        // what's remembered is added to, not replaced
        Assert.Equal(0, Run("sc101", "--scale", "5", "--remember"));
        profile = Settings.Load(SettingsPath).ProfileOf(Spellcasting101);
        Assert.Equal(5000, profile.Cycles);
        Assert.Equal(5, profile.Scale);

        Assert.Equal(0, Run("sc101", "--mouse-speed", "50", "--remember"));
        profile = Settings.Load(SettingsPath).ProfileOf(Spellcasting101);
        Assert.Equal(50, profile.MouseSpeed);
        Assert.Equal("cycles 5000, no sound, mouse speed 50%, scale 5", profile.ToString());
        Assert.ThrowsAny<Exception>(() => Run("sc101", "--mouse-speed", "5"));

        Assert.Equal(0, Run("sc101", "--forget"));
        Assert.True(Settings.Load(SettingsPath).ProfileOf(Spellcasting101).IsEmpty);
        Assert.Equal(Spellcasting, Settings.Load(SettingsPath).FolderOf(Spellcasting101));
    }

    [Fact]
    public void A_saved_state_is_resumed_from_its_file()
    {
        // one for each program, beside the saves rather than in them, where the game would see it
        var game = Games.Find(Spellcasting, null);
        Assert.Equal(Path.Combine(Games.Home, "states", "sc101", "S101.state"), Games.StateFile(game));

        string state = Path.Combine(_root, "s101.state");
        var ex = Assert.Throws<FileNotFoundException>(() => Run(Spellcasting, "--resume", "--state", state));
        Assert.Contains("no state saved", ex.Message);

        // a state of the program as loaded, before its first instruction
        var machine = new Pc.Machine(Spellcasting, Saves);
        machine.Load("S101.EXE");
        using (var file = File.Create(state))
        {
            machine.SaveState(file);
        }

        Assert.Equal(0, Run("sc101", "--resume", "--state", state));

        File.WriteAllText(state, "not a state");
        Assert.Throws<InvalidDataException>(() => Run("sc101", "--resume", "--state", state));
    }

    [Fact]
    public void The_menu_lists_the_games_and_what_is_set_up()
    {
        var settings = Settings.Load(SettingsPath);
        settings.Remember(Spellcasting101, Spellcasting);
        string picture = Path.Combine(AppContext.BaseDirectory, "screenshots", "menu.bmp");
        Directory.CreateDirectory(Path.GetDirectoryName(picture)!);
        Sdl.SdlMenu.Snapshot(settings, picture, selected: Array.IndexOf(Games.Known, Spellcasting101), message: "Spellcasting 101 is set up");

        // a 32-bit BMP: its pixels start where its header says
        byte[] bmp = File.ReadAllBytes(picture);
        Assert.Equal((byte)'B', bmp[0]);
        int start = BitConverter.ToInt32(bmp, 10);
        var colors = new HashSet<int>();
        for (int i = start; i + 4 <= bmp.Length; i += 4)
        {
            colors.Add(BitConverter.ToInt32(bmp, i) & 0xFFFFFF);
        }

        // the background, the bar, and text in four colors at least
        Assert.True(colors.Count >= 6, $"only {colors.Count} colors");
    }

    /// <summary>An MZ executable around code that starts at its first byte, as in the core's DOS tests.</summary>
    internal static byte[] Exe(byte[] code)
    {
        int total = 32 + code.Length;
        var exe = new byte[total];
        void Word(int at, int value) => BitConverter.GetBytes((ushort)value).CopyTo(exe, at);
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        Word(0x02, total % 512);
        Word(0x04, (total + 511) / 512);
        Word(0x08, 2);
        Word(0x0A, 0x1000);
        Word(0x0C, 0xFFFF);
        Word(0x0E, 0x1000);
        Word(0x10, 0xFFFE);
        Word(0x18, 0x1C);
        code.CopyTo(exe, 32);
        return exe;
    }
}

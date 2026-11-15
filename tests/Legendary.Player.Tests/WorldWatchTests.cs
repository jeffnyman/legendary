using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player.Tests;

/// <summary>
/// The watch on a game's world, with <i>Spellcasting 101</i> played under it. Without the game
/// (in LEGENDARY_GAMES, or legend/sc101/game above the tests), its test passes without
/// checking.
/// </summary>
public sealed class WorldWatchTests : IDisposable
{
    private const byte Enter = 0x1C;
    private const string Top = "qwertyuiop", Home = "asdfghjkl", Bottom = "zxcvbnm";

    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-watch").FullName;

    public void Dispose() => Directory.Delete(_saves, recursive: true);

    private static string? Game()
    {
        string? Has(string root) => Directory.Exists(Path.Combine(root, "sc101", "game")) ? Path.Combine(root, "sc101", "game") : null;

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

    private static byte Scan(char key) => key switch
    {
        ' ' => 0x39,
        '\n' => Enter,
        ',' => 0x33,
        '.' => 0x34,
        ';' => 0x27,
        _ when Top.Contains(key) => (byte)(0x10 + Top.IndexOf(key)),
        _ when Home.Contains(key) => (byte)(0x1E + Home.IndexOf(key)),
        _ => (byte)(0x2C + Bottom.IndexOf(key)),
    };

    [Theory]
    [InlineData(0, "12:00 am")]
    [InlineData(935, "3:35 pm")]
    [InlineData(725, "12:05 pm")]
    [InlineData(59, "12:59 am")]
    [InlineData(1439, "11:59 pm")]
    public void A_time_is_said_as_the_game_says_it(int minutes, string said) => Assert.Equal(said, WorldWatch.Time(minutes));

    [Fact]
    public void A_line_too_long_for_the_panel_is_folded_under_itself()
    {
        var folded = WorldWatch.Fold(new PanelLine("  answered by rat (352), asked as the object: 1", PanelTone.Action), 20).ToList();
        Assert.Equal(["  answered by rat", "    (352), asked as", "    the object: 1"], folded.Select(line => line.Text));
        Assert.All(folded, line => Assert.Equal(PanelTone.Action, line.Tone));
        Assert.All(folded, line => Assert.True(line.Text.Length <= 20));

        // a word longer than the line is cut where the line ends
        Assert.Equal(["abcdefghij", "  klmnopqr", "  st"], WorldWatch.Fold(new PanelLine("abcdefghijklmnopqrst"), 10).Select(line => line.Text));

        // what fits is left alone
        Assert.Equal([new PanelLine("Here", PanelTone.Dim)], WorldWatch.Fold(new PanelLine("Here", PanelTone.Dim), 20));
    }

    [Fact]
    public void Before_the_game_is_there_the_panel_says_so()
    {
        var machine = new Machine(_saves, _saves);
        var watch = new WorldWatch(machine, WorldMap.Spellcasting101, null);
        watch.Look();
        Assert.Equal(
            [new PanelLine("Legendary", PanelTone.Title), new PanelLine("The game isn't in memory yet.", PanelTone.Dim)],
            watch.Panel(40, 30));
    }

    [Fact]
    public void Why_a_routine_is_asked_is_said_in_words()
    {
        Assert.Equal("where it happens", WorldWatch.Why(1));
        Assert.Equal("the second object", WorldWatch.Why(4));
        Assert.Equal("the object", WorldWatch.Why(6));
        Assert.Equal("15", WorldWatch.Why(15));
    }

    [Fact]
    public void It_says_the_room_and_each_turn_as_the_game_is_played()
    {
        if (Game() is not { } folder)
        {
            return;
        }

        var machine = new Machine(folder, _saves) { InstructionsPerSecond = 3_000_000 };
        machine.Load("S101.EXE", "");
        var said = new StringWriter { NewLine = "\n" };
        var words = Legendary.Core.Text.Vocabulary.Read(File.ReadAllBytes(Directory.EnumerateFiles(folder).First(file => Path.GetFileName(file).Equals("vocab.dat", StringComparison.OrdinalIgnoreCase))));
        var watch = new WorldWatch(machine, WorldMap.Spellcasting101, said, words);

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

        // the launcher's title, with nothing to say; then a key, the game, and no to restoring
        Run(6);
        Assert.Equal("", said.ToString());
        Type("\n");
        Run(8);
        Type("n");
        Type("examine rat\n");
        Type("climb chimney\n");
        Type("open desk\n");
        string[] opened = [.. watch.Panel(46, 60).Select(line => line.Text)];
        Type("take note\n");
        Type("i. i then i and take frotz\n");
        Type("ask rat about sword\n");
        Type("n\n");

        string[] lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("world: the game is in memory, loaded at ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(", with 427 objects", lines[0], StringComparison.Ordinal);
        Assert.Equal("world: 3:35 pm, turn 0, in Bedchamber (1)", lines[1]);
        Assert.StartsWith("world:   here: rat (352), you (153) [cloak (82)], ", lines[2], StringComparison.Ordinal);
        Assert.Equal(
            "world:   exits: north to Potty Chamber (2); east: \"If you want to enter the fireplace, then say so.\"; "
            + "west to Alley (4) by door (60); south: \"If you want to jump out the window, then say so.\"; "
            + "down: \"If you want to jump out the window, then say so.\"; up: a routine decides",
            lines[3]);

        // describing the room sets a flag on it and on each thing it mentions
        Assert.Contains("world:   Bedchamber (1): flag 10, visited, set", lines);
        Assert.Contains("world:   rat (352): flag 6, seen, set", lines);

        // the lines from one that is there to the next command's words or action, or to the end
        string[] After(string line)
        {
            int at = Array.IndexOf(lines, line);
            Assert.True(at >= 0, $"Not said: {line}");
            return [.. lines.Skip(at + 1).TakeWhile(next => !next.StartsWith("world:   action ", StringComparison.Ordinal) && !next.StartsWith("world:   words: ", StringComparison.Ordinal))];
        }

        // the rat's own routine has something to say about being examined, so the asking ends there
        Assert.Equal(
            ["world:     answered by rat (352), asked as the object: 1", "world: 3:40 pm, turn 1, in Bedchamber (1)"],
            After("world:   action 60: examine rat (352)"));

        // CLIMB CHIMNEY, which the chimney's routine starts over as entering the fireplace: both
        // are performed, and both are said, in their order. The first is never answered: the
        // routine that started over jumped back to the main loop
        Assert.Equal(["world:     given up by a routine, which starts over with another action"], After("world:   action 25: climb chimney (185)"));
        Assert.Equal(
            ["world:     answered by fireplace (62), asked as the object: 1", "world: 3:45 pm, turn 2, in Bedchamber (1)"],
            After("world:   action 27: climb in fireplace (62)"));

        // opening the desk is left to the stock routine, which sets two of the desk's flags, and
        // what was in the desk is seen
        Assert.Equal(
            [
                "world:     answered by the action's own routine: 1",
                "world:   writing desk (61): flag 10, touched, set",
                "world:   writing desk (61): flag 12, open, set",
                "world:   application (73): flag 6, seen, set",
                "world:   love note (74): flag 6, seen, set",
                "world: 3:50 pm, turn 3, in Bedchamber (1)",
            ],
            After("world:   action 128: open writing desk (61)"));

        // taking the note moves it from the desk to you, and the room's contents are said again
        string[] taken = After("world:   action 74: get love note (74)");
        Assert.Equal("world:     answered by the action's own routine: 1", taken[0]);
        Assert.Equal("world:   love note (74) moved: from writing desk (61) to you (153)", taken[1]);
        Assert.Contains(taken, line => line.StartsWith("world:   here: rat (352), you (153) [love note (74), cloak (82)], ", StringComparison.Ordinal)
            && line.Contains("writing desk (61) [application (73)]", StringComparison.Ordinal));
        Assert.Equal("world: 3:55 pm, turn 4, in Bedchamber (1)", taken[^1]);

        // going north: you are moved, the new room and its exits are said, and the routine that
        // moved you jumps back to the main loop, done. The turn isn't counted yet: the game has
        // stopped at a prompt for more, with your stepfather on the stair
        string[] gone = After("world:   action 197: go north");
        Assert.Contains("world:   you (153) moved: from Bedchamber (1) to Potty Chamber (2)", gone);
        Assert.Contains("world: 4:00 pm, turn 5, in Potty Chamber (2)", gone);
        Assert.Contains("world:   here: you (153) [love note (74), cloak (82)], tiny door (66)", gone);
        Assert.Contains("world:   exits: south to Bedchamber (1); down: a routine decides", gone);
        Assert.Contains("world:     ended by a routine that jumped back to the main loop: done", gone);
        Assert.Contains("world:   Potty Chamber (2): flag 10, visited, set", gone);

        // the panel says what is open beside its name: the desk once opened, and the fireplace
        // from the start
        Assert.Contains("  writing desk (61), open", opened);
        Assert.Contains("  fireplace (62), open", opened);

        // the panel: the world as it is now, down a column, and under it the last of what was said
        var panel = watch.Panel(46, 60);
        Assert.All(panel, line => Assert.True(line.Text.Length <= 46));
        Assert.True(panel.Count <= 60);
        string[] shown = [.. panel.Select(line => line.Text)];
        Assert.Equal(["4:05 pm   turn 6", "Potty Chamber (2)", "", "Here", "  you (153)", "    love note (74)", "    cloak (82), list 2", "  tiny door (66)", "", "Exits", "  south  to Bedchamber (1)", "  down   a routine decides", "", "Lately"], shown[..14]);
        Assert.Equal([PanelTone.Title, PanelTone.Title, PanelTone.Text, PanelTone.Dim], panel.Take(4).Select(line => line.Tone));
        Assert.Contains(new PanelLine("action 197: go north", PanelTone.Action), panel);
        Assert.Contains(new PanelLine("you (153) moved: from Bedchamber (1) to Potty", PanelTone.Text), panel);
        Assert.DoesNotContain(shown, line => line.StartsWith("here:", StringComparison.Ordinal) || line.StartsWith("exits:", StringComparison.Ordinal));

        // a short panel keeps the top of the world and the end of what was said
        var small = watch.Panel(46, 12);
        Assert.Equal(12, small.Count);
        Assert.Equal("4:05 pm   turn 6", small[0].Text);

        // each command's words are said before its action, each with the parts of speech such a
        // word can be: a full stop, "then", and an "and" with a verb after it each end a command,
        // and a word that isn't in the vocabulary ends the line
        Assert.Equal(
            [
                "world:   words: examine [verb] rat [noun]",
                "world:   words: climb [verb] chimney [noun]",
                "world:   words: open [verb or preposition] desk [adjective or noun]",
                "world:   words: take [verb] note [adjective or noun]",
                "world:   words: i [verb or adjective] .",
                "world:   words: i [verb or adjective] then",
                "world:   words: i [verb or adjective] and",
                "world:   words: take [verb] frotz [not known]",
                "world:   words: ask [verb] rat [noun] about [preposition] sword [noun]",
                "world:   words: n [verb or noun]",
            ],
            lines.Where(line => line.StartsWith("world:   words: ", StringComparison.Ordinal)));
        Assert.Equal("world:   words: examine [verb] rat [noun]", lines[Array.IndexOf(lines, "world:   action 60: examine rat (352)") - 3]);

        // between a command's words and its action: the form of the grammar the words fit, and
        // for each phrase the objects it could be, with where the form let them be looked for.
        // Taking doesn't look in your hands; asking about a thing looks everywhere, and finds
        // the sword, which is in a shop a long way off
        string[] Before(string action) => [.. lines.Take(Array.IndexOf(lines, action)).Reverse().TakeWhile(line => !line.StartsWith("world:   words: ", StringComparison.Ordinal)).Reverse()];
        Assert.Equal(
            ["world:   form 306: take OBJECT, which is action 74", "world:   object \"note\", looked for carried, in the room, in something there: love note (74)"],
            Before("world:   action 74: get love note (74)"));
        Assert.Equal(
            ["world:   form 30: climb OBJECT, which is action 25", "world:   object \"chimney\", looked for in the room, in something there: chimney (185)"],
            Before("world:   action 25: climb chimney (185)"));
        Assert.Equal(
            [
                "world:   form 2: ask OBJECT about OBJECT, which is action 3",
                "world:   second object \"sword\", looked for anywhere at all: lead-plated sword (99)",
                "world:   object \"rat\", looked for in the room: rat (352)",
            ],
            Before("world:   action 3: ask rat (352) about lead-plated sword (99)"));
        Assert.Contains("world:   form 133: inventory, which is action 88", lines);

        // seven commands, ten actions; the exits are said once for each room
        Assert.Equal(10, lines.Count(line => line.StartsWith("world:   action ", StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(line => line.StartsWith("world:   exits: ", StringComparison.Ordinal)));
    }
}

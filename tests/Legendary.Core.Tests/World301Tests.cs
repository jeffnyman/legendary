using Legendary.Core.Programs;
using Legendary.Core.Text;
using Legendary.Core.Worlds;

namespace Legendary.Core.Tests;

/// <summary>
/// <i>Spellcasting 301</i>, read from its files: a program linked by the newer .RTLink, whose
/// overlay manager keeps its thunks outside its own segment, and the game's world in the
/// program's image, which is the game's memory before it has run. Without the game (in
/// LEGENDARY_GAMES, or legend/sc301/game above the tests), the tests pass without checking.
/// </summary>
public sealed class World301Tests
{
    private static RtlinkProgram? Program() =>
        TestData.Game("sc301/game") is { } folder && TestData.File(folder, "s301main.exe") is { } path ? RtlinkProgram.Load(path) : null;

    private static (World World, IReadOnlyList<VocabularyWord> Words)? Load()
    {
        if (Program() is not { } program || TestData.Game("sc301/game") is not { } folder
            || TestData.File(folder, "object.dat") is not { } names || TestData.File(folder, "vocab.dat") is not { } vocabulary)
        {
            return null;
        }

        var memory = program.Image().Memory;
        var world = World.Find(
            at => at < memory.Length ? memory.Span[at] : (byte)0, memory.Length, WorldMap.Spellcasting301, File.ReadAllBytes(names));
        return world is null ? null : (world, Vocabulary.Read(File.ReadAllBytes(vocabulary)));
    }

    [Fact]
    public void The_newer_linkers_program_is_read_as_the_older_ones_is()
    {
        if (Program() is not { } program)
        {
            return;
        }

        Assert.Equal((0x01F6, 0x232A, 0x0956), (program.LinkerVersion, program.StartSegment, program.StartOffset));
        Assert.Equal((228, 51, 67), (program.SegmentCount, program.Records.Count, program.LibraryRecords.Count));
        Assert.Equal(("S301.RTL", "S301MAIN.OVL"), (program.LibraryName, program.OvlName));
        Assert.Equal(5380, program.Records.Sum(record => record.Fixups.Sum(fixups => fixups.Places.Count)));
        Assert.All(program.Records.SelectMany(record => record.Fixups), fixups => Assert.False(fixups.Named));

        // 36 overlays in three places, and 949 thunks that start where the manager's segment ends
        Assert.Equal(36, program.Overlays.Count);
        Assert.Equal([5, 8, 23], program.Overlays.GroupBy(overlay => overlay.LoadSegment).Select(group => group.Count()));
        Assert.Equal(949, program.Thunks.Count);
        Assert.Equal(new ProgramThunk(0x232A, 0x138D, 1, 0x350B, 0x06D3), program.Thunks[0]);
        Assert.Equal(new ProgramThunk(0x232A, 0x3895, 33, 0x3AD9, 0x1F0D), program.Thunks[^1]);
        Assert.All(program.Thunks, thunk => Assert.InRange(
            thunk.TargetSegment, program.Overlays[thunk.Overlay - 1].LoadSegment, program.Overlays[thunk.Overlay - 1].EndSegment - 1));
    }

    [Fact]
    public void The_game_is_found_in_its_own_image()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;
        Assert.Equal(0, world.LoadSegment);
        Assert.True(world.IsThere);

        // as it starts: you, on the Beach, at ten past one, with no turn taken
        Assert.Equal((1, 255, 0, 790), (world.Room, world.Actor, world.Moves, world.Minutes));
        Assert.Null(world.Visit);
        Assert.Empty(world.Timers);
    }

    [Fact]
    public void Objects_are_kept_as_the_game_befores_are()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;
        Assert.Equal((1, "Beach"), (world.Object(1).Type, world.Object(1).Name));
        var you = world.Object(255);
        Assert.Equal((7, "you", "me"), (you.Type, you.Name, you.SecondName));
        Assert.Equal([1], you.Places);

        // what you start with: a coin in hand, and your glasses and bathing suit on
        Assert.Equal(["coin", "eyeglasses", "bathing suit"], world.Contents(255).Select(number => world.Object(number).Name));
        Assert.Equal((255, 1), (world.State(87).Place, world.State(87).Relation));

        // a flag is the bit the game's table gives it: the trophy case is locked, a container,
        // and can be seen through
        Assert.Equal([15, 18, 24], Enumerable.Range(1, 64).Where(world.State(91).Has));
        Assert.Equal(("locked", "open", "visited", "touched"),
            (world.Map.FlagName(15, 2), world.Map.FlagName(16, 2), world.Map.FlagName(6, 1), world.Map.FlagName(6, 2)));

        for (int number = 1; number <= world.Map.ObjectCount; number++)
        {
            Assert.InRange(world.Object(number).Type, 1, 8);
            Assert.All(world.Contents(number), inside => Assert.Contains(number, world.Object(inside).Places));
        }
    }

    [Fact]
    public void A_rooms_exits_are_its_rows_of_the_one_table()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;

        // Hu Delta Phart: to the Front Lawn, up to Your Room, and down by a routine
        var exits = world.Exits(4);
        Assert.Equal([1, 1, 4, 1], exits.Select(exit => exit.Kind));
        Assert.Equal([5, 3, 0, 5], exits.Select(exit => exit.Room));
        Assert.Empty(world.Exits(1));

        var all = Enumerable.Range(1, world.Map.ObjectCount).SelectMany(world.Exits).ToList();
        Assert.Equal((191, 4, 137), (all.Count(exit => exit.Kind == 1), all.Count(exit => exit.Kind == 2), all.Count(exit => exit.Kind == 4)));
        Assert.All(all.Where(exit => exit.Kind == 2), exit => Assert.Equal(6, world.Object(exit.Door).Type));
    }

    [Fact]
    public void The_vocabulary_and_the_grammar_are_read()
    {
        if (Load() is not { } game)
        {
            return;
        }

        // the vocabulary's size is of its packed text here, and not of the text
        Assert.Equal(1801, game.Words.Count);
        Assert.Equal(("#40", "zmove"), (game.Words[0].Text, game.Words[^1].Text));
        Assert.Equal(303, game.Words.Count(word => word.Synonym != 0));

        var grammar = game.World.ReadGrammar(game.Words);
        Assert.Equal(458, grammar.Forms.Count);
        Assert.Contains("open OBJECT", grammar.Shapes(151));
        Assert.Contains("wear OBJECT", grammar.Shapes(1));
    }
}

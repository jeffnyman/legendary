using Legendary.Core.Programs;
using Legendary.Core.Text;
using Legendary.Core.Worlds;

namespace Legendary.Core.Tests;

/// <summary>
/// <i>Spellcasting 201</i>'s world, read from its program's image as its files have it, which
/// is the game's memory before it has run: with its names from a file, its flags by a table,
/// and its exits in one. Without the game (in LEGENDARY_GAMES, or legend/sc201/game above the
/// tests), the tests pass without checking.
/// </summary>
public sealed class World201Tests
{
    private static (World World, IReadOnlyList<VocabularyWord> Words)? Load(bool named = true)
    {
        if (TestData.Game("sc201/game") is not { } folder || TestData.File(folder, "s201main.exe") is not { } path
            || TestData.File(folder, "object.dat") is not { } names || TestData.File(folder, "vocab.dat") is not { } vocabulary)
        {
            return null;
        }

        var memory = RtlinkProgram.Load(path).Image().Memory;
        var world = World.Find(
            at => at < memory.Length ? memory.Span[at] : (byte)0, memory.Length, WorldMap.Spellcasting201,
            named ? File.ReadAllBytes(names) : default);
        return world is null ? null : (world, Vocabulary.Read(File.ReadAllBytes(vocabulary)));
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

        // as it starts: you, in the Sorcerer's Lab, at 1:35 in the afternoon, with no turn taken
        Assert.Equal((46, 217, 0, 815), (world.Room, world.Actor, world.Moves, world.Minutes));
        Assert.Empty(world.Timers);
        Assert.Null(world.TimerRunning);
    }

    [Fact]
    public void Objects_have_their_names_from_the_file_of_names()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;
        var entrance = world.Object(1);
        Assert.Equal((1, "Main Entrance", null), (entrance.Type, entrance.Name, entrance.SecondName));
        Assert.Equal([356], entrance.Lists);
        Assert.Empty(entrance.Places);

        var you = world.Object(217);
        Assert.Equal((7, "you", "me"), (you.Type, you.Name, you.SecondName));
        Assert.Equal([46], you.Places);

        // a thing has three lists here, and one place
        var statue = world.Object(100);
        Assert.Equal((2, "statue"), (statue.Type, statue.Name));
        Assert.Equal([13], statue.Places);
        Assert.Equal(3, statue.Lists.Count);

        Assert.Equal("rope", world.Object(482).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Object(483));

        // without the file, the objects are there and have no names
        if (Load(named: false) is { } nameless)
        {
            Assert.Equal(("", 1), (nameless.World.Object(1).Name, nameless.World.Object(1).Type));
        }
    }

    [Fact]
    public void A_rooms_contents_are_its_list_followed_to_the_end()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;
        Assert.Equal(
            ["refrigerator", "duct", "trophy", "bedsheet", "your desk", "your bed"],
            world.Contents(4).Select(number => world.Object(number).Name));

        for (int number = 1; number <= world.Map.ObjectCount; number++)
        {
            Assert.InRange(world.Object(number).Type, 1, 8);
            Assert.All(world.Contents(number), inside => Assert.Contains(number, world.Object(inside).Places));
        }
    }

    [Fact]
    public void A_flag_is_the_bit_the_games_table_gives_it()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;

        // the Main Entrance: it gives light, is at the university, and is outdoors. Its bits
        // are 7, 8 and 9, which for a thing are other flags
        Assert.Equal([9, 17, 33], Enumerable.Range(1, 64).Where(world.State(1).Has));
        Assert.Equal([8, 21, 22], Enumerable.Range(1, 64).Where(world.State(100).Has));

        // you are the one person who isn't someone else
        Assert.False(world.State(217).Has(37));
        Assert.True(world.State(218).Has(37));

        Assert.Equal(("open", "locked", "outdoors"), (world.Map.FlagName(13, 2), world.Map.FlagName(14, 2), world.Map.FlagName(33, 1)));
        Assert.Equal(("touched", "visited"), (world.Map.FlagName(6, 2), world.Map.FlagName(6, 1)));
        Assert.Equal((13, 0, 0), (world.State(100).Place, world.State(100).SecondPlace, world.State(100).Relation));
    }

    [Fact]
    public void A_rooms_exits_are_its_rows_of_the_one_table()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;

        // the Main Entrance: two that a numbered routine decides, four to rooms, and out
        var exits = world.Exits(1);
        Assert.Equal([1, 3, 4, 5, 6, 7, 12], exits.Select(exit => exit.Direction));
        Assert.Equal([4, 1, 1, 1, 1, 4, 4], exits.Select(exit => exit.Kind));
        Assert.Equal([0, 6, 19, 14, 3, 0, 0], exits.Select(exit => exit.Room));
        Assert.Equal([0, -1, -1, -1, -1, 1, 0], exits.Select(exit => exit.Routine));

        // Your Room: down and out, to Hu Delta Phart
        Assert.Equal([(10, 3), (12, 3)], world.Exits(4).Select(exit => (exit.Direction, exit.Room)));

        // an exit by a door names the door, which is an object of the type that is in two places
        var byDoor = Enumerable.Range(1, world.Map.ObjectCount).SelectMany(world.Exits).Where(exit => exit.Kind == 2).ToList();
        Assert.Equal(16, byDoor.Count);
        Assert.All(byDoor, exit => Assert.Equal(6, world.Object(exit.Door).Type));
        Assert.All(byDoor, exit => Assert.Equal(1, world.Object(exit.Room).Type));

        Assert.Empty(world.Exits(217));
    }

    [Fact]
    public void The_packed_vocabulary_has_its_words_by_number()
    {
        if (Load() is not { } game)
        {
            return;
        }

        Assert.Equal(1609, game.Words.Count);
        Assert.Equal((1, "'n'", 4), (game.Words[0].Number, game.Words[0].Text, game.Words[0].Parts));
        Assert.Equal(game.Words.Select(word => word.Text).Order(StringComparer.Ordinal), game.Words.Select(word => word.Text));

        // ACTIVATE stands for LIGHT, as a verb; LIGHT stands for nothing
        var activate = game.Words[11];
        var light = game.Words[activate.Synonym - 1];
        Assert.Equal(("activate", "light", 1), (activate.Text, light.Text, activate.SynonymParts));
        Assert.Equal(0, light.Synonym);
        Assert.Equal(264, game.Words.Count(word => word.Synonym != 0));
    }

    [Fact]
    public void The_grammar_is_read_with_its_wider_flags()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var grammar = game.World.ReadGrammar(game.Words);
        Assert.Equal(405, grammar.Forms.Count);
        Assert.Equal(["apply OBJECT", "put on OBJECT", "wear OBJECT"], grammar.Shapes(1));
        Assert.Equal("apply OBJECT to OBJECT", grammar.Shapes(2)[0]);
        Assert.Contains("break OBJECT with OBJECT", grammar.Shapes(14));
        Assert.Contains("open OBJECT", grammar.Shapes(134));
    }
}

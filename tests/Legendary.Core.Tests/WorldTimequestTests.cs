using Legendary.Core.Programs;
using Legendary.Core.Text;
using Legendary.Core.Worlds;

namespace Legendary.Core.Tests;

/// <summary>
/// <i>Timequest</i>'s world, read from its program's image as its files have it, which is the
/// game's memory before it has run: the first game's records with more flags, every room's
/// exits in one table, and a clock for each place in each time. Without the game (in
/// LEGENDARY_GAMES, or legend/tquest/game above the tests), the tests pass without checking.
/// </summary>
public sealed class WorldTimequestTests
{
    private static (World World, byte[] Memory)? Load()
    {
        if (TestData.Game("tquest/game") is not { } folder || TestData.File(folder, "timemain.exe") is not { } path)
        {
            return null;
        }

        // a copy, for the tests that put the player somewhere else
        byte[] memory = Plink86Program.Load(path).Image().Memory.ToArray();
        var world = World.Find(at => at < memory.Length ? memory[at] : (byte)0, memory.Length, WorldMap.Timequest);
        return world is null ? null : (world, memory);
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

        // as it starts: you, at home in Drexler's office, at noon, with no turn taken
        Assert.Equal((483, 485, 0, 720), (world.Room, world.Actor, world.Moves, world.Minutes));
        Assert.Equal("home", world.Visit);
        Assert.Equal("Drexler's office", world.Object(483).Name);
        Assert.Empty(world.Timers);
    }

    [Fact]
    public void Each_place_in_each_time_has_its_year_and_its_own_clock()
    {
        if (Load() is not { } game)
        {
            return;
        }

        // the two bytes that say where and when, in the data
        void Go(byte place, byte time)
        {
            game.Memory[(WorldMap.Timequest.DataSegment * 16) + WorldMap.Timequest.PlaceOffset] = place;
            game.Memory[(WorldMap.Timequest.DataSegment * 16) + WorldMap.Timequest.TimeOffset] = time;
        }

        var world = game.World;
        Go(3, 6);
        Assert.Equal(("Dover, 1588", 12 * 60), (world.Visit, world.Minutes));
        Go(5, 1);
        Assert.Equal("Rome, 44 BC", world.Visit);
        Go(3, 0);
        Assert.Equal(("Dover, 1361 BC", 8 * 60), (world.Visit, world.Minutes));
        Go(1, 8);
        Assert.Equal(("Peking, 1940", (17 * 60) + 40), (world.Visit, world.Minutes));
        Go(6, 9);
        Assert.Equal(("home", 12 * 60), (world.Visit, world.Minutes));
    }

    [Fact]
    public void Objects_have_the_first_games_records_with_more_flags()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;
        var tavern = world.Object(1);
        Assert.Equal((1, "Tavern"), (tavern.Type, tavern.Name));
        Assert.Equal(["sailors", "innkeeper"], world.Contents(1).Take(2).Select(number => world.Object(number).Name));

        // the tankard is the innkeeper's, and can be taken; the door has a second name
        Assert.Equal((8, "tankard"), (world.Object(59).Type, world.Object(59).Name));
        Assert.Equal(14, world.State(59).Place);
        Assert.Equal([8, 12], Enumerable.Range(1, 64).Where(world.State(59).Has));
        Assert.Equal(("bedroom door", "door"), (world.Object(55).Name, world.Object(55).SecondName));

        // a place's flags are four bytes: the Tavern has light, which is flag 23
        Assert.Equal([9, 23], Enumerable.Range(1, 64).Where(world.State(1).Has));
        Assert.Equal(("has light", "lit", "visited", "touched"),
            (world.Map.FlagName(23, 1), world.Map.FlagName(23, 2), world.Map.FlagName(2, 1), world.Map.FlagName(2, 2)));

        for (int number = 1; number <= world.Map.ObjectCount; number++)
        {
            Assert.InRange(world.Object(number).Type, 1, 8);
            Assert.All(world.Contents(number), inside => Assert.Contains(number, world.Object(inside).Places));
        }
    }

    [Fact]
    public void A_rooms_exits_are_its_rows_of_the_one_table_in_five_kinds()
    {
        if (Load() is not { } game)
        {
            return;
        }

        var world = game.World;

        // the Landing: a routine, a door, and three to the Tavern
        var exits = world.Exits(2);
        Assert.Equal([4, 2, 1, 1, 1], exits.Select(exit => exit.Kind));
        Assert.Equal((3, 55), (exits[1].Room, exits[1].Door));
        Assert.Equal(1, exits[0].Routine);

        // the Rocky shore: south only says something
        var south = world.Exits(8).Single(exit => exit.Direction == 5);
        Assert.Equal(3, south.Kind);
        Assert.StartsWith("Persons with a strong death wish", south.Message, StringComparison.Ordinal);

        // an exit a byte of the data opens: shut as the game starts, and open when the byte is set
        var all = Enumerable.Range(1, world.Map.ObjectCount).SelectMany(world.Exits).ToList();
        Assert.Equal((125, 5, 16, 121, 27), (all.Count(exit => exit.Kind == 1), all.Count(exit => exit.Kind == 2),
            all.Count(exit => exit.Kind == 3), all.Count(exit => exit.Kind == 4), all.Count(exit => exit.Kind == 5)));
        Assert.All(all.Where(exit => exit.Kind == 5), exit => Assert.False(exit.Open));
        Assert.All(all.Where(exit => exit.Kind == 5), exit => Assert.InRange(exit.Room, 1, world.Map.ObjectCount));
        int room = Enumerable.Range(1, world.Map.ObjectCount).First(number => world.Exits(number).Any(exit => exit.Kind == 5 && exit.Room == 277));
        game.Memory[(WorldMap.Timequest.DataSegment * 16) + 0x499A] = 1;
        Assert.Contains(world.Exits(room), exit => exit is { Kind: 5, Room: 277, Open: true });
    }

    [Fact]
    public void The_grammar_is_the_first_games_kind()
    {
        if (Load() is not { } game || TestData.Game("tquest/game") is not { } folder || TestData.File(folder, "vocab.dat") is not { } vocabulary)
        {
            return;
        }

        var words = Vocabulary.Read(File.ReadAllBytes(vocabulary));
        Assert.Equal(1296, words.Count);
        var grammar = game.World.ReadGrammar(words);
        Assert.Equal(354, grammar.Forms.Count);
        Assert.Contains("open OBJECT", grammar.Shapes(94));
        Assert.Contains("wait", grammar.Shapes(140));
    }
}

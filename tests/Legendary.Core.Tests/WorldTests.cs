using Legendary.Core.Programs;
using Legendary.Core.Worlds;

namespace Legendary.Core.Tests;

/// <summary>
/// A game's world, read from the program's image as its file has it, which is the game's
/// memory before it has run: <i>Spellcasting 101</i> as it starts. Without the game (in
/// LEGENDARY_GAMES, or legend/sc101/game above the tests), the tests pass without checking.
/// </summary>
public sealed class WorldTests
{
    private static World? Load()
    {
        if (TestData.Game("sc101/game") is not { } folder || TestData.File(folder, "s101main.exe") is not { } path)
        {
            return null;
        }

        var memory = Plink86Program.Load(path).Image().Memory;
        return World.Find(at => at < memory.Length ? memory.Span[at] : (byte)0, memory.Length, WorldMap.Spellcasting101);
    }

    [Fact]
    public void The_game_is_found_in_its_own_image_at_segment_zero()
    {
        if (Load() is not { } world)
        {
            return;
        }

        Assert.Equal(0, world.LoadSegment);
        Assert.True(world.IsThere);

        // and before it has run, no timer is going
        Assert.Empty(world.Timers);
        Assert.Null(world.TimerRunning);
    }

    [Fact]
    public void It_isnt_found_where_it_isnt()
    {
        byte[] empty = new byte[0x60000];
        Assert.Null(World.Find(at => at < empty.Length ? empty[at] : (byte)0, empty.Length, WorldMap.Spellcasting101));
    }

    [Fact]
    public void Objects_have_their_names_types_and_places()
    {
        if (Load() is not { } world)
        {
            return;
        }

        var bedchamber = world.Object(1);
        Assert.Equal((1, "Bedchamber", null), (bedchamber.Type, bedchamber.Name, bedchamber.SecondName));
        Assert.Equal([352], bedchamber.Lists);
        Assert.Empty(bedchamber.Places);

        // you, object 153, are in the Bedchamber, on its first list, with the pile of hay next
        var you = world.Object(153);
        Assert.Equal((7, "you", "me"), (you.Type, you.Name, you.SecondName));
        Assert.Equal([1], you.Places);
        Assert.Equal((0, 72), (you.Relation, you.Next));
        Assert.Equal([0, 82, 0], you.Lists);

        // the chimney is of the type that is in two places, and starts in neither
        var chimney = world.Object(185);
        Assert.Equal((6, "chimney"), (chimney.Type, chimney.Name));
        Assert.Equal([0, 0], chimney.Places);

        Assert.Equal("Potty Chamber", world.Object(2).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Object(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Object(428));
    }

    [Fact]
    public void A_rooms_contents_are_its_list_followed_to_the_end()
    {
        if (Load() is not { } world)
        {
            return;
        }

        Assert.Equal([352, 153, 72, 65, 64, 62, 61, 60], world.Contents(1));
        Assert.Equal([82], world.Contents(153));
        Assert.Equal(["rat", "you", "pile of hay"], world.Contents(1).Take(3).Select(number => world.Object(number).Name));
    }

    [Fact]
    public void Every_object_can_be_read_and_every_list_ends()
    {
        if (Load() is not { } world)
        {
            return;
        }

        for (int number = 1; number <= world.Map.ObjectCount; number++)
        {
            var found = world.Object(number);
            Assert.InRange(found.Type, 1, 8);
            Assert.All(world.Contents(number), inside => Assert.Contains(number, world.Object(inside).Places));
        }
    }

    [Fact]
    public void A_rooms_exits_are_read_with_where_each_goes()
    {
        if (Load() is not { } world)
        {
            return;
        }

        // the Bedchamber's: north to the Potty Chamber, west to the Alley by the door, three
        // that only say something, and up, which a routine decides
        var exits = world.Exits(1);
        Assert.Equal([1, 3, 7, 5, 10, 9], exits.Select(exit => exit.Direction));
        Assert.Equal([1, 3, 2, 3, 3, 4], exits.Select(exit => exit.Kind));
        Assert.Equal((2, 0), (exits[0].Room, exits[0].Door));
        Assert.Equal((4, 60), (exits[2].Room, exits[2].Door));
        Assert.Equal("If you want to enter the fireplace, then say so.", exits[1].Message);
        Assert.Equal("If you want to jump out the window, then say so.", exits[3].Message);
        Assert.Null(exits[0].Message);
        Assert.Equal((0, 0, null), (exits[5].Room, exits[5].Door, exits[5].Message));

        // what isn't a room has none
        Assert.Empty(world.Exits(153));
        Assert.Equal("north", world.Map.Directions[exits[0].Direction]);
    }

    [Fact]
    public void An_objects_state_is_its_flags_and_where_it_is()
    {
        if (Load() is not { } world)
        {
            return;
        }

        // you: flags 5 and 7, in the Bedchamber on its first list
        var you = world.State(153);
        Assert.Equal((0x50ul, 1, 0, 0), (you.Flags, you.Place, you.SecondPlace, you.Relation));
        Assert.True(you.Has(5) && you.Has(7));
        Assert.False(you.Has(6) || you.Has(0) || you.Has(65));

        // a thing has six bytes of flags: the fireplace's are 7, 12, 14, 15 and 21
        var fireplace = world.State(62);
        Assert.Equal(0x106840ul, fireplace.Flags);
        Assert.Equal([7, 12, 14, 15, 21], Enumerable.Range(1, 64).Where(fireplace.Has));

        // a flag has a name, which for a place or a person can be another
        Assert.Equal("open", world.Map.FlagName(12, 2));
        Assert.Equal(("touched", "visited"), (world.Map.FlagName(10, 2), world.Map.FlagName(10, 1)));
        Assert.Equal(("gives light", "gives light", "female"), (world.Map.FlagName(9, 1), world.Map.FlagName(9, 2), world.Map.FlagName(9, 7)));
        Assert.Null(world.Map.FlagName(18, 2));

        // a room is nowhere, and on no list
        Assert.Equal((0x100ul, 0, 0, -1), (world.State(1).Flags, world.State(1).Place, world.State(1).SecondPlace, world.State(1).Relation));

        // and every object's state agrees with the object read whole
        for (int number = 1; number <= world.Map.ObjectCount; number++)
        {
            var whole = world.Object(number);
            var state = world.State(number);
            Assert.Equal(whole.Places.Count > 0 ? whole.Places[0] : 0, state.Place);
            Assert.Equal(whole.Relation, state.Relation);
        }
    }
}

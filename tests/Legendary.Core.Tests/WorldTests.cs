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
}

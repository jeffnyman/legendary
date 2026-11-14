using System.Globalization;
using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player;

/// <summary>
/// The game's world, said as it changes: the time, the turn and the room, what is in the room
/// and what those things hold, and each action as the game performs it. It reads the game's
/// memory and writes nothing to it, so the game is played as without it.
/// </summary>
/// <remarks>
/// It looks once a frame, which is often enough to see a turn and not always enough to see
/// everything inside one: a command the game turns into another shows as the last of them.
/// </remarks>
internal sealed class WorldWatch(Machine machine, WorldMap map, TextWriter output)
{
    // how many looks to let pass between searches for a game that isn't there yet
    private const int SearchEvery = 30;

    private World? _world;
    private int _looks;
    private string _place = "";
    private string _here = "";
    private string _action = "";

    /// <summary>Looks at the game, and says what has changed since the last look.</summary>
    public void Look()
    {
        if (_world is { IsThere: false })
        {
            // the game ended, or a saved state without it was loaded
            _world = null;
        }

        if (_world is null)
        {
            if (_looks++ % SearchEvery != 0 || (_world = World.Find(Read, 0xA0000, map)) is null)
            {
                return;
            }

            output.WriteLine($"world: the game is in memory, loaded at {_world.LoadSegment:X4}h, with {map.ObjectCount} objects");
        }

        var world = _world;
        int room = world.Room;
        if (room < 1 || room > map.ObjectCount)
        {
            // the game is loaded and hasn't put the player anywhere yet
            return;
        }

        string place = $"world: {Time(world.Minutes)}, turn {world.Moves}, in {Name(world, room)}";
        string here = $"world:   here: {Inside(world, room, depth: 0)}";
        if (place != _place || here != _here)
        {
            output.WriteLine(_place = place);
            output.WriteLine(_here = here);
        }

        if (world.Action != 0)
        {
            string action = $"world:   action {world.Action}{On(world, " on ", world.ActionObject)}{On(world, " with ", world.ActionSecondObject)}";
            if (action != _action)
            {
                output.WriteLine(_action = action);
            }
        }
    }

    /// <summary>A time of day as the game's own status line has it, as in <c>3:35 pm</c>.</summary>
    internal static string Time(int minutes)
    {
        int hour = minutes / 60 % 24;
        return string.Create(CultureInfo.InvariantCulture, $"{((hour + 11) % 12) + 1}:{minutes % 60:00} {(hour < 12 ? "am" : "pm")}");
    }

    private byte Read(int linear) => machine.Read((uint)linear);

    private static string Name(World world, int number) => $"{world.Object(number).Name} ({number})";

    private static string On(World world, string word, int number) =>
        number >= 1 && number <= world.Map.ObjectCount ? word + Name(world, number) : "";

    // what an object holds, and in brackets what each of those holds, a few deep
    private static string Inside(World world, int number, int depth)
    {
        var contents = world.Contents(number);
        if (contents.Count == 0)
        {
            return depth == 0 ? "nothing" : "";
        }

        return string.Join(", ", contents.Select(inside =>
        {
            string held = depth < 3 ? Inside(world, inside, depth + 1) : "";
            return held.Length == 0 ? Name(world, inside) : $"{Name(world, inside)} [{held}]";
        }));
    }
}

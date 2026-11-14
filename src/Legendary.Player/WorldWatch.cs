using System.Globalization;
using Legendary.Core.Text;
using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player;

/// <summary>
/// The game's world, said as it changes: the time, the turn and the room, what is in the room
/// and what those things hold, and each action as the game performs it. It reads the game's
/// memory and writes nothing to it, so the game is played as without it.
/// </summary>
/// <param name="machine">The PC the game is running on.</param>
/// <param name="map">Where the game keeps its world.</param>
/// <param name="output">Where to say it.</param>
/// <param name="words">
/// The game's vocabulary, to name actions by the commands that make them; without it, or when
/// it isn't this game's, they are said by number alone.
/// </param>
/// <remarks>
/// The room and what is in it are looked at once a frame. The actions aren't: the machine says
/// each time the game reaches its routine that performs one, so every action is said, the ones
/// a routine performs inside another's, set in under it, and the ones the game makes of a
/// command as well as the command.
/// </remarks>
internal sealed class WorldWatch(Machine machine, WorldMap map, TextWriter output, IReadOnlyList<VocabularyWord>? words = null)
{
    // how many looks to let pass between searches for a game that isn't there yet
    private const int SearchEvery = 30;

    private World? _world;
    private Grammar? _grammar;
    private int _looks;
    private string _place = "";
    private string _here = "";

    /// <summary>Looks at the game, and says what has changed since the last look.</summary>
    public void Look()
    {
        if (_world is { IsThere: false })
        {
            // the game ended, or a saved state without it was loaded
            _world = null;
            machine.Cpu.WatchNothing();
        }

        if (_world is null)
        {
            if (_looks++ % SearchEvery != 0 || (_world = World.Find(Read, 0xA0000, map)) is null)
            {
                return;
            }

            output.WriteLine($"world: the game is in memory, loaded at {_world.LoadSegment:X4}h, with {map.ObjectCount} objects");
            machine.Cpu.Watch((uint)_world.PerformAddress, Performing);
            try
            {
                _grammar = words is null ? null : _world.ReadGrammar(words);
            }
            catch (InvalidDataException)
            {
                // the words aren't this game's: its actions go by number
                _grammar = null;
            }
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
    }

    // the game is at its routine that performs an action, about to: which, and how deep
    private void Performing()
    {
        // other code can be at the address before the game is loaded there, or after it has gone
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var cpu = machine.Cpu;
        var (action, first, second) = world.PerformArguments((cpu.Ss * 16) + (ushort)cpu.Esp);
        string inside = new(' ', 2 * Math.Clamp(world.PerformDepth, 0, 8));
        output.WriteLine($"world:   {inside}action {action}: {Said(world, action, first, second)}");
    }

    /// <summary>A time of day as the game's own status line has it, as in <c>3:35 pm</c>.</summary>
    internal static string Time(int minutes)
    {
        int hour = minutes / 60 % 24;
        return string.Create(CultureInfo.InvariantCulture, $"{((hour + 11) % 12) + 1}:{minutes % 60:00} {(hour < 12 ? "am" : "pm")}");
    }

    private byte Read(int linear) => machine.Read((uint)linear);

    private static string Name(World world, int number) => $"{world.Object(number).Name} ({number})";

    // the action as a command that makes it, with its objects where the command has them:
    // "examine rat (352)". Without a grammar, or for an action no command makes, its objects alone
    private string Said(World world, int action, int first, int second)
    {
        int[] objects = [.. new[] { first, second }.TakeWhile(number => number >= 1 && number <= map.ObjectCount)];
        if (_grammar?.Shape(action, objects.Length) is not { } shape)
        {
            return objects.Length == 0 ? "no command makes it" : string.Join(", ", objects.Select(number => Name(world, number)));
        }

        int next = 0;
        return string.Join(' ', shape.Split(' ').Select(part =>
            part != Grammar.Object ? part : next < objects.Length ? Name(world, objects[next++]) : "something"));
    }

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

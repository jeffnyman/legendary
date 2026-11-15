using System.Globalization;
using Legendary.Core.Text;
using Legendary.Core.Worlds;
using Legendary.Pc;

namespace Legendary.Player;

/// <summary>
/// The game's world, said as it changes: the time, the turn and the room, what is in the room
/// and what those things hold, the room's exits, each action as the game performs it, and
/// what the action changed: every object, anywhere in the game, that moved or had a flag set
/// or cleared. It reads the game's
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
    private string _exits = "";
    private ObjectState[] _states = [];

    /// <summary>Looks at the game, and says what has changed since the last look.</summary>
    public void Look()
    {
        if (_world is { IsThere: false })
        {
            // the game ended, or a saved state without it was loaded
            _world = null;
            _states = [];
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
            machine.Cpu.Watch((uint)_world.JumpBackAddress, JumpedBack);
            foreach (var point in map.Answers)
            {
                machine.Cpu.Watch((uint)_world.AnswerAddress(point), () => Answered(point));
            }
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

        SayChanges(world);
        string place = $"world: {Time(world.Minutes)}, turn {world.Moves}, in {Name(world, room)}";
        string here = $"world:   here: {Inside(world, room, depth: 0)}";
        string exits = $"world:   exits: {Exits(world, room)}";

        // each when it changes: the first every turn, the others when something comes or goes
        // and when the room is another
        if (place != _place)
        {
            output.WriteLine(_place = place);
        }

        if (here != _here)
        {
            output.WriteLine(_here = here);
        }

        if (exits != _exits)
        {
            output.WriteLine(_exits = exits);
        }
    }

    // every object as it is now against how it was at the last look: what moved, and which
    // flags changed. The first look only remembers
    private void SayChanges(World world)
    {
        var now = new ObjectState[map.ObjectCount];
        for (int number = 1; number <= now.Length; number++)
        {
            now[number - 1] = world.State(number);
        }

        for (int number = 1; number <= _states.Length; number++)
        {
            var (was, state) = (_states[number - 1], now[number - 1]);
            if (was == state)
            {
                continue;
            }

            if ((was.Place, was.SecondPlace, was.Relation) != (state.Place, state.SecondPlace, state.Relation))
            {
                output.WriteLine($"world:   {Name(world, number)} moved: from {Where(world, was)} to {Where(world, state)}");
            }

            for (int flag = 1; flag <= 64; flag++)
            {
                if (was.Has(flag) != state.Has(flag))
                {
                    output.WriteLine($"world:   {Name(world, number)}: flag {flag} {(state.Has(flag) ? "set" : "cleared")}");
                }
            }
        }

        _states = now;
    }

    // where an object is: what it's in and on which of that's lists, or the two it's between
    private string Where(World world, ObjectState state)
    {
        string In(int number) => number >= 1 && number <= map.ObjectCount ? Name(world, number) : "nowhere";
        return state.SecondPlace != 0 ? $"{In(state.Place)} and {In(state.SecondPlace)}"
            : state.Relation > 0 ? $"{In(state.Place)}, its list {state.Relation + 1}"
            : In(state.Place);
    }

    // a room's exits, each by its direction: where it goes, what is said, or that a routine decides
    private string Exits(World world, int room)
    {
        var exits = world.Exits(room);
        return exits.Count == 0 ? "none" : string.Join("; ", exits.Select(exit => DirectionName(exit.Direction) + exit.Kind switch
        {
            1 => $" to {Room(exit.Room)}",
            2 => $" to {Room(exit.Room)} by {Room(exit.Door)}",
            3 => $": \"{exit.Message}\"",
            4 => ": a routine decides",
            _ => string.Create(CultureInfo.InvariantCulture, $": an exit of kind {exit.Kind}"),
        }));

        string Room(int number) => number >= 1 && number <= map.ObjectCount ? Name(world, number) : $"object {number}";
    }

    private string DirectionName(int direction) =>
        direction >= 1 && direction < map.Directions.Count ? map.Directions[direction] : string.Create(CultureInfo.InvariantCulture, $"direction {direction}");

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

    // the main loop's setjmp has returned: by a routine's longjmp, when AX isn't 0. Such an
    // action is never answered, because the routine that jumped didn't go back to who asked it
    private void JumpedBack()
    {
        if (_world is not { IsThere: true })
        {
            return;
        }

        switch ((ushort)machine.Cpu.Eax)
        {
            case 0:
                break;
            case 1:
                output.WriteLine("world:     given up by a routine, which starts over with another action");
                break;
            default:
                output.WriteLine("world:     ended by a routine that jumped back to the main loop: done");
                break;
        }
    }

    // a routine the game asked about the action has answered: said when it's the answer that
    // ends the asking, which is any but zero, and for the action's own routine whatever it is
    private void Answered(AnswerPoint point)
    {
        if (_world is not { IsThere: true } world)
        {
            return;
        }

        var cpu = machine.Cpu;
        int answer = (ushort)cpu.Eax;
        if (answer == 0 && point.Asked != Asked.Action)
        {
            return;
        }

        string who;
        if (point.Asked == Asked.Object)
        {
            var (number, why) = world.AskedArguments((cpu.Ss * 16) + (ushort)cpu.Esp);
            who = number >= 1 && number <= map.ObjectCount ? $"{Name(world, number)}, asked as {Why(why)}" : $"object {number}";
        }
        else
        {
            who = point.Asked == Asked.Before ? "the routine before the action's" : "the action's own routine";
        }

        string inside = new(' ', 2 * Math.Clamp(world.PerformDepth, 0, 8));
        output.WriteLine($"world:   {inside}answered by {who}: {answer}");
    }

    /// <summary>Why an object's routine is asked about an action, from the number it's asked with.</summary>
    internal static string Why(int why) => why switch
    {
        1 => "where it happens",
        2 => "the actor",
        3 => "what the second object is in",
        4 => "the second object",
        5 => "what the object is in",
        6 => "the object",
        _ => string.Create(CultureInfo.InvariantCulture, $"{why}"),
    };

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
        if (action == map.GoAction && world.Direction != 0)
        {
            // going has no object: where to is a direction, which the game keeps beside the action
            return $"go {DirectionName(world.Direction)}";
        }

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

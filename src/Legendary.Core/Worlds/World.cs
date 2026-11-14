using System.Text;
using Legendary.Core.Text;

namespace Legendary.Core.Worlds;

/// <summary>An object as a game has it at one moment: a room, a thing, a person.</summary>
/// <param name="Number">Its number, from 1.</param>
/// <param name="Type">Its type, which says what its record holds.</param>
/// <param name="Name">Its name.</param>
/// <param name="SecondName">Its second name, when it has one.</param>
/// <param name="Places">The objects it's in; none for a type that is nowhere.</param>
/// <param name="Relation">Which of its parent's lists it's on, or -1 for a type on none.</param>
/// <param name="Next">The next object on that list, or 0.</param>
/// <param name="Lists">The first object on each of its own lists, 0 for an empty one.</param>
public sealed record WorldObject(
    int Number, int Type, string Name, string? SecondName, IReadOnlyList<int> Places, int Relation, int Next, IReadOnlyList<int> Lists);

/// <summary>
/// A game's world, read from its memory as it is at the moment of asking: where the player is,
/// what time it is, what was last done, and the tree its objects are in. Nothing is kept, so
/// what it says is what the game has now, and nothing is written.
/// </summary>
/// <remarks>
/// The memory is a PC's, by linear address, with the game's program loaded at some segment. A
/// program's image as its file has it is the same thing loaded at segment zero.
/// </remarks>
public sealed class World
{
    // Microsoft C's notice, 8 bytes into the segment DS has
    private static readonly byte[] Notice = Encoding.ASCII.GetBytes("MS Run-Time Library");

    private readonly Func<int, byte> _read;

    /// <summary>A world in a memory, with its program loaded at a segment.</summary>
    public World(Func<int, byte> read, WorldMap map, ushort loadSegment)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(map);
        _read = read;
        Map = map;
        LoadSegment = loadSegment;
    }

    /// <summary>Where the game keeps what.</summary>
    public WorldMap Map { get; }

    /// <summary>The segment the game's program is loaded at.</summary>
    public ushort LoadSegment { get; }

    /// <summary>
    /// Whether the game is there: its count of objects is where the map says, and is the
    /// map's. It isn't before the game has loaded, or once something else has the memory.
    /// </summary>
    public bool IsThere => DataWord(Map.CountOffset) == Map.ObjectCount && Word(Linear(Map.TableSegment, 2)) != 0;

    /// <summary>The room the player is in, an object's number.</summary>
    public int Room => DataWord(Map.RoomOffset);

    /// <summary>Who is acting: the player's own object.</summary>
    public int Actor => DataWord(Map.ActorOffset);

    /// <summary>The turns taken.</summary>
    public int Moves => DataWord(Map.MovesOffset);

    /// <summary>The time of day, in minutes from midnight; the game counts five at a time.</summary>
    public int Minutes => DataWord(Map.ClockOffset) * 5;

    /// <summary>The number of the action last performed, or being performed.</summary>
    public int Action => _read(Linear(Map.ActionSegment, Map.ActionOffset));

    /// <summary>That action's object, or 0.</summary>
    public int ActionObject => Word(Linear(Map.ActionSegment, Map.ObjectOffset));

    /// <summary>That action's second object, or 0.</summary>
    public int ActionSecondObject => Word(Linear(Map.ActionSegment, Map.SecondObjectOffset));

    /// <summary>
    /// Looks through a memory for the game: for Microsoft C's notice at the start of a data
    /// segment, with the game's count of objects where the map has it from there. Null when
    /// the game isn't loaded.
    /// </summary>
    /// <param name="read">The memory, by linear address.</param>
    /// <param name="size">How much of it to look through, in bytes.</param>
    /// <param name="map">The game's map.</param>
    public static World? Find(Func<int, byte> read, int size, WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(map);

        // the notice is 8 bytes into a segment, so only those addresses are looked at
        for (int at = (map.DataSegment * 16) + 8; at + Notice.Length <= size; at += 16)
        {
            if (read(at) != Notice[0] || read(at + 1) != Notice[1])
            {
                continue;
            }

            bool found = true;
            for (int i = 2; i < Notice.Length && found; i++)
            {
                found = read(at + i) == Notice[i];
            }

            if (found)
            {
                var world = new World(read, map, (ushort)((at >> 4) - map.DataSegment));
                if (world.IsThere)
                {
                    return world;
                }
            }
        }

        return null;
    }

    /// <summary>An object, as it is now.</summary>
    /// <exception cref="ArgumentOutOfRangeException">There is no object of that number.</exception>
    public WorldObject Object(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Map.ObjectCount);

        // the table's entry: its type, a byte that's zero, and a far pointer to its record
        int entry = Linear(Map.TableSegment, 6 * (number - 1));
        int type = _read(entry);
        int record = (Word(entry + 4) * 16) + Word(entry + 2);

        // the game's own table says how many places and lists a type has, and its record's size
        int about = Linear(Map.DataSegment, Map.TypesOffset + (3 * type));
        int places = _read(about), lists = _read(about + 1), size = _read(about + 2);

        string name = Text(Word(record + 2), Word(record)) ?? "";
        string? second = Text(Word(record + 6), Word(record + 4));
        if (!Map.Layouts.TryGetValue(type, out var layout) || layout.Size != size)
        {
            return new WorldObject(number, type, name, second, [], -1, 0, []);
        }

        return new WorldObject(
            number, type, name, second,
            layout.Places < 0 ? [] : Words(record + layout.Places, places),
            layout.Relation < 0 ? -1 : _read(record + layout.Relation),
            layout.Next < 0 ? 0 : Word(record + layout.Next),
            layout.Lists < 0 ? [] : Words(record + layout.Lists, lists));
    }

    /// <summary>
    /// What is in an object, on each of its lists in turn: the objects' numbers. An object
    /// that is in two places, as a door is, is on neither's list and isn't found this way.
    /// </summary>
    public IReadOnlyList<int> Contents(int number)
    {
        var found = new List<int>();
        foreach (int first in Object(number).Lists)
        {
            // a list that came back to itself would be a fault of the game's; it isn't followed
            for (int at = first; at >= 1 && at <= Map.ObjectCount && !found.Contains(at); at = Object(at).Next)
            {
                found.Add(at);
            }
        }

        return found;
    }

    /// <summary>
    /// The game's grammar, read from its two tables, with the game's words to say it in. The
    /// tables are the program's own and don't change as the game is played.
    /// </summary>
    /// <exception cref="InvalidDataException">A form doesn't start with a verb: these aren't the game's words.</exception>
    public Grammar ReadGrammar(IReadOnlyList<VocabularyWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var forms = new List<CommandForm>();
        int seconds = Linear(Map.GrammarSegment, Map.SecondsOffset);
        for (int at = Linear(Map.GrammarSegment, Map.FormsOffset); at + 10 <= seconds; at += 10)
        {
            // the verb, the preposition, flags, the two actions, and its entries in the second table
            int verb = Word(at);
            if (verb > words.Count || (verb != 0 && !words[verb - 1].IsVerb))
            {
                throw new InvalidDataException($"The grammar's form {forms.Count + 1} doesn't start with a verb.");
            }

            int start = _read(at + 7), count = _read(at + 8);
            forms.Add(new CommandForm(
                verb, Word(at + 2), _read(at + 4), _read(at + 5), _read(at + 6),
                [.. Enumerable.Range(start, count).Select(index => seconds + (6 * index))
                    .Select(second => new SecondObjectForm(Word(second), _read(second + 2), _read(second + 4)))]));
        }

        return new Grammar(forms, words, Map.ActionNames);
    }

    private int Linear(ushort segment, int offset) => ((LoadSegment + segment) * 16) + offset;

    private int Word(int linear) => _read(linear) | (_read(linear + 1) << 8);

    private int DataWord(ushort offset) => Word(Linear(Map.DataSegment, offset));

    private int[] Words(int linear, int count) => [.. Enumerable.Range(0, count).Select(index => Word(linear + (2 * index)))];

    // a far pointer's text, to its zero; pointers in the game's memory are whole, with the
    // load segment already in them
    private string? Text(int segment, int offset)
    {
        if (segment == 0 && offset == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        int at = (segment * 16) + offset;
        for (byte next; text.Length < 80 && (next = _read(at)) != 0; at++)
        {
            text.Append((char)next);
        }

        return text.ToString();
    }
}

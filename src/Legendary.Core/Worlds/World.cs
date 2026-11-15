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
/// What of an object the game changes as it's played: its flags, where it is, and which of
/// that place's lists it is on.
/// </summary>
/// <param name="Flags">Its flags, flag 1 the lowest bit.</param>
/// <param name="Place">The object it's in, or 0; for one in two places, the first.</param>
/// <param name="SecondPlace">The second object it's in, for one in two places; otherwise 0.</param>
/// <param name="Relation">Which of its place's lists it's on, or -1 for a type on none.</param>
public readonly record struct ObjectState(ulong Flags, int Place, int SecondPlace, int Relation)
{
    /// <summary>Whether a flag is set; they are numbered from 1.</summary>
    public bool Has(int flag) => flag is >= 1 and <= 64 && (Flags >> (flag - 1) & 1) != 0;
}

/// <summary>A way out of a room.</summary>
/// <param name="Direction">The direction's number.</param>
/// <param name="Kind">
/// What kind of exit: 1 to a room, 2 to a room by a door, 3 a message said in place of going,
/// 4 a routine that decides.
/// </param>
/// <param name="Room">For kinds 1 and 2, the room it goes to; otherwise 0.</param>
/// <param name="Door">For kind 2, the door it goes by; otherwise 0.</param>
/// <param name="Message">For kind 3, what is said; otherwise null.</param>
public sealed record WorldExit(int Direction, int Kind, int Room, int Door, string? Message);

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
    /// Where the routine that performs an action starts, a linear address: the program is
    /// there each time it is about to perform one.
    /// </summary>
    public int PerformAddress => Linear(Map.PerformSegment, Map.PerformOffset);

    /// <summary>How many actions are being performed, one inside another; 0 between them.</summary>
    public int PerformDepth => DataWord(Map.DepthOffset);

    /// <summary>
    /// The action about to be performed and its objects, for a program that is at
    /// <see cref="PerformAddress"/>: the routine's arguments, read from its stack, where they
    /// are above the far address it returns to.
    /// </summary>
    /// <param name="stack">The linear address of the top of the stack, SS:SP.</param>
    public (int Action, int Object, int SecondObject) PerformArguments(int stack) =>
        (_read(stack + 4), Word(stack + 6), Word(stack + 8));

    /// <summary>
    /// Where the main loop's <c>setjmp</c> returns, a linear address: the program is there,
    /// with something other than 0 in AX, when a routine has jumped out of an action.
    /// </summary>
    public int JumpBackAddress => Linear(Map.PerformSegment, Map.JumpBackOffset);

    /// <summary>The linear address of one of the map's places where a routine has answered.</summary>
    public int AnswerAddress(AnswerPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return Linear(Map.PerformSegment, point.Offset);
    }

    /// <summary>
    /// What an object's routine was asked with, for a program at an <see cref="AnswerPoint"/>
    /// of an object's: the object, and the number that says why it was asked, the call's
    /// arguments, still on the stack.
    /// </summary>
    /// <param name="stack">The linear address of the top of the stack, SS:SP.</param>
    public (int Object, int Why) AskedArguments(int stack) => (Word(stack), Word(stack + 2));

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
    /// What of an object changes as the game is played, read without its names: cheap enough
    /// to ask of every object every frame.
    /// </summary>
    public ObjectState State(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Map.ObjectCount);
        int entry = Linear(Map.TableSegment, 6 * (number - 1));
        int type = _read(entry);
        int record = (Word(entry + 4) * 16) + Word(entry + 2);
        if (!Map.Layouts.TryGetValue(type, out var layout))
        {
            return new ObjectState(0, 0, 0, -1);
        }

        ulong flags = 0;
        for (int index = 0; index < layout.FlagBytes; index++)
        {
            flags |= (ulong)_read(record + 8 + index) << (8 * index);
        }

        // how many places a type has is the game's to say
        int places = layout.Places < 0 ? 0 : _read(Linear(Map.DataSegment, Map.TypesOffset + (3 * type)));
        return new ObjectState(
            flags,
            places > 0 ? Word(record + layout.Places) : 0,
            places > 1 ? Word(record + layout.Places + 2) : 0,
            layout.Relation < 0 ? -1 : _read(record + layout.Relation));
    }

    /// <summary>A room's exits, in the order its record has them; none for what isn't a room.</summary>
    public IReadOnlyList<WorldExit> Exits(int room)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(room, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(room, Map.ObjectCount);
        int entry = Linear(Map.TableSegment, 6 * (room - 1));
        int record = (Word(entry + 4) * 16) + Word(entry + 2);
        if (!Map.Layouts.TryGetValue(_read(entry), out var layout) || layout.Exits < 0)
        {
            return [];
        }

        // six bytes each: a direction, a kind, and a far pointer to what the kind needs
        var exits = new List<WorldExit>();
        int rows = (Word(record + layout.Exits + 2) * 16) + Word(record + layout.Exits);
        for (int row = rows, end = rows + (6 * _read(record + layout.ExitCount)); row < end; row += 6)
        {
            int kind = _read(row + 1), segment = Word(row + 4), offset = Word(row + 2);
            int at = (segment * 16) + offset;
            exits.Add(new WorldExit(
                _read(row), kind,
                kind is 1 or 2 ? Word(at) : 0,
                kind == 2 ? Word(at + 2) : 0,
                kind == 3 ? Text(segment, offset, 200) : null));
        }

        return exits;
    }

    /// <summary>The direction the action of going is to go in: its number.</summary>
    public int Direction => _read(Linear(Map.ActionSegment, Map.DirectionOffset));

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
    private string? Text(int segment, int offset, int most = 80)
    {
        if (segment == 0 && offset == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        int at = (segment * 16) + offset;
        for (byte next; text.Length < most && (next = _read(at)) != 0; at++)
        {
            text.Append((char)next);
        }

        return text.ToString();
    }
}

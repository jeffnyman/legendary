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

/// <summary>A phrase of a command, and what the parser found it could be.</summary>
/// <param name="All">Whether the phrase is <c>all</c>, with or without a noun after it.</param>
/// <param name="Adjectives">Its adjectives, as words' numbers.</param>
/// <param name="Noun">Its noun, a word's number, or 0 for none.</param>
/// <param name="Objects">The objects it could be, by what they are called and where they are.</param>
public sealed record FoundPhrase(bool All, IReadOnlyList<int> Adjectives, int Noun, IReadOnlyList<int> Objects);

/// <summary>A routine the game has put by to run on a later turn, or on every turn.</summary>
/// <param name="Segment">
/// The routine's segment, as the program has it when loaded at segment zero; 0 in a game
/// whose timers are numbers.
/// </param>
/// <param name="Offset">The routine's offset, or in a game whose timers are numbers, the timer's number.</param>
/// <param name="Count">The turns to go before it runs, or -1 for a routine that runs every turn.</param>
public sealed record WorldTimer(int Segment, int Offset, int Count)
{
    /// <summary>Whether it runs every turn.</summary>
    public bool EveryTurn => Count == -1;

    /// <summary>The segment and offset as one number, as the map's names for the timers have them.</summary>
    public int Key => (Segment << 16) | Offset;
}

/// <summary>A way out of a room.</summary>
/// <param name="Direction">The direction's number.</param>
/// <param name="Kind">
/// What kind of exit: 1 to a room, 2 to a room by a door, 3 a message said in place of going,
/// 4 a routine that decides.
/// </param>
/// <param name="Routine">For kind 4 in a game that numbers such routines, the number; otherwise -1.</param>
/// <param name="Room">For kinds 1, 2 and 5, the room it goes to; otherwise 0.</param>
/// <param name="Door">For kind 2, the door it goes by; otherwise 0.</param>
/// <param name="Message">For kind 3, what is said, and for kind 5 what is said while it's shut, if anything; otherwise null.</param>
/// <param name="Open">For kind 5, an exit a byte of the game's data opens, whether it is open now.</param>
public sealed record WorldExit(int Direction, int Kind, int Room, int Door, string? Message, int Routine = -1, bool Open = false);

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
    private readonly byte[] _names;
    private (int Bit, int Types)[]? _flags;

    /// <summary>A world in a memory, with its program loaded at a segment.</summary>
    /// <param name="read">The memory, by linear address.</param>
    /// <param name="map">The game's map.</param>
    /// <param name="loadSegment">The segment the program is loaded at.</param>
    /// <param name="names">
    /// The file of the objects' names, whole, for a game that keeps them out of its program;
    /// without it such a game's objects have no names.
    /// </param>
    public World(Func<int, byte> read, WorldMap map, ushort loadSegment, ReadOnlyMemory<byte> names = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(map);
        _read = read;
        Map = map;
        LoadSegment = loadSegment;

        // the file is the size of its text, in a word, and then each name and a zero
        var file = names.Span;
        _names = file.Length < 2 ? [] : file.Slice(2, Math.Min(file[0] | (file[1] << 8), file.Length - 2)).ToArray();
    }

    /// <summary>Where the game keeps what.</summary>
    public WorldMap Map { get; }

    /// <summary>The segment the game's program is loaded at.</summary>
    public ushort LoadSegment { get; }

    /// <summary>
    /// Whether the game is there: its count of objects is where the map says, and is the
    /// map's. It isn't before the game has loaded, or once something else has the memory.
    /// </summary>
    public bool IsThere => DataWord(Map.CountOffset) == Map.ObjectCount && (Word(Entry(1) + 2) | Word(Entry(1) + 4)) != 0;

    /// <summary>The room the player is in, an object's number.</summary>
    public int Room => DataWord(Map.RoomOffset);

    /// <summary>Who is acting: the player's own object.</summary>
    public int Actor => DataWord(Map.ActorOffset);

    /// <summary>The turns taken.</summary>
    public int Moves => DataWord(Map.MovesOffset);

    /// <summary>The time of day, in minutes from midnight; the game counts some at a time.</summary>
    public int Minutes => Map.ClocksSegment == 0 ? DataWord(Map.ClockOffset) * Map.ClockStep : (_read(Clock + 2) * 60) + _read(Clock + 3);

    /// <summary>
    /// Where and when the player is, in a game of places visited in several times: the place's
    /// name and the year, as <c>Rome, 44 BC</c>, or <c>home</c>. Null in a game with one place
    /// and time, and when the game's bytes for them say neither.
    /// </summary>
    public string? Visit
    {
        get
        {
            if (Map.PlaceNames is not { } places)
            {
                return null;
            }

            int place = _read(Linear(Map.DataSegment, Map.PlaceOffset)), time = _read(Linear(Map.DataSegment, Map.TimeOffset));
            if (place == places.Count)
            {
                return "home";
            }

            if (place > places.Count || time >= Map.Times)
            {
                return null;
            }

            int year = (short)DataWord((ushort)(Map.YearsOffset + (2 * time)));
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{places[place]}, {Math.Abs(year)}{(year < 0 ? " BC" : "")}");
        }
    }

    // the clock that is running: home's, or the one for the place and time the player is in
    private int Clock
    {
        get
        {
            int place = _read(Linear(Map.DataSegment, Map.PlaceOffset)), time = _read(Linear(Map.DataSegment, Map.TimeOffset));
            return Map.PlaceNames is not { } places || place >= places.Count || time >= Map.Times
                ? Linear(Map.DataSegment, Map.HomeClockOffset)
                : Linear(Map.ClocksSegment, Map.ClocksOffset + (4 * ((Map.Times * place) + time)));
        }
    }

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
    /// <param name="names">The file of the objects' names, for a game that keeps them in one.</param>
    public static World? Find(Func<int, byte> read, int size, WorldMap map, ReadOnlyMemory<byte> names = default)
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
                var world = new World(read, map, (ushort)((at >> 4) - map.DataSegment), names);
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
        int entry = Entry(number);
        int type = _read(entry);
        int record = (Word(entry + 4) * 16) + Word(entry + 2);

        // the game's own table says how many places and lists a type has, and its record's size
        int about = TypeEntry(type);
        int places = _read(about), lists = _read(about + 1), size = _read(about + 2);
        if (!Map.Layouts.TryGetValue(type, out var layout) || layout.Size != size)
        {
            // a record of a size that isn't known still starts with its names, as pointers
            return new WorldObject(number, type, Text(Word(record + 2), Word(record)) ?? "", Text(Word(record + 6), Word(record + 4)), [], -1, 0, []);
        }

        string name = (layout.Names == 4 ? Named(Word(record)) : Text(Word(record + 2), Word(record))) ?? "";
        string? second = layout.Names == 4 ? Named(Word(record + 2)) : Text(Word(record + 6), Word(record + 4));
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
        int entry = Entry(number);
        int type = _read(entry);
        int record = (Word(entry + 4) * 16) + Word(entry + 2);
        if (!Map.Layouts.TryGetValue(type, out var layout))
        {
            return new ObjectState(0, 0, 0, -1);
        }

        ulong bits = 0;
        for (int index = 0; index < layout.FlagBytes; index++)
        {
            bits |= (ulong)_read(record + layout.Names + index) << (8 * index);
        }

        // where the game has a table of flags, a flag is the bit the table gives it, for the
        // types the table gives it to
        ulong flags = bits;
        if (Map.FlagCount > 0)
        {
            flags = 0;
            var table = FlagTable();
            for (int flag = 1; flag < table.Length; flag++)
            {
                if ((table[flag].Types >> (type - 1) & 1) != 0 && (bits >> table[flag].Bit & 1) != 0)
                {
                    flags |= 1UL << (flag - 1);
                }
            }
        }

        // how many places a type has is the game's to say
        int places = layout.Places < 0 ? 0 : _read(TypeEntry(type));
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
        int entry = Entry(room);
        int record = (Word(entry + 4) * 16) + Word(entry + 2);
        if (!Map.Layouts.TryGetValue(_read(entry), out var layout) || (layout.Exits < 0 && layout.ExitStart < 0))
        {
            return [];
        }

        if (layout.ExitStart >= 0)
        {
            return TableExits(Word(record + layout.ExitStart), _read(record + layout.ExitCount));
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

    // a room's rows of the one table of exits, four bytes each: a direction, a kind, and a
    // word that is a room, a door's row in the table of doors, or a routine's number
    private List<WorldExit> TableExits(int first, int count)
    {
        var exits = new List<WorldExit>();
        for (int row = 0; first > 0 && row < count; row++)
        {
            int at = Linear(Map.ExitsSegment, Map.ExitsOffset + (4 * (first - 1 + row)));
            int kind = _read(at + 1), value = Word(at + 2);
            int door = kind == 2 ? Linear(Map.DoorsSegment, Map.DoorsOffset + (4 * value)) : 0;

            // an exit that only says something, and one a byte of the data opens, which has
            // what it says while it's shut: a game has these two tables or neither
            bool texts = Map.ExitTextsSegment != 0;
            int text = kind == 3 && texts ? Linear(Map.ExitTextsSegment, Map.ExitTextsOffset + (4 * value)) : 0;
            int opened = kind == 5 && texts ? Linear(Map.ExitVariablesSegment, Map.ExitVariablesOffset + (8 * value)) : 0;
            exits.Add(new WorldExit(
                _read(at), kind,
                kind == 1 ? value : kind == 2 ? Word(door) : opened != 0 ? Word(opened) : 0,
                kind == 2 ? Word(door + 2) : 0,
                text != 0 ? Text(Word(text + 2), Word(text), 200) : opened != 0 ? Text(Word(opened + 6), Word(opened + 4), 200) : null,
                kind == 4 ? value : -1,
                opened != 0 && _read(Linear(Map.DataSegment, Word(opened + 2))) != 0));
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

            // where the flags are a word, what follows them is a byte further on
            int wide = Map.WideGrammar ? 1 : 0;
            int start = _read(at + 7 + wide), count = _read(at + 8 + wide);
            forms.Add(new CommandForm(
                verb, Word(at + 2), _read(at + 4), _read(at + 5 + wide), _read(at + 6 + wide),
                [.. Enumerable.Range(start, count).Select(index => seconds + (6 * index))
                    .Select(second => new SecondObjectForm(Word(second), _read(second + 2), _read(second + 4 + wide)))]));
        }

        return new Grammar(forms, words, Map.ActionNames);
    }

    /// <summary>The linear address of the place where a command's next token has just been taken.</summary>
    public int TokenAddress => Linear(Map.TokenSegment, Map.TokenOffset);

    /// <summary>The same, of the place where the token after a full stop has been looked ahead to.</summary>
    public int AfterStopAddress => Linear(Map.TokenSegment, Map.AfterStopOffset);

    /// <summary>The same, of the place where the token after <c>and</c> has been looked ahead to.</summary>
    public int AfterAndAddress => Linear(Map.TokenSegment, Map.AfterAndOffset);

    /// <summary>
    /// Whether a place where a token has been taken is the gathering routine's now: always,
    /// for a game that keeps the routine in memory, and otherwise when the call just before
    /// the place is to the routine that takes a word off the line, as it is while the
    /// gathering routine's overlay is the one loaded there.
    /// </summary>
    /// <param name="address">The place, a linear address: one of the three the map names.</param>
    public bool IsGathering(int address) =>
        Map.TokenizerOffset == 0
        || (_read(address - 5) == 0x9A && Word(address - 4) == Map.TokenizerOffset && Word(address - 2) == LoadSegment + Map.TokenizerSegment);

    /// <summary>The number of the word the token just taken is, or 0 when it isn't a word.</summary>
    public int TokenWord => Word(Linear(Map.ActionSegment, Map.TokenWordOffset));

    /// <summary>The linear address of the place where the grammar has just been searched for the command's form.</summary>
    public int FormAddress => Linear(Map.PerformSegment, Map.FormOffset);

    /// <summary>
    /// The form the search found, counting from 0 in the grammar; the action it makes of the
    /// command; and how many lots of objects the command has. A form of -1 when none fits.
    /// </summary>
    public (int Form, int Action, int Lots) FormFound
    {
        get
        {
            int form = Word(Linear(Map.ActionSegment, Map.FormIndexOffset));
            return (form == 0xFFFF ? -1 : form, _read(Linear(Map.ActionSegment, Map.FormActionOffset)), _read(Linear(Map.ActionSegment, Map.LotCountOffset)));
        }
    }

    /// <summary>The linear address of the place where the objects a lot of phrases could be have just been found.</summary>
    public int FoundAddress => Linear(Map.FoundSegment ?? Map.PerformSegment, Map.FoundOffset);

    /// <summary>
    /// What was found, for a program that is at <see cref="FoundAddress"/>: which of the
    /// command's lots of phrases it was, 0 for the object's and 1 for the second object's;
    /// where the form let them be looked for; and each phrase with the objects it could be.
    /// </summary>
    /// <param name="stack">The linear address of the top of the stack, SS:SP.</param>
    public (int Lot, int Where, IReadOnlyList<FoundPhrase> Phrases) Found(int stack)
    {
        int Far(int at) => ((Word(at + 2) * 16) + Word(at)) & 0xFFFFF;
        int lot = Far(stack), found = Far(stack + 6), said = Far(lot + 2);
        var phrases = new List<FoundPhrase>();
        for (int phrase = 0; phrase < Math.Min((int)_read(lot), 16); phrase++)
        {
            int entry = found + (6 * phrase), objects = Far(entry + 2), words = said + (0x28 * phrase);
            phrases.Add(new FoundPhrase(
                Word(words) != 0,
                [.. Enumerable.Range(0, Math.Min((int)_read(words + 4), 15)).Select(index => Word(words + 6 + (2 * index)))],
                Word(words + 0x26),
                [.. Enumerable.Range(0, _read(entry)).Select(index => Word(objects + (2 * index)))]));
        }

        return ((lot - Linear(Map.ActionSegment, Map.LotsOffset)) / Map.LotSize, _read(stack + 4), phrases);
    }

    /// <summary>The timers the game has going, in the order it runs them.</summary>
    public IReadOnlyList<WorldTimer> Timers =>
        [.. Enumerable.Range(0, Math.Clamp(DataWord(Map.TimerCountOffset), 0, 16)).Select(index => TimerAt(Linear(Map.ActionSegment, Map.TimersOffset) + (Map.TimerSize * index)))];

    /// <summary>The timer whose routine is being run, or null between them.</summary>
    public WorldTimer? TimerRunning =>
        DataWord(Map.TimerIndexOffset) is var index && index < Math.Clamp(DataWord(Map.TimerCountOffset), 0, 16)
            ? TimerAt(Linear(Map.ActionSegment, Map.TimersOffset) + (Map.TimerSize * index))
            : null;

    /// <summary>The linear address of the routine that starts a timer.</summary>
    public int TimerStartAddress => Linear(Map.TimerSegment, Map.TimerStartOffset);

    /// <summary>The linear address of the routine that stops a timer.</summary>
    public int TimerStopAddress => Linear(Map.TimerSegment, Map.TimerStopOffset);

    /// <summary>The linear address of the call of a timer's routine.</summary>
    public int TimerRunAddress => Linear(Map.TimerSegment, Map.TimerRunOffset);

    /// <summary>
    /// The timer a program at <see cref="TimerStartAddress"/> or <see cref="TimerStopAddress"/>
    /// has been given: the routine's arguments, above the far address it returns to. The count
    /// means something only to the start.
    /// </summary>
    /// <param name="stack">The linear address of the top of the stack, SS:SP.</param>
    public WorldTimer TimerArgument(int stack) =>
        Map.TimerSize == 4 ? new(0, Word(stack + 4) & 0xFF, (short)Word(stack + 6)) : TimerAt(stack + 4);

    // a far pointer and a count, as the table and the stack both have them, or where timers
    // are numbers, the number in a byte and the count
    private WorldTimer TimerAt(int at) =>
        Map.TimerSize == 4
            ? new(0, _read(at), (short)Word(at + 2))
            : new((Word(at + 2) - LoadSegment) & 0xFFFF, Word(at), (short)Word(at + 4));

    // an object's entry in the table of objects
    private int Entry(int number) => Linear(Map.TableSegment, Map.TableOffset + (6 * (number - 1)));

    // a type's entry in the table of types, which can be reckoned from before its segment's start
    private int TypeEntry(int type) => Linear(Map.TypesSegment ?? Map.DataSegment, (ushort)(Map.TypesOffset + (3 * type)));

    // the game's table of flags, read once: it is the program's own and doesn't change
    private (int Bit, int Types)[] FlagTable()
    {
        if (_flags is null)
        {
            _flags = new (int, int)[Map.FlagCount + 1];
            for (int flag = 1; flag <= Map.FlagCount; flag++)
            {
                int at = Linear(Map.FlagsSegment, Map.FlagsOffset + (4 * flag));
                _flags[flag] = (_read(at) & 63, Word(at + 2));
            }
        }

        return _flags;
    }

    // a name from the file of names, by where it starts there, counted from 1
    private string? Named(int start)
    {
        if (start < 1 || start > _names.Length)
        {
            return null;
        }

        int end = Array.IndexOf(_names, (byte)0, start - 1);
        return Encoding.Latin1.GetString(_names, start - 1, (end < 0 ? _names.Length : end) - (start - 1));
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

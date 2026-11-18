namespace Legendary.Core.Worlds;

/// <summary>
/// Where a type of object has what in its record, after the names and flags every record
/// starts with. A field a type doesn't have is -1.
/// </summary>
/// <param name="Size">The record's size, which the game's own table of types must agree with.</param>
/// <param name="Places">The objects it's in, a word each.</param>
/// <param name="Relation">A byte: which of its parent's lists it's on.</param>
/// <param name="Next">The next object on that list.</param>
/// <param name="Lists">The first object on each of its own lists, a word each.</param>
/// <param name="FlagBytes">How many bytes of flags it has, after its names.</param>
/// <param name="ExitCount">A room's: a byte, how many exits it has.</param>
/// <param name="Exits">A room's: a far pointer to its exits, six bytes each.</param>
/// <param name="Names">
/// The size of the two names every record starts with: 8 for two far pointers into the
/// program, or 4 for two words, each where a name starts in the file of names, from 1.
/// </param>
/// <param name="ExitStart">
/// A room's, in a game that keeps every room's exits in one table: the number of its first
/// row there, from 1.
/// </param>
public sealed record RecordLayout(
    int Size, int Places = -1, int Relation = -1, int Next = -1, int Lists = -1, int FlagBytes = 2, int ExitCount = -1, int Exits = -1,
    int Names = 8, int ExitStart = -1);

/// <summary>Whose routine the game has asked about an action it is performing.</summary>
public enum Asked
{
    /// <summary>An object's: the object and why it's asked are the call's arguments.</summary>
    Object,

    /// <summary>The routine that comes before the action's own, for an action that has one.</summary>
    Before,

    /// <summary>The action's own routine, the stock reply, when nothing before it answered.</summary>
    Action,
}

/// <summary>
/// A place in the routine that performs an action where one of the routines it asks has just
/// answered: the instruction after the call, with the answer in AX and the call's arguments
/// still on the stack.
/// </summary>
/// <param name="Offset">Where it is, in the routine's segment.</param>
/// <param name="Asked">Whose routine it was.</param>
public sealed record AnswerPoint(ushort Offset, Asked Asked);

/// <summary>
/// Where a game keeps its world, by the addresses its program has when loaded at segment
/// zero: what the notes found by taking the program apart, for a reader of the game's memory
/// as it runs.
/// </summary>
/// <param name="DataSegment">The segment DS has, with Microsoft C's notice 8 bytes in.</param>
/// <param name="TableSegment">The table of objects: six bytes each, a type and a far pointer.</param>
/// <param name="ObjectCount">How many objects there are.</param>
/// <param name="CountOffset">Where, in the data, the game keeps that count.</param>
/// <param name="TypesOffset">
/// Where, in the data, the table of types would have type 0: three bytes for each type, how
/// many places, how many lists, and its record's size.
/// </param>
/// <param name="RoomOffset">In the data, the room the player is in.</param>
/// <param name="ActorOffset">In the data, who is acting: the player's own object.</param>
/// <param name="MovesOffset">In the data, the turns taken.</param>
/// <param name="ClockOffset">In the data, the time of day, in steps of five minutes.</param>
/// <param name="ActionSegment">The segment with the action being performed and its objects.</param>
/// <param name="ActionOffset">There, the action's number, a byte.</param>
/// <param name="ObjectOffset">There, the action's object.</param>
/// <param name="SecondObjectOffset">There, its second object.</param>
/// <param name="PerformSegment">The segment of the routine that performs an action.</param>
/// <param name="PerformOffset">
/// Where the routine starts. It is called far, with the action and its two objects as its
/// arguments, a word each.
/// </param>
/// <param name="DepthOffset">In the data, how many actions are being performed, one inside another.</param>
/// <param name="Answers">Where, in that routine, each routine it asks has answered, in the order it asks them.</param>
/// <param name="JumpBackOffset">
/// In that routine's segment, where the main loop's <c>setjmp</c> returns, with AX saying how:
/// 0 when the loop has just set it, and otherwise what a routine that jumped back there gave
/// <c>longjmp</c>. 1 is an action given up and started over as another, and more than 1 an
/// action that is done.
/// </param>
/// <param name="GoAction">The action that is going somewhere.</param>
/// <param name="DirectionOffset">In the action's segment, a byte: the direction to go in.</param>
/// <param name="Directions">The directions' names, by number; the first, for 0, is empty.</param>
/// <param name="GrammarSegment">The segment with the grammar's two tables.</param>
/// <param name="FormsOffset">There, the forms a command can take, ten bytes each.</param>
/// <param name="SecondsOffset">
/// There, where the forms end and the ways a form goes on to a second object start, six
/// bytes each.
/// </param>
/// <param name="VocabularyFile">The file with the game's words, which the grammar names by number.</param>
/// <param name="ActionNames">
/// Names for actions the grammar would name badly: going somewhere, which every direction's
/// word makes, would have a direction's name.
/// </param>
/// <param name="TokenSegment">The segment of the routine that gathers a command's tokens.</param>
/// <param name="TokenOffset">
/// Where, in that routine, the routine that takes the next word off the line has just given
/// it the command's next: the instruction after the call, with the token's code in AX.
/// </param>
/// <param name="AfterStopOffset">The same, for the word it looks ahead to after a full stop.</param>
/// <param name="AfterAndOffset">The same, for the word it looks ahead to after <c>and</c>.</param>
/// <param name="TokenWordOffset">In the action's segment, the number of the word the token is.</param>
/// <param name="WordTokens">
/// The codes of the tokens that are a word of the vocabulary, each with the parts of speech
/// such a word can be: the parser's grammar picks among them.
/// </param>
/// <param name="MarkTokens">The codes of the tokens that are a mark, each with the mark.</param>
/// <param name="EndTokens">The codes of the tokens that end a command.</param>
/// <param name="FormOffset">
/// In the performing routine's segment, where the main loop has just searched the grammar for
/// the command's form: the instruction after the call, with more than 0 in AX when one fits.
/// </param>
/// <param name="FormIndexOffset">In the action's segment, which form was found, counting from 0; FFFF for none.</param>
/// <param name="FormActionOffset">In the action's segment, a byte: the action the form makes of the command.</param>
/// <param name="FoundOffset">
/// In the performing routine's segment, where the objects the phrases of one of the command's
/// two lots could be have just been found: the instruction after the call, with the call's
/// arguments still on the stack. They are a far pointer to the lot, in the sentence; where
/// the form lets the objects be looked for; and a far pointer to what was found, six bytes
/// for each phrase, a count and at 2 a far pointer to that many objects.
/// </param>
/// <param name="LotsOffset">In the action's segment, the first of the sentence's two lots of phrases.</param>
/// <param name="LotSize">
/// A lot's size. Its first byte is how many phrases it has, and at 2 is a far pointer to them,
/// 40 bytes each: the word <c>all</c> if the phrase is that, the article, a count of
/// adjectives and the adjectives, and at 26 hex the noun, all as words' numbers.
/// </param>
/// <param name="LotCountOffset">In the action's segment, a byte: how many lots the command has.</param>
/// <param name="TimersOffset">
/// In the action's segment, the table of timers: six bytes each, a far pointer to a routine
/// and a count. A count above zero goes down by one a turn, and at zero the routine is called;
/// a count of -1 is a routine called every turn.
/// </param>
/// <param name="TimerCountOffset">In the data, how many timers the table has.</param>
/// <param name="TimerIndexOffset">In the data, which timer is being run, counting from 0.</param>
/// <param name="TimerSegment">The segment of the routines that keep the table.</param>
/// <param name="TimerStartOffset">
/// There, the routine that puts a routine in the table, or gives one that is there a new
/// count. It is called far with the routine, a far pointer, and the count.
/// </param>
/// <param name="TimerStopOffset">There, the routine that takes one out: called far with the routine.</param>
/// <param name="TimerRunOffset">There, the call of a timer's routine, as its turn comes.</param>
/// <param name="TimerNames">
/// A name for each timer, by its routine's segment and offset as one number: what its routine
/// does, from reading it.
/// </param>
/// <param name="PlaceType">The type of object that is a place.</param>
/// <param name="PersonType">The type of object that is a person.</param>
/// <param name="FlagNames">
/// What each flag is, by its number, as the program's code uses it: a flag that isn't here is
/// one nothing has and nothing asks about.
/// </param>
/// <param name="PlaceFlagNames">The flags that are something else for a place.</param>
/// <param name="PersonFlagNames">The flags that are something else for a person.</param>
/// <param name="StateFlags">The flags that say how a thing is for now, worth saying beside its name.</param>
/// <param name="Layouts">The records' layouts, by type.</param>
/// <param name="TableOffset">Where the table of objects starts in its segment.</param>
/// <param name="TypesSegment">The segment the table of types is in, when it isn't the data's.</param>
/// <param name="NamesFile">The file the objects' names are in, for a game that keeps them out of the program.</param>
/// <param name="FlagsSegment">
/// The segment of a table of flags, for a game in which a flag's number doesn't say which
/// bit it is: four bytes for each flag, the bit and, at 2, a word with a bit for each type
/// that has the flag.
/// </param>
/// <param name="FlagsOffset">Where in that segment the table would have flag 0.</param>
/// <param name="FlagCount">How many flags the table has; 0 for a game without one.</param>
/// <param name="ExitsSegment">The segment of the one table of every room's exits, four bytes each.</param>
/// <param name="ExitsOffset">Where in it the first row is.</param>
/// <param name="DoorsSegment">The segment of the table of doors an exit can go by: a room and the door, a word each.</param>
/// <param name="DoorsOffset">Where in it the first is.</param>
/// <param name="WideGrammar">Whether the grammar's flags are a word and not a byte, which moves what follows them.</param>
/// <param name="TimerSize">
/// A timer's size in the table: 6 for a far pointer to its routine and a count, or 4 for the
/// timer's number in a byte and, at 2, the count.
/// </param>
/// <param name="ClockStep">How many minutes one step of the clock is.</param>
/// <param name="TokenizerSegment">
/// The segment of the routine that takes a word off the line, for a game whose routine that
/// gathers the tokens is in an overlay: the places in it are that routine's only while its
/// overlay is the one in memory, which the call to this routine just before each shows.
/// </param>
/// <param name="TokenizerOffset">That routine's offset; 0 for a game whose gathering routine is always in memory.</param>
/// <param name="ExitTextsSegment">
/// The segment of the table of what an exit that leads nowhere says, for a game that keeps
/// its exits in one table: a far pointer to the text for each.
/// </param>
/// <param name="ExitTextsOffset">Where in it the first is.</param>
/// <param name="ExitVariablesSegment">
/// The segment of the table of exits a variable opens, eight bytes each: the room it leads
/// to, where in the data the byte is that opens it when it isn't zero, and a far pointer to
/// what it says while it's shut.
/// </param>
/// <param name="ExitVariablesOffset">Where in it the first is.</param>
/// <param name="PlaceOffset">
/// In the data, for a game of places visited in several times, a byte: which place the player
/// is in. One more than the last place is home.
/// </param>
/// <param name="TimeOffset">In the data, a byte: which of the times.</param>
/// <param name="YearsOffset">In the data, the year of each time, a word each, less than zero for a year BC.</param>
/// <param name="ClocksSegment">
/// The segment of the table of clocks, one for each place in each time, four bytes each: a
/// day in a word, an hour and a minute. 0 for a game with one clock.
/// </param>
/// <param name="ClocksOffset">Where in it the first place's first is.</param>
/// <param name="HomeClockOffset">In the data, the clock for home.</param>
/// <param name="PlaceNames">The places' names, by number: as many as there are places, and each has a clock for every time.</param>
/// <param name="Times">How many times a place can be visited in.</param>
/// <param name="ExitVariableAt">
/// How far into a row of the exits a variable opens the variable is: 2, with a far pointer to
/// what the exit says after it, or 4, with a word between and after it the number, from 1, of
/// one of the exits' texts.
/// </param>
/// <param name="FinderSegment">
/// The segment of the routine that finds a lot's objects, for a game in which the call to
/// it is in an overlay: the place at <paramref name="FoundOffset"/> is that call's return
/// only while the call is what's before it. With <paramref name="FinderOffset"/> 0, the
/// place is taken to be always that.
/// </param>
/// <param name="FinderOffset">That routine's offset.</param>
/// <param name="DaysOffset">
/// In the data, for a game that counts days and not turns, how many days have passed. With
/// it, <paramref name="MovesOffset"/> is 0: the game keeps no count of turns.
/// </param>
/// <param name="StateSegment">
/// The segment the action, its direction and its two objects are in, with the word a token
/// is and the form found, when it isn't <paramref name="ActionSegment"/>: <i>Gateway II</i>
/// keeps them in its data segment, and the lots and the timers where the action was.
/// </param>
/// <param name="FoundSize">
/// The size of what was found for a phrase: 6, a count and at 2 a far pointer to the objects,
/// or 4, a count and at 2 where the objects are in the stack's segment.
/// </param>
/// <param name="FoundSegment">
/// The segment the place at <paramref name="FoundOffset"/> is in, for a game whose main loop
/// isn't in the segment of the routine that performs an action; null when it is.
/// </param>
public sealed record WorldMap(
    ushort DataSegment, ushort TableSegment, int ObjectCount, ushort CountOffset, ushort TypesOffset,
    ushort RoomOffset, ushort ActorOffset, ushort MovesOffset, ushort ClockOffset,
    ushort ActionSegment, ushort ActionOffset, ushort ObjectOffset, ushort SecondObjectOffset,
    ushort PerformSegment, ushort PerformOffset, ushort DepthOffset, IReadOnlyList<AnswerPoint> Answers,
    ushort JumpBackOffset, int GoAction, ushort DirectionOffset, IReadOnlyList<string> Directions,
    ushort GrammarSegment, ushort FormsOffset, ushort SecondsOffset, string VocabularyFile,
    IReadOnlyDictionary<int, string> ActionNames,
    ushort TokenSegment, ushort TokenOffset, ushort AfterStopOffset, ushort AfterAndOffset, ushort TokenWordOffset,
    IReadOnlyDictionary<int, string> WordTokens, IReadOnlyDictionary<int, string> MarkTokens, IReadOnlyList<int> EndTokens,
    ushort FormOffset, ushort FormIndexOffset, ushort FormActionOffset, ushort FoundOffset, ushort LotsOffset, int LotSize, ushort LotCountOffset,
    ushort TimersOffset, ushort TimerCountOffset, ushort TimerIndexOffset,
    ushort TimerSegment, ushort TimerStartOffset, ushort TimerStopOffset, ushort TimerRunOffset,
    IReadOnlyDictionary<int, string> TimerNames,
    int PlaceType, int PersonType,
    IReadOnlyDictionary<int, string> FlagNames,
    IReadOnlyDictionary<int, string> PlaceFlagNames,
    IReadOnlyDictionary<int, string> PersonFlagNames,
    IReadOnlyList<int> StateFlags,
    IReadOnlyDictionary<int, RecordLayout> Layouts,
    ushort TableOffset = 0, ushort? TypesSegment = null, string? NamesFile = null,
    ushort FlagsSegment = 0, ushort FlagsOffset = 0, int FlagCount = 0,
    ushort ExitsSegment = 0, ushort ExitsOffset = 0, ushort DoorsSegment = 0, ushort DoorsOffset = 0,
    bool WideGrammar = false, int TimerSize = 6, int ClockStep = 5,
    ushort TokenizerSegment = 0, ushort TokenizerOffset = 0,
    ushort ExitTextsSegment = 0, ushort ExitTextsOffset = 0, ushort ExitVariablesSegment = 0, ushort ExitVariablesOffset = 0,
    ushort PlaceOffset = 0, ushort TimeOffset = 0, ushort YearsOffset = 0,
    ushort ClocksSegment = 0, ushort ClocksOffset = 0, ushort HomeClockOffset = 0,
    IReadOnlyList<string>? PlaceNames = null, int Times = 0, ushort? FoundSegment = null,
    int ExitVariableAt = 2, ushort FinderSegment = 0, ushort FinderOffset = 0, int FoundSize = 6,
    ushort DaysOffset = 0, ushort? StateSegment = null)
{
    /// <summary>
    /// Whether the places in the program where a command is parsed are known. A map can have
    /// a game's world, and where its actions are performed, without them.
    /// </summary>
    public bool Parses => TokenOffset != 0;

    /// <summary>What a flag is for an object of a type, or null when it isn't known.</summary>
    public string? FlagName(int flag, int type) =>
        type == PlaceType && PlaceFlagNames.TryGetValue(flag, out string? place) ? place
        : type == PersonType && PersonFlagNames.TryGetValue(flag, out string? person) ? person
        : FlagNames.GetValueOrDefault(flag);

    /// <summary><i>Spellcasting 101: Sorcerers Get All the Girls</i> (1990).</summary>
    public static WorldMap Spellcasting101 { get; } = new(
        DataSegment: 0x4517, TableSegment: 0x2929, ObjectCount: 427, CountOffset: 0x34E4, TypesOffset: 0x34C9,
        RoomOffset: 0x42, ActorOffset: 0x44, MovesOffset: 0x46, ClockOffset: 0x48,
        ActionSegment: 0x3B00, ActionOffset: 0x58, ObjectOffset: 0x00, SecondObjectOffset: 0x56,
        PerformSegment: 0x0C35, PerformOffset: 0x0FF4, DepthOffset: 0x778C,
        Answers:
        [
            // the room, what the actor is in, and the actor; the routine before the action's;
            // what the second object is in, and it; what the first is in, and it; the action's
            new(0x11AF, Asked.Object), new(0x11DE, Asked.Object), new(0x1218, Asked.Object), new(0x1236, Asked.Object),
            new(0x1252, Asked.Before),
            new(0x128D, Asked.Object), new(0x12AB, Asked.Object), new(0x12E6, Asked.Object), new(0x1304, Asked.Object),
            new(0x1320, Asked.Action),
        ],
        JumpBackOffset: 0x2916, GoAction: 197, DirectionOffset: 0x59,
        Directions: ["", "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest", "up", "down", "in", "out"],
        GrammarSegment: 0x2B6A, FormsOffset: 0x00D1, SecondsOffset: 0x0F81, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [197] = "go" },
        TokenSegment: 0x130E, TokenOffset: 0x00ED, AfterStopOffset: 0x017E, AfterAndOffset: 0x01FC, TokenWordOffset: 0x4AA0,
        WordTokens: new Dictionary<int, string>
        {
            [-2] = "verb", [-3] = "preposition", [-4] = "adjective", [-5] = "noun", [-6] = "article",
            [-8] = "verb or noun", [-9] = "verb or adjective", [-10] = "verb, adjective or noun",
            [-11] = "adjective or noun", [-12] = "verb or preposition",
        },
        MarkTokens: new Dictionary<int, string> { [-18] = ",", [-19] = ".", [-20] = ";" },
        EndTokens: [-1, -17, -20],
        FormOffset: 0x21ED, FormIndexOffset: 0x8AD6, FormActionOffset: 0x54, FoundOffset: 0x0CCB, LotsOffset: 0x3C, LotSize: 12, LotCountOffset: 0x38,
        TimersOffset: 0x4F30, TimerCountOffset: 0x7858, TimerIndexOffset: 0x785A,
        TimerSegment: 0x1272, TimerStartOffset: 0x0033, TimerStopOffset: 0x00BF, TimerRunOffset: 0x0250,
        TimerNames: new Dictionary<int, string>
        {
            [0x07EC04B4] = "the simulation ends",
            [0x07EC06D9] = "the stepfather on the stairs",
            [0x07EC06DF] = "the man with the whip",
            [0x07EC06E5] = "the potty door closes",
            [0x07EC06EB] = "the voice at the gate",
            [0x07EC06F1] = "Miss Beancounter's spectacles",
            [0x07EC06F7] = "the amazon leader",
            [0x07EC06FD] = "the amazons' visits",
            [0x07EC0703] = "the chambermaid",
            [0x07EC0709] = "the waiter comes",
            [0x07EC070F] = "the waiter brings it",
            [0x07EC0715] = "the islanders gather",
            [0x07EC071B] = "the islanders' thanks",
            [0x07EC0721] = "Ocarina at home",
            [0x07EC0727] = "Glockenspiel comes back",
            [0x07EC072D] = "the gods quarrel",
            [0x07EC0733] = "the gold gate shuts",
            [0x07EC0739] = "the silver gate shuts",
            [0x07EC073F] = "the bronze gate shuts",
            [0x07EC0745] = "the ending",
            [0x07EC074B] = "to Batguano Court",
            [0x07EC075A] = "the role-players' game",
            [0x07EC0760] = "meals at the Cafeteria",
            [0x07EC0766] = "the Stadium's day",
            [0x07EC076C] = "the professor tires",
            [0x07EC0772] = "the party starts",
            [0x07EC0778] = "the party's chatter",
            [0x07EC077E] = "Gretchen with you",
            [0x07EC0784] = "Gretchen arrives",
            [0x07EC078A] = "the fratman",
            [0x07EC07D2] = "dinner with Hillary",
            [0x07EC07D8] = "the class bell",
            [0x07EC07DE] = "the dinner invitation",
            [0x07EC07E4] = "Tickingclock comes in",
            [0x07EC07EA] = "the lecture",
            [0x07EC07F0] = "the day's first look",
            [0x07EC089F] = "a word forgotten",
            [0x07EC08AD] = "a byte forgotten",
            [0x07EC08BA] = "FRIMP wears off",
            [0x07EC09F3] = "sleepiness",
            [0x07EC128A] = "ZEM wears off",
            [0x07EC12A3] = "the dragon's breath",
            [0x07EC12A9] = "the pendulum",
            [0x09BB0D1E] = "the red button resets",
            [0x09BB0D26] = "the surfboard's trip",
            [0x36700899] = "the registration line",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [2] = "its name takes an",
            [3] = "plural",
            [4] = "takes some",
            [5] = "its name takes no article",
            [6] = "seen",
            [7] = "not listed",
            [8] = "special to take",
            [9] = "gives light",
            [10] = "touched",
            [11] = "a door",
            [12] = "open",
            [13] = "locked",
            [14] = "can't be opened or closed",
            [15] = "a container",
            [16] = "left out of all",
            [17] = "lit",
            [19] = "can be seen through",
            [20] = "can be read",
            [21] = "can be got in or on",
            [22] = "burns",
            [23] = "can be taken",
            [24] = "can be worn",
            [25] = "a key",
            [26] = "cuts",
            [27] = "cuts",
            [28] = "can be eaten",
            [29] = "what's dropped in it lands outside",
            [30] = "magic",
            [31] = "can be lit",
            [32] = "to be looked for",
            [33] = "things can be on it",
        },
        PlaceFlagNames: new Dictionary<int, string> { [7] = "outdoors", [10] = "visited" },
        PersonFlagNames: new Dictionary<int, string> { [8] = "can be asked things", [9] = "female" },
        StateFlags: [12, 13, 17],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(28, Lists: 0x0E, ExitCount: 0x16, Exits: 0x18),
            [2] = new(36, Places: 0x12, Relation: 0x14, Next: 0x16, Lists: 0x18, FlagBytes: 6),
            [3] = new(14),
            [4] = new(22, Lists: 0x0E),
            [5] = new(22, Lists: 0x0E),
            [6] = new(26, Places: 0x0E, Lists: 0x12),
            [7] = new(32, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14),
            [8] = new(22, Places: 0x0E, Relation: 0x10, Next: 0x12),
        });

    /// <summary>
    /// <i>Spellcasting 201: The Sorcerer's Appliance</i> (1991).
    /// </summary>
    public static WorldMap Spellcasting201 { get; } = new(
        DataSegment: 0x4974, TableSegment: 0x2918, ObjectCount: 482, CountOffset: 0x3E70, TypesOffset: 0xFFFD,
        RoomOffset: 0x2A06, ActorOffset: 0x2A08, MovesOffset: 0x2A0A, ClockOffset: 0x2A0C,
        ActionSegment: 0x3FA7, ActionOffset: 0xB2, ObjectOffset: 0xAE, SecondObjectOffset: 0xB0,
        PerformSegment: 0x0D68, PerformOffset: 0x0EDF, DepthOffset: 0x7D9E,
        Answers:
        [
            // the room, the player when someone else is acting, what the actor is in, and the
            // actor; the routine before the action's; what the second object is in, and it;
            // what the first is in, and it; the action's
            new(0x10B0, Asked.Object), new(0x10E2, Asked.Object), new(0x111D, Asked.Object), new(0x113C, Asked.Object),
            new(0x1159, Asked.Before),
            new(0x1195, Asked.Object), new(0x11B4, Asked.Object), new(0x11ED, Asked.Object), new(0x1209, Asked.Object),
            new(0x1223, Asked.Action),
        ],
        JumpBackOffset: 0x2A25, GoAction: 211, DirectionOffset: 0xB3,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2C03, FormsOffset: 0x00E2, SecondsOffset: 0x10B4, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [211] = "go" },
        // the routine that gathers the tokens is the first game's, offset for offset, in overlay 4
        TokenSegment: 0x32B1, TokenOffset: 0x00ED, AfterStopOffset: 0x017E, AfterAndOffset: 0x01FC, TokenWordOffset: 0x4029,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x239C, FormIndexOffset: 0x0139, FormActionOffset: 0x0135, FoundOffset: 0x090E, LotsOffset: 0x011D, LotSize: 12, LotCountOffset: 0x0117,
        TimersOffset: 0x5D2A, TimerCountOffset: 0x7EF0, TimerIndexOffset: 0x7EF2,
        TimerSegment: 0x13A2, TimerStartOffset: 0x0027, TimerStopOffset: 0x009E, TimerRunOffset: 0x0213,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "the day's initiation",
            [2] = "the upperclassmen find you",
            [3] = "the serpent heads speak",
            [4] = "the simulation ends",
            [5] = "sleepiness",
            [6] = "the class bell",
            [7] = "the tower clock chimes",
            [8] = "nothing",
            [9] = "a word forgotten",
            [10] = "a byte forgotten",
            [11] = "what was raised sinks back",
            [12] = "the simpleberries wear off",
            [13] = "the speed potion wears off",
            [14] = "the mascot swells back",
            [15] = "the party starts",
            [16] = "you open your eyes",
            [17] = "the Brus make ready",
            [18] = "the Poolside Party",
            [19] = "Hiddenmolar and Cowpatty whisper",
            [20] = "word of Tickingclock's death",
            [21] = "Tickingclock's errand",
            [22] = "Tickingclock goes",
            [23] = "the sewer gurgles",
            [24] = "the last newspaper goes",
            [25] = "a second byte forgotten",
            [26] = "the Appliance runs",
            [27] = "the Sorcerer returns",
            [28] = "the banana fetches water",
            [29] = "the army of bananas",
            [30] = "a knock from the yard",
            [31] = "twelve bytes forgotten",
            [32] = "everyone out of the pool",
            [33] = "the Welcome Back Party fills",
            [34] = "the Royal Parade",
            [35] = "the hellhounds come",
            [36] = "the bier is brought in",
            [37] = "you are yourself again",
            [38] = "Hiddenmolar tells all",
            [39] = "the messenger",
            [40] = "the cloak is finished",
            [41] = "the cloak is brought",
            [42] = "the iron turns to copper",
            [43] = "the copper dissolves",
            [44] = "the hanging",
            [45] = "the cook and the hors d'oevres",
            [46] = "the tree is a sapling again",
            [47] = "the Social Hall, turn by turn",
            [48] = "a count down by sixes",
            [49] = "seen in the Ancient Lab",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "its name takes no article",
            [2] = "its name takes an",
            [3] = "plural",
            [4] = "takes some",
            [5] = "seen",
            [6] = "touched",
            [7] = "special to take",
            [8] = "not listed",
            [9] = "gives light",
            [10] = "a door",
            [11] = "can't be opened or closed",
            [12] = "can be read",
            [13] = "open",
            [14] = "locked",
            [15] = "can be taken",
            [16] = "in the sewer",
            [17] = "at the university",
            [18] = "left out of all",
            [19] = "lit",
            [20] = "can be seen through",
            [21] = "things can be on it",
            [22] = "can be got in or on",
            [23] = "burns",
            [24] = "a container",
            [25] = "can be worn",
            [26] = "a key",
            [27] = "cuts",
            [28] = "cuts",
            [29] = "can be eaten",
            [30] = "what's dropped in it lands outside",
            [31] = "magic",
            [32] = "can be lit",
            [33] = "outdoors",
            [34] = "a part of the Appliance",
            [36] = "a bed",
            [37] = "someone other than you",
            [38] = "female",
        },
        PlaceFlagNames: new Dictionary<int, string> { [6] = "visited" },
        PersonFlagNames: new Dictionary<int, string>(),
        StateFlags: [13, 14, 19],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(24, Lists: 0x0A, ExitCount: 0x14, ExitStart: 0x16, Names: 4),
            [2] = new(34, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14, FlagBytes: 6, Names: 4),
            [3] = new(10, Names: 4),
            [4] = new(18, Lists: 0x0A, Names: 4),
            [5] = new(18, Lists: 0x0A, Names: 4),
            [6] = new(22, Places: 0x0A, Lists: 0x0E, Names: 4),
            [7] = new(28, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Lists: 0x10, Names: 4),
            [8] = new(18, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Names: 4),
        },
        TableOffset: 0x00B8, TypesSegment: 0x2918, NamesFile: "OBJECT.DAT",
        FlagsSegment: 0x2918, FlagsOffset: 0x0014, FlagCount: 40,
        ExitsSegment: 0x2918, ExitsOffset: 0x29CC, DoorsSegment: 0x2918, DoorsOffset: 0x2DF4,
        WideGrammar: true, TimerSize: 4,
        TokenizerSegment: 0x12F1, TokenizerOffset: 0x027C);

    /// <summary>
    /// <i>Timequest</i> (1991): between the other two, with the first game's records and flags
    /// by their bits, the second's one table of exits and numbered timers, and a clock for
    /// each place in each time.
    /// </summary>
    public static WorldMap Timequest { get; } = new(
        DataSegment: 0x4333, TableSegment: 0x29E5, ObjectCount: 518, CountOffset: 0x05DA, TypesOffset: 0xFFFD,
        RoomOffset: 0x8A, ActorOffset: 0x8C, MovesOffset: 0x92, ClockOffset: 0,
        ActionSegment: 0x3C2B, ActionOffset: 0x54, ObjectOffset: 0x3312, SecondObjectOffset: 0x00,
        PerformSegment: 0x1134, PerformOffset: 0x0D40, DepthOffset: 0x529E,
        Answers:
        [
            // the first game's ten, in its order
            new(0x0F00, Asked.Object), new(0x0F32, Asked.Object), new(0x0F6D, Asked.Object), new(0x0F8C, Asked.Object),
            new(0x0FA9, Asked.Before),
            new(0x0FE5, Asked.Object), new(0x1004, Asked.Object), new(0x103D, Asked.Object), new(0x1059, Asked.Object),
            new(0x1073, Asked.Action),
        ],
        JumpBackOffset: 0x2466, GoAction: 144, DirectionOffset: 0x55,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2C82, FormsOffset: 0x00A5, SecondsOffset: 0x0E79, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [144] = "go" },
        TokenSegment: 0x1408, TokenOffset: 0x00F7, AfterStopOffset: 0x0188, AfterAndOffset: 0x0206, TokenWordOffset: 0x3314,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x1EDD, FormIndexOffset: 0x6458, FormActionOffset: 0x52, FoundOffset: 0x0759, LotsOffset: 0x3A, LotSize: 12, LotCountOffset: 0x36,
        TimersOffset: 0x388A, TimerCountOffset: 0x527E, TimerIndexOffset: 0x5280,
        TimerSegment: 0x1101, TimerStartOffset: 0x002B, TimerStopOffset: 0x00A2, TimerRunOffset: 0x01DA,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "the sultan presents you to Yahya",
            [2] = "the sultan gives up on you",
            [3] = "the wives leave off",
            [4] = "the wives go to bathe",
            [5] = "the stolen veil is missed",
            [6] = "a rustling behind the wall",
            [7] = "the sultan is heard outside",
            [8] = "the sultan finds you under the bed",
            [9] = "the sultan spares you",
            [10] = "a soldier grabs you in the bazaar",
            [11] = "the mule lowers his ears",
            [12] = "the soldiers search the House",
            [13] = "Vettenmyer bursts in",
            [14] = "Vettenmyer tells the key's secret",
            [15] = "footsteps on the stairs",
            [16] = "another you comes in",
            [17] = "Vettenmyer grabs the laser",
            [18] = "Vettenmyer fires at you",
            [19] = "Vettenmyer fires at the other you",
            [20] = "Vettenmyer laughs last",
            [21] = "Vettenmyer waits for the other you",
            [22] = "the other you materializes",
            [23] = "the other you stops the Doomsday Device",
            [24] = "Vettenmyer is met in Rome",
            [25] = "the riddle's word was never said",
            [26] = "Drake goes up to the queen",
            [27] = "the strumpet is thrown out",
            [28] = "Churchill's broadcast",
            [29] = "what's seen at the Tavern",
            [30] = "the courier calls for a horse",
            [31] = "the courier gallops past",
            [32] = "the courier is ambushed",
            [33] = "the courier rides by",
            [34] = "the outlaws melt into the forest",
            [35] = "the courier stirs",
            [36] = "the courier comes to",
            [37] = "the cleric examines the letter",
            [38] = "the courier reaches Runnymede",
            [39] = "the cleric reads the letter to King John",
            [40] = "an arrow from the trees",
            [41] = "the Shed, turn by turn",
            [42] = "a hue and cry past the shed",
            [43] = "the sky brightens",
            [44] = "the sun comes up",
            [45] = "the pope's book, every ten turns",
            [46] = "nothing",
            [47] = "a man on a bench speaks",
            [48] = "the focaccia man comes by",
            [49] = "the focaccia man wanders off",
            [50] = "the parade of lions begins",
            [51] = "the lion breaks away",
            [52] = "the lion charges",
            [53] = "the lion kills",
            [54] = "the chariot race",
            [55] = "the Circus empties",
            [56] = "Napoleon's aide argues",
            [57] = "Napoleon is overheard",
            [58] = "Napoleon dismisses the aide",
            [59] = "Napoleon decides",
            [60] = "the hanky dries",
            [61] = "the priest misses the crown",
            [62] = "the pope comes for the crown",
            [63] = "the shout for Charlemagne",
            [64] = "the pope sets out",
            [65] = "the pope asks for Attila",
            [66] = "a barbarian strides into the tent",
            [67] = "Attila comes out of the tent",
            [68] = "war cries outside the tent",
            [69] = "Attila's patience ends",
            [70] = "Pope Leo comes out of the Vatican",
            [71] = "Pope Leo comes down the road",
            [72] = "the pope is shoved into the tent",
            [73] = "Hitler and Mussolini at the radio, turn by turn",
            [74] = "Hitler gets the radio going",
            [75] = "Hitler says to hop to it",
            [76] = "Hitler throws you out",
            [77] = "Mussolini asks the word",
            [78] = "Vettenmyer sneers",
            [79] = "the old man stares",
            [80] = "Vettenmyer grows agitated",
            [81] = "Vettenmyer's look of hatred",
            [82] = "Vettenmyer dies",
            [83] = "the old man's last breath",
            [84] = "the ether evaporates",
            [85] = "the surroundings melt away",
            [86] = "the bracelet glows and goes",
            [87] = "Vettenmyer steps out of the wall",
            [88] = "the general's orders",
            [89] = "the general says to stop wasting time",
            [90] = "the Olmec sneaks a look",
            [91] = "the Olmec knocks you out",
            [92] = "the Olmec's knife is sharp",
            [93] = "the Olmec raises his knife",
            [94] = "the eclipse",
            [95] = "the eclipse ends",
            [96] = "the Olmec throws his spear",
            [97] = "the spear hits",
            [98] = "a messenger to Montezuma",
            [99] = "Montezuma and the mural",
            [100] = "Montezuma's messenger returns",
            [101] = "the priest's sacrifice",
            [102] = "the priest sees an imposter",
            [103] = "the priest's knife",
            [104] = "the sacrificial priest comes in",
            [105] = "the mourners file into the cave",
            [106] = "the mourners tire of waiting",
            [107] = "the mourners drink the poison",
            [108] = "the Mongol leader leaves the tent",
            [109] = "Genghis storms back",
            [110] = "the women come back",
            [111] = "the funeral procession, every ten turns",
            [112] = "the barbarian army",
            [113] = "the fortune cookie",
            [114] = "nothing",
            [115] = "the girl",
            [116] = "Cleopatra's sedan chair, every hour",
            [117] = "nothing",
            [118] = "a word cleared",
            [119] = "Caesar congratulates you",
            [120] = "a rock from the window",
            [121] = "from the Royal Palace to the Royal compound",
            [122] = "Vettenmyer's welcome",
            [123] = "the vizier overthrows the sultan",
            [124] = "Napoleon has you taken away",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "takes some",
            [2] = "touched",
            [3] = "seen",
            [4] = "left out of all",
            [5] = "its name takes no article",
            [6] = "its name takes an",
            [7] = "plural",
            [8] = "not listed",
            [11] = "open",
            [12] = "can be taken, and hasn't been",
            [14] = "can be opened and closed",
            [15] = "a container",
            [16] = "burns",
            [17] = "a door",
            [18] = "what's dropped in it lands outside",
            [19] = "can be eaten",
            [21] = "a liquid",
            [22] = "cuts",
            [23] = "lit",
            [24] = "things can be on it",
            [25] = "cuts",
            [26] = "can be read",
            [28] = "a key",
            [29] = "can be turned on and off",
            [30] = "can be taken",
            [32] = "can be seen into",
            [33] = "an animal",
            [34] = "turned on",
            [35] = "can be got in or on",
            [36] = "locked",
            [37] = "can be worn",
            [38] = "worn",
        },
        PlaceFlagNames: new Dictionary<int, string> { [2] = "visited", [8] = "outdoors", [23] = "has light" },
        PersonFlagNames: new Dictionary<int, string> { [10] = "female", [11] = "someone other than you", [12] = "can't answer" },
        StateFlags: [11, 23, 34, 36],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(28, Lists: 0x10, FlagBytes: 4, ExitCount: 0x18, ExitStart: 0x1A),
            [2] = new(36, Places: 0x12, Relation: 0x14, Next: 0x16, Lists: 0x18, FlagBytes: 6),
            [3] = new(14),
            [4] = new(22, Lists: 0x0E),
            [5] = new(24, Lists: 0x10, FlagBytes: 4),
            [6] = new(28, Places: 0x10, Lists: 0x14, FlagBytes: 4),
            [7] = new(34, Places: 0x10, Relation: 0x12, Next: 0x14, Lists: 0x16, FlagBytes: 4),
            [8] = new(24, Places: 0x10, Relation: 0x12, Next: 0x14, FlagBytes: 4),
        },
        TableOffset: 0x0018, TypesSegment: 0x29E5,
        ExitsSegment: 0x29E5, ExitsOffset: 0x2352, DoorsSegment: 0x4333, DoorsOffset: 0x05EC,
        TimerSize: 4, ClockStep: 1,
        ExitTextsSegment: 0x29E5, ExitTextsOffset: 0x28AF, ExitVariablesSegment: 0x29E5, ExitVariablesOffset: 0x28EF,
        PlaceOffset: 0x6C, TimeOffset: 0x6D, YearsOffset: 0x6E,
        ClocksSegment: 0x27B0, ClocksOffset: 0x010C, HomeClockOffset: 0x82,
        PlaceNames: ["Baghdad", "Peking", "Cairo", "Dover", "Mexico", "Rome"], Times: 9);

    /// <summary>
    /// <i>Spellcasting 301: Spring Break</i> (1992): kept as <i>Spellcasting 201</i> is, with
    /// its routines in tables, a main loop apart from the routine that performs an action, and
    /// the routine that gathers a command's tokens in an overlay with the one that takes them.
    /// </summary>
    public static WorldMap Spellcasting301 { get; } = new(
        DataSegment: 0x4934, TableSegment: 0x2BDB, ObjectCount: 517, CountOffset: 0x48B2, TypesOffset: 0xFFFD,
        RoomOffset: 0x388C, ActorOffset: 0x388E, MovesOffset: 0x3890, ClockOffset: 0x3892,
        ActionSegment: 0x3E77, ActionOffset: 0xABB6, ObjectOffset: 0x059C, SecondObjectOffset: 0x059A,
        PerformSegment: 0x0000, PerformOffset: 0x01A4, DepthOffset: 0x3812,
        Answers:
        [
            // the same ten, in the same order
            new(0x0370, Asked.Object), new(0x03A2, Asked.Object), new(0x03DA, Asked.Object), new(0x03F9, Asked.Object),
            new(0x0416, Asked.Before),
            new(0x044F, Asked.Object), new(0x046E, Asked.Object), new(0x04A4, Asked.Object), new(0x04C0, Asked.Object),
            new(0x04DA, Asked.Action),
        ],
        JumpBackOffset: 0x0847, GoAction: 236, DirectionOffset: 0xABB7,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2E2A, FormsOffset: 0x0626, SecondsOffset: 0x180A, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [236] = "go" },
        // the routine that gathers the tokens is in overlay 2, with the one that takes a word off the line
        TokenSegment: 0x350B, TokenOffset: 0x236B, AfterStopOffset: 0x23FC, AfterAndOffset: 0x247A, TokenWordOffset: 0x4BF8,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x0DE5, FormIndexOffset: 0x4C04, FormActionOffset: 0x023E, FoundOffset: 0x0A63, LotsOffset: 0x0226, LotSize: 12, LotCountOffset: 0x0220,
        TimersOffset: 0x4D68, TimerCountOffset: 0x3852, TimerIndexOffset: 0x3854,
        TimerSegment: 0x02D3, TimerStartOffset: 0x002C, TimerStopOffset: 0x009E, TimerRunOffset: 0x020A,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "sleepiness",
            [2] = "two words forgotten",
            [3] = "a byte forgotten",
            [4] = "what was raised settles back",
            [5] = "you open your eyes",
            [6] = "a count down by sixes",
            [7] = "the carpet is loaded, turn by turn",
            [8] = "the carpet flies east, turn by turn",
            [9] = "the carpet spirals down",
            [10] = "the world's most beautiful woman passes",
            [11] = "Fred fetches you to the lobby",
            [12] = "Sid takes you to the beach, turn by turn",
            [13] = "Moe calls it a day",
            [14] = "Gary says the bash is near",
            [15] = "the bimbos follow the studfinder",
            [16] = "The Judge passes through the lobby",
            [17] = "The Judge comes to the party",
            [18] = "the cell's bars rust away",
            [19] = "the studfinder hums",
            [20] = "The Judge judges the sand castles",
            [21] = "The Judge's event at the bandstand, turn by turn",
            [22] = "the mouse race",
            [23] = "the event at the pool, turn by turn",
            [24] = "everyone comes into the bar, turn by turn",
            [25] = "the Sheriff jails you",
            [26] = "BIGFINNO wears off",
            [27] = "the Soda-Barfing Contest, turn by turn",
            [28] = "the drawing of straws, turn by turn",
            [29] = "the laxative works",
            [30] = "the Sheriff jails you, from the arena",
            [31] = "the volleyball face-off, turn by turn",
            [32] = "the Yus swagger onto the beach",
            [33] = "the showdown on the boulevard, turn by turn",
            [34] = "the magic mushrooms come out",
            [35] = "the mushrooms take hold, turn by turn",
            [36] = "the tan fades",
            [37] = "Moe's turn",
            [38] = "Vince's turn",
            [39] = "Ralph's turn",
            [40] = "Ollie's turn",
            [41] = "Izzie's turn",
            [42] = "Fred's turn",
            [43] = "the Pharts come to the Dance Club",
            [44] = "the building shakes",
            [45] = "the baby squid is found",
            [46] = "the hunger pains",
            [47] = "the baby squid whimpers, every turn",
            [48] = "Joey shoots at your feet",
            [49] = "the dog digs under the fence",
            [50] = "Ollie says Fred has passed out",
            [51] = "Sid visits the cell",
            [52] = "a count down by fours",
            [53] = "the mama bull returns",
            [54] = "the carpet sputters",
            [55] = "your hostess in Room 1812 loses patience",
            [56] = "Snorty's nose clears",
            [57] = "Snorty pops the inflatable cow",
            [58] = "the crowds swarm to Blather Beach",
            [59] = "your order is called",
            [60] = "Moe says to get a move on",
            [61] = "the human pyramid comes apart",
            [62] = "the Copy Nymph flies in",
            [63] = "the moby berry shrinks",
            [64] = "the berry plant shrivels",
            [65] = "you feel filled with vigor",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "its name takes no article",
            [2] = "its name takes an",
            [3] = "plural",
            [4] = "takes some",
            [5] = "seen",
            [6] = "touched",
            [7] = "special to take",
            [8] = "not listed",
            [9] = "gives light",
            [10] = "an animal",
            [11] = "can't be opened or closed",
            [12] = "a door",
            [14] = "can be read",
            [15] = "locked",
            [16] = "open",
            [17] = "things can be on it",
            [18] = "a container",
            [19] = "can be taken",
            [20] = "under water",
            [23] = "lit",
            [24] = "can be seen through",
            [26] = "can be got in or on",
            [27] = "burns",
            [30] = "can be worn",
            [31] = "a key",
            [32] = "cuts",
            [33] = "cuts",
            [35] = "can be eaten",
            [37] = "magic",
            [38] = "can be lit",
            [39] = "a bed",
            [41] = "a tube of paint",
            [45] = "a seahorse",
            [46] = "outdoors",
            [47] = "someone other than you",
            [48] = "female",
        },
        PlaceFlagNames: new Dictionary<int, string> { [6] = "visited" },
        PersonFlagNames: new Dictionary<int, string>(),
        StateFlags: [15, 16, 23],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(24, Lists: 0x0A, ExitCount: 0x14, ExitStart: 0x16, Names: 4),
            [2] = new(40, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14, FlagBytes: 6, Names: 4),
            [3] = new(12, Names: 4),
            [4] = new(20, Lists: 0x0A, Names: 4),
            [5] = new(20, Lists: 0x0A, Names: 4),
            [6] = new(24, Places: 0x0A, Lists: 0x0E, Names: 4),
            [7] = new(30, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Lists: 0x10, Names: 4),
            [8] = new(20, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Names: 4),
        },
        TableOffset: 0x00E4, TypesSegment: 0x2BDB, NamesFile: "OBJECT.DAT",
        FlagsSegment: 0x2BDB, FlagsOffset: 0x0014, FlagCount: 51,
        ExitsSegment: 0x2BDB, ExitsOffset: 0x1F36, DoorsSegment: 0x4934, DoorsOffset: 0x48B4,
        WideGrammar: true, TimerSize: 4,
        TokenizerSegment: 0x350B, TokenizerOffset: 0x01F4,
        FoundSegment: 0x0166);

    /// <summary>
    /// <i>Gateway</i> (1992): <i>Spellcasting 301</i>'s system with <i>Timequest</i>'s wider
    /// flags and five kinds of exit, a clock in minutes, and the call that finds a command's
    /// objects in an overlay.
    /// </summary>
    public static WorldMap Gateway { get; } = new(
        DataSegment: 0x4C0B, TableSegment: 0x27E4, ObjectCount: 734, CountOffset: 0x3BEE, TypesOffset: 0xFFFD,
        RoomOffset: 0x38D8, ActorOffset: 0x38DA, MovesOffset: 0, ClockOffset: 0x38E0,
        ActionSegment: 0x3DA4, ActionOffset: 0xE658, ObjectOffset: 0x055A, SecondObjectOffset: 0x0558,
        PerformSegment: 0x0000, PerformOffset: 0x01A4, DepthOffset: 0x38AE,
        Answers:
        [
            // the same ten, where Spellcasting 301 has them
            new(0x0370, Asked.Object), new(0x03A2, Asked.Object), new(0x03DA, Asked.Object), new(0x03F9, Asked.Object),
            new(0x0416, Asked.Before),
            new(0x044F, Asked.Object), new(0x046E, Asked.Object), new(0x04A4, Asked.Object), new(0x04C0, Asked.Object),
            new(0x04DA, Asked.Action),
        ],
        JumpBackOffset: 0x07B8, GoAction: 152, DirectionOffset: 0xE659,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2C95, FormsOffset: 0x04BE, SecondsOffset: 0x1472, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [152] = "go" },
        // the routine that gathers the tokens is in overlay 2, and takes each word off the line
        // by a thunk to another overlay
        TokenSegment: 0x366B, TokenOffset: 0x06FB, AfterStopOffset: 0x078C, AfterAndOffset: 0x080A, TokenWordOffset: 0x6CA4,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x0CF0, FormIndexOffset: 0x8D0A, FormActionOffset: 0x02DC, FoundOffset: 0x0295, LotsOffset: 0x02C4, LotSize: 12, LotCountOffset: 0x02BE,
        TimersOffset: 0x8C04, TimerCountOffset: 0x38D4, TimerIndexOffset: 0x38D6,
        TimerSegment: 0x02E3, TimerStartOffset: 0x002B, TimerStopOffset: 0x00A2, TimerRunOffset: 0x0222,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "the woman leaves you the gold card",
            [2] = "the poker game, turn by turn",
            [3] = "the beast limps about",
            [4] = "the Dorman sun sets",
            [5] = "a byte changed",
            [6] = "two bytes forgotten",
            [7] = "it has dried off",
            [8] = "they have dried off",
            [9] = "the Ballroom, turn by turn",
            [10] = "the wheel turns, turn by turn",
            [11] = "the beast goes, with the last berries",
            [12] = "the boulder lands on you",
            [13] = "the portals flare",
            [14] = "the demon wanders, turn by turn",
            [15] = "the demons grow restless",
            [16] = "the hydra's head is knocked into the inferno",
            [17] = "the demon stops coughing",
            [18] = "the demon saws at the net",
            [19] = "tiredness, every thirty turns",
            [20] = "the hydra hunts for you",
            [21] = "the beast slams you against the wall",
            [22] = "the dust spills from the bag",
            [23] = "Thom Seldridge comes into the bar",
            [24] = "Terri Neilson comes to the bar",
            [25] = "the robot bartender comes over",
            [26] = "the robot bartender trundles away",
            [27] = "the robot bartender chirps",
            [28] = "Nubar snatches the drink",
            [29] = "Nubar plays the trivia game",
            [30] = "Terri Neilson at the bar, turn by turn",
            [31] = "the boots come round the planet",
            [32] = "nothing",
            [33] = "the prospectors fill the conference room",
            [34] = "the ship handling class ends",
            [35] = "a guard clears the room for a meeting",
            [36] = "the meeting ends",
            [37] = "a man comes down the corridor",
            [38] = "Perry scurries back",
            [39] = "Thom Seldridge leaves the bar",
            [40] = "Nubar Kamalian leaves the bar",
            [41] = "Terri Neilson leaves the bar",
            [42] = "a look at where you are, every thirty turns",
            [43] = "a word forgotten",
            [44] = "the MP arrests you at the Museum",
            [45] = "a creature scurries across the desert",
            [46] = "a yellow creature flits near",
            [47] = "the blue fog clears",
            [48] = "people stare below your navel",
            [49] = "the alien berries, turn by turn",
            [50] = "the old maintenance man appears",
            [51] = "the old man looks for his key",
            [52] = "the old man finishes and goes",
            [53] = "the portal disappears",
            [54] = "the Hydra lair's rock and path, every ten turns",
            [55] = "the spider robot comes for the waste",
            [56] = "the spider robot goes",
            [57] = "the robot's waste box",
            [58] = "the field actuator's hum rises",
            [59] = "something crashes through the anemones",
            [60] = "the spider decides",
            [61] = "the spider's pincers",
            [62] = "the enraged spider",
            [63] = "a worm bursts from the ground",
            [64] = "the worm chews at your helmet",
            [65] = "the pod's tongue",
            [66] = "your drink, every thirty turns",
            [67] = "the snake strikes",
            [68] = "the old man's room, every three turns",
            [69] = "it burrows out of sight",
            [70] = "it scampers out of view",
            [71] = "the bartender snores",
            [72] = "the salamander comes out of the lake",
            [73] = "the salamander goes",
            [74] = "a wailing from the east",
            [75] = "a wailing, further off",
            [76] = "a bubbling moan",
            [77] = "nothing",
            [78] = "the aliens file off",
            [79] = "the program insists, turn by turn",
            [80] = "the firestorm from the broken pyramid",
            [81] = "the tech goes back to his newsletter",
            [82] = "the chasm walls lurch, turn by turn",
            [83] = "at the Tunnel Antechamber",
            [84] = "the shoots grow around you",
            [85] = "the salamander comes up in the tank",
            [86] = "the cheeping stops",
            [87] = "Becker begins his story",
            [88] = "Becker's story ends",
            [89] = "a seed spins into the air",
            [90] = "it finishes the leaves and runs",
            [91] = "sleepiness, every ten turns",
            [92] = "the blister disappears",
            [93] = "Becker goes through the wooden door",
            [94] = "a leaf swats the air",
            [95] = "the raft sinks",
            [96] = "a sense of forgetting something",
            [97] = "an opening appears in the wall",
            [98] = "Becker sprints ahead",
            [99] = "what comes through Becker's door",
            [100] = "Becker asleep, turn by turn",
            [101] = "the tech explains the Guide",
            [102] = "your allotted time ends",
            [103] = "the banner, every ten turns",
            [104] = "Nubar Kamalian sits down next to you",
            [105] = "the Tanning Room, turn by turn",
            [106] = "Becker wants a nap",
            [107] = "Becker wakes",
            [108] = "the tan, every sixty turns",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "takes some",
            [2] = "touched",
            [4] = "its name takes no article",
            [5] = "its name takes an",
            [6] = "plural",
            [7] = "seen",
            [8] = "not listed",
            [10] = "can be taken, and hasn't been",
            [12] = "open",
            [13] = "a liquid",
            [14] = "can be opened and closed",
            [15] = "a door",
            [16] = "opened for you as you go through",
            [17] = "what's dropped in it lands outside",
            [18] = "can be eaten",
            [19] = "a container",
            [21] = "alive",
            [22] = "lit",
            [23] = "can be worn",
            [24] = "cuts",
            [26] = "can be read",
            [27] = "cuts",
            [28] = "things can be on it",
            [29] = "can be taken",
            [30] = "a key",
            [31] = "can be seen through",
            [33] = "turned on",
            [34] = "can be got in or on",
            [35] = "locked",
            [36] = "can be turned on and off",
            [37] = "playing cards",
            [38] = "can be sat on",
            [39] = "outdoors",
            [40] = "female",
            [41] = "a person, and not you or a monster",
            [42] = "can't answer",
        },
        PlaceFlagNames: new Dictionary<int, string> { [2] = "visited", [22] = "has light" },
        PersonFlagNames: new Dictionary<int, string>(),
        StateFlags: [12, 22, 33, 35],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(26, Lists: 0x0C, FlagBytes: 4, ExitCount: 0x16, ExitStart: 0x18, Names: 4),
            [2] = new(36, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14, FlagBytes: 6, Names: 4),
            [3] = new(12, Names: 4),
            [4] = new(20, Lists: 0x0A, Names: 4),
            [5] = new(22, Lists: 0x0C, FlagBytes: 4, Names: 4),
            [6] = new(24, Places: 0x0A, Lists: 0x0E, Names: 4),
            [7] = new(32, Places: 0x0C, Relation: 0x0E, Next: 0x10, Lists: 0x12, FlagBytes: 4, Names: 4),
            [8] = new(24, Places: 0x0C, Relation: 0x0E, Next: 0x10, FlagBytes: 4, Names: 4),
        },
        TableOffset: 0x00C0, TypesSegment: 0x27E4, NamesFile: "OBJECT.DAT",
        FlagsSegment: 0x27E4, FlagsOffset: 0x0014, FlagCount: 42,
        ExitsSegment: 0x27E4, ExitsOffset: 0x417E, DoorsSegment: 0x27E4, DoorsOffset: 0x4996,
        WideGrammar: true, TimerSize: 4, ClockStep: 1,
        TokenizerSegment: 0x1CF4, TokenizerOffset: 0x12EF,
        ExitTextsSegment: 0x27E4, ExitTextsOffset: 0x4AD2, ExitVariablesSegment: 0x4C0B, ExitVariablesOffset: 0x3BF0,
        FoundSegment: 0x3569, ExitVariableAt: 4, FinderSegment: 0x3450, FinderOffset: 0x0442, FoundSize: 4,
        DaysOffset: 0x38E2);

    /// <summary>
    /// <i>Eric the Unready</i> (1993): <i>Spellcasting 301</i>'s system, to where its routines
    /// are, with <i>Gateway</i>'s records and flags, and both a count of turns and a clock in
    /// minutes. Its addresses were found by setting its code beside that game's, routine by
    /// routine.
    /// </summary>
    public static WorldMap EricTheUnready { get; } = new(
        DataSegment: 0x495D, TableSegment: 0x2B79, ObjectCount: 537, CountOffset: 0x3BCE, TypesOffset: 0xFFFD,
        RoomOffset: 0x38D4, ActorOffset: 0x38D6, MovesOffset: 0x38DA, ClockOffset: 0x38DC,
        ActionSegment: 0x3C53, ActionOffset: 0xD080, ObjectOffset: 0x401A, SecondObjectOffset: 0x4018,
        PerformSegment: 0x0000, PerformOffset: 0x01A4, DepthOffset: 0x38AC,
        Answers:
        [
            // the same ten, where Spellcasting 301 has them
            new(0x0370, Asked.Object), new(0x03A2, Asked.Object), new(0x03DA, Asked.Object), new(0x03F9, Asked.Object),
            new(0x0416, Asked.Before),
            new(0x044F, Asked.Object), new(0x046E, Asked.Object), new(0x04A4, Asked.Object), new(0x04C0, Asked.Object),
            new(0x04DA, Asked.Action),
        ],
        JumpBackOffset: 0x0891, GoAction: 159, DirectionOffset: 0xD081,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2D8F, FormsOffset: 0x04E8, SecondsOffset: 0x167C, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [159] = "go" },
        // the routine that gathers the tokens is in overlay 2, with the one that takes a word off the line
        TokenSegment: 0x31DF, TokenOffset: 0x236F, AfterStopOffset: 0x2400, AfterAndOffset: 0x247E, TokenWordOffset: 0x81DA,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x0E27, FormIndexOffset: 0x81E6, FormActionOffset: 0x3DBA, FoundOffset: 0x0B44, LotsOffset: 0x3DA2, LotSize: 12, LotCountOffset: 0x3D9C,
        TimersOffset: 0x834A, TimerCountOffset: 0x38C2, TimerIndexOffset: 0x38C4,
        TimerSegment: 0x028D, TimerStartOffset: 0x002A, TimerStopOffset: 0x009C, TimerRunOffset: 0x0208,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "the farmer fires you",
            [2] = "the farmer wants an answer",
            [3] = "a word forgotten",
            [4] = "Ed McDwarf gives back the sweepstakes notice",
            [5] = "you tire of walking on your knees",
            [6] = "the beard begins to itch",
            [7] = "the beard itches more",
            [8] = "you rip off the beard",
            [9] = "the wheel carries you round",
            [10] = "a gnu lumbers through",
            [11] = "the tavern's patrons and their Mead Lite",
            [12] = "Bruce the waiter",
            [13] = "the oaf's heads argue",
            [14] = "a raspberry at Blicester Castle",
            [15] = "the turtles close in",
            [16] = "the man in the stocks, turn by turn",
            [17] = "a boy and his father at the fair",
            [18] = "the storyteller's show",
            [19] = "Lily's show",
            [20] = "the jugglers come on",
            [21] = "the jugglers finish",
            [22] = "Sir Pectoral's thugs catch you",
            [23] = "the elf's shell game",
            [24] = "the musician wanders off",
            [25] = "the dragon flames, turn by turn",
            [26] = "the hanky dries out",
            [27] = "the virgin stops crying",
            [28] = "a unicorn comes for you",
            [29] = "the priestess searches, and the sacrifice begins",
            [30] = "a scream from the sanctuary",
            [31] = "Molochi smokes and burns, turn by turn",
            [32] = "words from the sanctuary",
            [33] = "the dragon's flame goes out",
            [34] = "the escalator carries you up",
            [35] = "a god greets you",
            [36] = "the cork pops",
            [37] = "Morty's door opens",
            [38] = "the receptionist speaks into her headset",
            [39] = "Clio comes back, and Morty finishes",
            [40] = "Morty shows you out",
            [41] = "the candles are put out",
            [42] = "the librarian fusses",
            [43] = "the library book disappears",
            [44] = "the library book is missed",
            [45] = "the slimewig wriggles away",
            [46] = "the slimewig kicks",
            [47] = "a salty burp",
            [48] = "the raft's voyage, turn by turn",
            [49] = "the whirlpool",
            [50] = "Gnoll Island's own",
            [51] = "the castaways of Milligan's Island talk",
            [52] = "a bottle floats in",
            [53] = "the hourglass's sand runs",
            [54] = "the wedding in the Great Hall, turn by turn",
            [55] = "the makeup eats through the chain",
            [56] = "the witch turns you inside out",
            [57] = "the castle falls",
            [58] = "the small voice hurries you",
            [59] = "your squire calls you out",
            [60] = "the Sergeant-At-Arms",
            [61] = "the knights' meeting",
            [62] = "the bard, for a copper penny",
            [63] = "the bard, for a silver penny",
            [64] = "the bard finishes his song",
            [65] = "the bard's song, which listen asks about",
            [66] = "the feasters' food fight",
            [67] = "the drunken knights fight",
            [68] = "the vanilla melts",
            [69] = "the delivery boy arrives",
            [70] = "the Shooting Gallery's own",
            [71] = "something is tossed to the jugglers",
            [72] = "the clock strikes noon, and the quest is over",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "takes some",
            [2] = "its name takes an",
            [4] = "its name takes no article",
            [5] = "touched",
            [6] = "its routine's own mark",
            [7] = "plural",
            [8] = "seen",
            [10] = "can be taken, and hasn't been",
            [11] = "not listed",
            [12] = "alive",
            [13] = "lit",
            [14] = "open",
            [15] = "can be opened and closed",
            [16] = "things can be on it",
            [17] = "what's dropped in it lands outside",
            [18] = "female",
            [19] = "can be eaten or drunk",
            [20] = "a door",
            [21] = "a liquid",
            [22] = "a container",
            [23] = "burns",
            [24] = "cuts",
            [25] = "can't answer",
            [26] = "can be taken",
            [27] = "cuts",
            [28] = "can be read",
            [29] = "locked",
            [31] = "a key",
            [32] = "got out of before you go",
            [33] = "can be seen through",
            [34] = "outdoors",
            [35] = "turned on",
            [36] = "can be turned on and off",
            [37] = "can be got in or on",
            [39] = "can be worn",
            [40] = "can be spoken to",
        },
        PlaceFlagNames: new Dictionary<int, string> { [5] = "visited", [13] = "has light" },
        PersonFlagNames: new Dictionary<int, string>(),
        StateFlags: [13, 14, 29, 35],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(24, Lists: 0x0A, ExitCount: 0x14, ExitStart: 0x16, Names: 4),
            [2] = new(36, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14, FlagBytes: 6, Names: 4),
            [3] = new(12, Names: 4),
            [4] = new(20, Lists: 0x0A, Names: 4),
            [5] = new(22, Lists: 0x0C, FlagBytes: 4, Names: 4),
            [6] = new(26, Places: 0x0C, Lists: 0x10, FlagBytes: 4, Names: 4),
            [7] = new(32, Places: 0x0C, Relation: 0x0E, Next: 0x10, Lists: 0x12, FlagBytes: 4, Names: 4),
            [8] = new(22, Places: 0x0C, Relation: 0x0E, Next: 0x10, FlagBytes: 4, Names: 4),
        },
        TableOffset: 0x00B8, TypesSegment: 0x2B79, NamesFile: "OBJECT.DAT",
        FlagsSegment: 0x2B79, FlagsOffset: 0x0014, FlagCount: 40,
        ExitsSegment: 0x2B79, ExitsOffset: 0x1CF2, DoorsSegment: 0x2B79, DoorsOffset: 0x2006,
        WideGrammar: true, TimerSize: 4, ClockStep: 1,
        TokenizerSegment: 0x31DF, TokenizerOffset: 0x01F4,
        FoundSegment: 0x016C);

    /// <summary>
    /// <i>Gateway II: Homeworld</i> (1993): the system built again by a later compiler, with
    /// records of a new design in seven types, the action and its objects in the data segment
    /// where the games before kept them far, and no doors among its exits.
    /// </summary>
    public static WorldMap Gateway2 { get; } = new(
        DataSegment: 0x4B4C, TableSegment: 0x27D7, ObjectCount: 521, CountOffset: 0x3336, TypesOffset: 0xFFFD,
        RoomOffset: 0x314E, ActorOffset: 0x3150, MovesOffset: 0x3154, ClockOffset: 0x3156,
        ActionSegment: 0x3C73, ActionOffset: 0x9132, ObjectOffset: 0x9084, SecondObjectOffset: 0x9082,
        PerformSegment: 0x0000, PerformOffset: 0x0184, DepthOffset: 0x3120,
        Answers:
        [
            // the same ten calls, in the same order, closer together
            new(0x02ED, Asked.Object), new(0x0310, Asked.Object), new(0x033E, Asked.Object), new(0x0355, Asked.Object),
            new(0x0368, Asked.Before),
            new(0x0394, Asked.Object), new(0x03AB, Asked.Object), new(0x03D7, Asked.Object), new(0x03EE, Asked.Object),
            new(0x0401, Asked.Action),
        ],
        JumpBackOffset: 0x0750, GoAction: 134, DirectionOffset: 0x9133,
        Directions: Spellcasting101.Directions,
        GrammarSegment: 0x2B9A, FormsOffset: 0x045E, SecondsOffset: 0x1354, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [134] = "go" },
        // the routine that gathers the tokens is in overlay 2, with the one that takes a word off the line
        TokenSegment: 0x3246, TokenOffset: 0x20F9, AfterStopOffset: 0x218A, AfterAndOffset: 0x2208, TokenWordOffset: 0x9086,
        WordTokens: Spellcasting101.WordTokens, MarkTokens: Spellcasting101.MarkTokens, EndTokens: Spellcasting101.EndTokens,
        FormOffset: 0x0C5C, FormIndexOffset: 0x9092, FormActionOffset: 0x3AE6, FoundOffset: 0x0A2B, LotsOffset: 0x3ACE, LotSize: 12, LotCountOffset: 0x3AC8,
        TimersOffset: 0x9010, TimerCountOffset: 0x3136, TimerIndexOffset: 0x3138,
        TimerSegment: 0x0234, TimerStartOffset: 0x0026, TimerStopOffset: 0x0096, TimerRunOffset: 0x01F1,
        TimerNames: new Dictionary<int, string>
        {
            [1] = "a question, which yes and no end",
            [2] = "the pictures fade away",
            [3] = "nothing",
            [4] = "nothing",
            [5] = "the artist's own",
            [6] = "a diggle crawls by",
            [7] = "the hypo unit's own, every turn",
            [8] = "the crystal rod and the portal change color",
            [9] = "a button pops out",
            [10] = "a screen goes wild",
            [11] = "a tentacle grabs the kord",
            [12] = "the insect creatures attack, turn by turn",
            [13] = "the creatures enter the specimen cage",
            [14] = "the creatures leave the specimen cage",
            [15] = "the creatures in the cage slow and die",
            [16] = "the creatures scrabble again",
            [17] = "the green blob, turn by turn",
            [18] = "the carnivores in the clearing",
            [19] = "the fruit pod's own",
            [20] = "the lizard takes the red egg",
            [21] = "nothing",
            [22] = "the man with the thermos, turn by turn",
            [23] = "your Heechee escort hurries you on",
            [24] = "the lecture hall's light turns green",
            [25] = "your communicator pings",
            [26] = "your escort grows impatient",
            [27] = "the Heechee city's own",
            [28] = "the students and the hologram",
            [29] = "the ape men in the Cavern",
            [30] = "the ape men in the Grasslands",
            [31] = "the spear at the River Bank",
            [32] = "the alien robot comes, click-clack",
            [33] = "the guidething wakes and speaks",
            [34] = "the ship's computer speaks, turn by turn",
            [35] = "the Place of Seeing, turn by turn",
            [36] = "the field generator's own",
            [37] = "the metal disk seals, and you fall",
            [38] = "a probe crashes",
            [39] = "something falls from the sky",
            [40] = "the technicians flood out of the temple",
            [41] = "the technicians swarm in",
            [42] = "the speaker speaks",
            [43] = "the beams of light shift back",
            [44] = "the workthing and the case, turn by turn",
            [45] = "the workthing comes back from the vault",
            [46] = "the tram, turn by turn",
            [47] = "the prototype's power runs low",
            [48] = "the prototype's battery recharges",
            [49] = "sinking into the slime",
            [50] = "the PV goes dormant",
            [51] = "the escort and the red tag",
            [52] = "a voice in your condominium",
            [53] = "the terrorists' attack, turn by turn",
            [54] = "the grenade explodes",
            [55] = "the match's own",
            [56] = "the radio's messages, turn by turn",
            [57] = "the hijacking's own",
            [58] = "a strange creature emerges",
        },
        PlaceType: 1, PersonType: 7,
        FlagNames: new Dictionary<int, string>
        {
            [1] = "takes some",
            [3] = "its name takes no article",
            [4] = "plural",
            [5] = "its name takes an",
            [6] = "its routine's own mark",
            [7] = "touched",
            [8] = "seen",
            [9] = "can be taken, and hasn't been",
            [10] = "not listed",
            [11] = "alive",
            [12] = "lit",
            [13] = "can't answer",
            [15] = "things can be on it",
            [16] = "a door",
            [17] = "can be taken",
            [18] = "can be read",
            [19] = "can be opened and closed",
            [20] = "locked",
            [21] = "open",
            [22] = "can be turned on and off",
            [23] = "a liquid",
            [24] = "a key",
            [25] = "cuts",
            [26] = "its routine's other mark",
            [27] = "can be eaten",
            [28] = "burns",
            [29] = "turned on",
            [32] = "can be got in or on",
            [33] = "a weapon",
            [34] = "can be worn",
            [35] = "a person",
            [36] = "a container",
            [37] = "what's dropped in it lands outside",
            [38] = "female",
            [39] = "outdoors",
            [41] = "can be sat on",
            [42] = "can be folded",
        },
        PlaceFlagNames: new Dictionary<int, string> { [7] = "visited", [12] = "has light" },
        PersonFlagNames: new Dictionary<int, string>(),
        StateFlags: [12, 20, 21, 29],
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(22, Lists: 0x06, ExitCount: 0x0C, ExitStart: 0x0E, Names: 4),
            [2] = new(28, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Lists: 0x10, FlagBytes: 6, Names: 4),
            [3] = new(32, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Lists: 0x10, FlagBytes: 6, Names: 4),
            [4] = new(12, Names: 4),
            [5] = new(18, Lists: 0x0A, Names: 4),
            [6] = new(18, Lists: 0x0A, FlagBytes: 6, Names: 4),
            [7] = new(34, Places: 0x0A, Relation: 0x0C, Next: 0x0E, Lists: 0x10, FlagBytes: 6, Names: 4),
        },
        TableOffset: 0x00BE, TypesSegment: 0x27D7, NamesFile: "OBJECT.DAT",
        FlagsSegment: 0x27D7, FlagsOffset: 0x0012, FlagCount: 42,
        ExitsSegment: 0x27D7, ExitsOffset: 0x346A,
        WideGrammar: true, TimerSize: 4, ClockStep: 1,
        TokenizerSegment: 0x3246, TokenizerOffset: 0x01BE,
        ExitTextsSegment: 0x27D7, ExitTextsOffset: 0x3BAE, ExitVariablesSegment: 0x27D7, ExitVariablesOffset: 0x3BFA,
        FoundSegment: 0x0135, ExitVariableAt: 4, StateSegment: 0x4B4C);
}

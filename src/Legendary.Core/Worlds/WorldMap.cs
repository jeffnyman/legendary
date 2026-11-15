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
/// <param name="FlagBytes">How many bytes of flags it has, from offset 8.</param>
/// <param name="ExitCount">A room's: a byte, how many exits it has.</param>
/// <param name="Exits">A room's: a far pointer to its exits, six bytes each.</param>
public sealed record RecordLayout(
    int Size, int Places = -1, int Relation = -1, int Next = -1, int Lists = -1, int FlagBytes = 2, int ExitCount = -1, int Exits = -1);

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
    IReadOnlyDictionary<int, RecordLayout> Layouts)
{
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
}

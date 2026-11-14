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
public sealed record RecordLayout(int Size, int Places = -1, int Relation = -1, int Next = -1, int Lists = -1);

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
/// <param name="Layouts">The records' layouts, by type.</param>
public sealed record WorldMap(
    ushort DataSegment, ushort TableSegment, int ObjectCount, ushort CountOffset, ushort TypesOffset,
    ushort RoomOffset, ushort ActorOffset, ushort MovesOffset, ushort ClockOffset,
    ushort ActionSegment, ushort ActionOffset, ushort ObjectOffset, ushort SecondObjectOffset,
    ushort PerformSegment, ushort PerformOffset, ushort DepthOffset, IReadOnlyList<AnswerPoint> Answers,
    ushort JumpBackOffset,
    ushort GrammarSegment, ushort FormsOffset, ushort SecondsOffset, string VocabularyFile,
    IReadOnlyDictionary<int, string> ActionNames,
    IReadOnlyDictionary<int, RecordLayout> Layouts)
{
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
        JumpBackOffset: 0x2916,
        GrammarSegment: 0x2B6A, FormsOffset: 0x00D1, SecondsOffset: 0x0F81, VocabularyFile: "VOCAB.DAT",
        ActionNames: new Dictionary<int, string> { [197] = "go" },
        Layouts: new Dictionary<int, RecordLayout>
        {
            [1] = new(28, Lists: 0x0E),
            [2] = new(36, Places: 0x12, Relation: 0x14, Next: 0x16, Lists: 0x18),
            [3] = new(14),
            [4] = new(22, Lists: 0x0E),
            [5] = new(22, Lists: 0x0E),
            [6] = new(26, Places: 0x0E, Lists: 0x12),
            [7] = new(32, Places: 0x0E, Relation: 0x10, Next: 0x12, Lists: 0x14),
            [8] = new(22, Places: 0x0E, Relation: 0x10, Next: 0x12),
        });
}

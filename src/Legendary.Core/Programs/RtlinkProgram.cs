using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Programs;

/// <summary>
/// A list of fixups in a record: what they are, and the places, counted from the record's start.
/// </summary>
/// <param name="Flags">What kind they are, and what goes at each place.</param>
/// <param name="Frame">The frame's number, when the flags say one is named.</param>
/// <param name="Target">The target's.</param>
/// <param name="Addend">A number to add to what goes there.</param>
/// <param name="Places">The places.</param>
public sealed record RtlinkFixups(ushort Flags, int Frame, int Target, int Addend, IReadOnlyList<int> Places)
{
    /// <summary>They name a frame and a target, and aren't plain relocations.</summary>
    public bool Named => (Flags & 0x0001) != 0;
}

/// <summary>A stretch of a segment, as .RTLink keeps a program: its bytes and its fixups.</summary>
/// <param name="Segment">The segment's number in the program's table, from 1.</param>
/// <param name="Offset">How far into the segment the stretch starts.</param>
/// <param name="Length">How many bytes it is.</param>
/// <param name="Data">The bytes, or null when the stretch is only cleared.</param>
/// <param name="Fixups">The lists of fixups.</param>
public sealed record RtlinkRecord(int Segment, int Offset, int Length, ReadOnlyMemory<byte>? Data, IReadOnlyList<RtlinkFixups> Fixups);

/// <summary>
/// A program linked by .RTLink/Plus (Pocket Soft), with its run-time library and overlays. The
/// MZ header describes only a stub, which reads RTLINKST.COM, the linker's startup code, and
/// jumps into it; that code does the loading. The image starts with a header of the linker's
/// own, and the program is kept after it as records, a stretch of one segment each, which go
/// where a table of segments says. A record's fixups say which words in it hold segments. The
/// library is a file of more such records, which go by the program's own table, and whose
/// fixups link it to the program. The overlays and their thunks are in the segment the
/// program starts in, which is the overlay manager's.
/// </summary>
public sealed class RtlinkProgram : OverlaidProgram
{
    // how the stub begins: call <find the startup code> / jmp far cs:[4]
    private static ReadOnlySpan<byte> Entry => [0xE8, 0x05, 0x00, 0x2E, 0xFF, 0x2E, 0x04, 0x00];

    // the one version whose flags are read here, the library's first word, and the sizes of
    // an overlay's entry in the manager's table and of a thunk
    private const int Version = 0x019A, LibraryMagic = 0x37BA, EntrySize = 18, ThunkSize = 10;

    // a list of fixups' flags: named; the value counted from the frame and not from the
    // place; what is put there; a number to add comes with the places; what the target's
    // number is of, and the frame's; a count of places follows; the word is shifted up
    // twelve bits before the segment is added
    private const int Named = 0x0001, FromFrame = 0x0002, Kind = 0x001C, HasAddend = 0x0020;
    private const int TargetKind = 0x01C0, FrameKind = 0x0E00, Many = 0x1000, Shifted = 0x4000;

    // what a frame's or a target's number is of
    private const int OfSegment = 0, OfGroup = 1, OfSymbol = 2, Absolute = 3;

    private readonly byte[] _memory;
    private readonly HashSet<int> _relocations = [];
    private readonly int[] _segments;
    private readonly int[] _groups;
    private readonly int _symbols;
    private readonly int _symbolCount;

    /// <summary>
    /// Reads a program from the bytes of its .EXE, its .OVL when it has one, and its run-time
    /// library when it names one.
    /// </summary>
    /// <exception cref="InvalidDataException">The .EXE isn't a program this version of .RTLink linked.</exception>
    public RtlinkProgram(byte[] exe, byte[]? ovl = null, byte[]? library = null)
        : base(exe, ovl)
    {
        if (!IsRtlink(exe))
        {
            throw new InvalidDataException("Not a .RTLink program: it doesn't start in .RTLink's stub.");
        }

        var image = Mz.Image.Span;
        LinkerVersion = Word(image, 0x34);
        if (LinkerVersion != Version)
        {
            throw new InvalidDataException($"Linked by a version of .RTLink that isn't known here: {LinkerVersion:X4}.");
        }

        StartOffset = Word(image, 0);
        StartSegment = Word(image, 2);
        ushort stackOffset = Word(image, 4), stackSegment = Word(image, 6);
        _segments = Addresses(image, Word(image, 8), Word(image, 0x0A));
        _groups = Addresses(image, Word(image, 0x0C), Word(image, 0x0E));
        _symbols = Word(image, 0x10);
        _symbolCount = Word(image, 0x12);
        MemoryEndSegment = (ushort)(stackSegment + (stackOffset + 15) / 16);
        _memory = new byte[MemoryEndSegment * 16];

        Records = ReadRecords(Mz.Image, Word(image, 0x14), Word(image, 0x16), out _);
        LibraryName = NameOfLibrary(exe);
        if (LibraryName is not null)
        {
            LibraryRecords = ReadLibrary(
                library ?? throw new InvalidDataException($"Its run-time library, {LibraryName}, wasn't given."),
                Word(image, 0x26));
        }

        foreach (var record in Records.Concat(LibraryRecords))
        {
            Place(record);
        }

        LoaderSegment = TableSegment = StartSegment;
        (Overlays, Thunks, OvlName) = ReadManager();
    }

    /// <summary>Whether a program's bytes start in .RTLink's stub.</summary>
    public static bool IsRtlink(byte[] exe)
    {
        ArgumentNullException.ThrowIfNull(exe);
        MzExecutable mz;
        try
        {
            mz = new MzExecutable(exe);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        int entry = mz.Cs * 16 + mz.Ip;
        var image = mz.Image.Span;
        return entry + Entry.Length <= image.Length && image.Slice(entry, Entry.Length).SequenceEqual(Entry);
    }

    /// <summary>The run-time library a .RTLink program names, or null when it names none.</summary>
    public static string? NameOfLibrary(byte[] exe)
    {
        var image = new MzExecutable(exe).Image.Span;
        if (image.Length < 0x51 || image[0x1C] != 1)
        {
            return null;
        }

        var name = image.Slice(0x1D, 8);
        int end = name.IndexOf((byte)0);
        return $"{Encoding.ASCII.GetString(end < 0 ? name : name[..end])}.{Encoding.ASCII.GetString(image.Slice(0x4E, 3))}";
    }

    /// <summary>
    /// Reads a program from its .EXE, with the .OVL and the run-time library it names from the
    /// same folder, whatever case the files' names are in.
    /// </summary>
    public static new RtlinkProgram Load(string exePath)
    {
        byte[] exe = File.ReadAllBytes(exePath);
        byte[]? library = NameOfLibrary(exe) is { } name && Beside(exePath, name) is { } path ? File.ReadAllBytes(path) : null;
        var withoutOverlays = new RtlinkProgram(exe, null, library);
        return withoutOverlays.OvlName.Length > 0 && Beside(exePath, withoutOverlays.OvlName) is { } ovl
            ? new RtlinkProgram(exe, File.ReadAllBytes(ovl), library)
            : withoutOverlays;
    }

    /// <inheritdoc/>
    public override string Linker => ".RTLink/Plus";

    /// <summary>The linker's version, as the header has it: 019A is, by the look of it, 4.10.</summary>
    public ushort LinkerVersion { get; }

    /// <summary>The segment of the program's first instruction, which is the overlay manager's.</summary>
    public ushort StartSegment { get; }

    /// <summary>The offset of the program's first instruction.</summary>
    public ushort StartOffset { get; }

    /// <summary>How many segments the program's table has.</summary>
    public int SegmentCount => _segments.Length;

    /// <summary>The library's name, or null when the program names none.</summary>
    public string? LibraryName { get; }

    /// <summary>The program's own records.</summary>
    public IReadOnlyList<RtlinkRecord> Records { get; }

    /// <summary>The library's records, which go by the program's table of segments.</summary>
    public IReadOnlyList<RtlinkRecord> LibraryRecords { get; } = [];

    /// <summary>Where a segment starts in memory, by its number in the table, from 1.</summary>
    public int SegmentStart(int number) =>
        number >= 1 && number <= _segments.Length
            ? _segments[number - 1]
            : throw new InvalidDataException($"A record names segment {number}, and there are {_segments.Length}.");

    /// <summary>
    /// The records from a place in a file, and where they end. A record is numbers, each one
    /// to four bytes long: a segment's number, a count of bytes, how far into the segment
    /// they go, 1 when the bytes follow and 0 when the stretch is only cleared, the bytes, and
    /// a count of lists of fixups with the lists.
    /// </summary>
    public static IReadOnlyList<RtlinkRecord> ReadRecords(ReadOnlyMemory<byte> file, int at, int count, out int end)
    {
        var reader = new Reader(file, at);
        var records = new List<RtlinkRecord>(count);
        for (int i = 0; i < count; i++)
        {
            int segment = reader.Number(), length = reader.Number(), offset = reader.Number();
            // null said outright: left to be converted, it would be an array's, and so no bytes rather than none
            ReadOnlyMemory<byte>? data = reader.Number() != 0 ? reader.Take(length) : (ReadOnlyMemory<byte>?)null;
            var fixups = new List<RtlinkFixups>();
            for (int lists = reader.Number(); lists > 0; lists--)
            {
                ReadFixups(ref reader, fixups);
            }

            records.Add(new RtlinkRecord(segment, offset, length, data, fixups));
        }

        end = reader.At;
        return records;
    }

    /// <inheritdoc/>
    protected override byte[] Root(out HashSet<int> relocations)
    {
        relocations = [.. _relocations];
        return (byte[])_memory.Clone();
    }

    // one list's fixups: one with an addend can be several, each with its own places
    private static void ReadFixups(ref Reader reader, List<RtlinkFixups> fixups)
    {
        ushort flags = reader.Word();
        int frame = 0, target = 0;
        if ((flags & Named) != 0)
        {
            frame = reader.Number();
            target = reader.Number();
        }

        bool addends = (flags & HasAddend) != 0;
        for (int sets = addends && (flags & Many) != 0 ? reader.Number() : 1; sets > 0; sets--)
        {
            int addend = 0;
            bool many = (flags & Many) != 0;
            if (addends)
            {
                int number = reader.Number();
                many = (number & 1) != 0;
                addend = number >> 1;
            }

            int[] places = new int[many ? reader.Number() : 1];
            for (int i = 0; i < places.Length; i++)
            {
                places[i] = reader.Number();
            }

            fixups.Add(new RtlinkFixups(flags, frame, target, addend, places));
        }
    }

    private static int[] Addresses(ReadOnlySpan<byte> image, int at, int count)
    {
        int[] addresses = new int[count];
        for (int i = 0; i < count; i++)
        {
            addresses[i] = Word(image, at + 4 * i + 2) * 16 + Word(image, at + 4 * i);
        }

        return addresses;
    }

    private IReadOnlyList<RtlinkRecord> ReadLibrary(byte[] library, ushort stamp)
    {
        if (library.Length < 0x12 || Word(library, 0) != LibraryMagic)
        {
            throw new InvalidDataException($"{LibraryName} isn't a .RTLink run-time library.");
        }

        if (Word(library, 4) != stamp || Word(library, 8) != LinkerVersion)
        {
            throw new InvalidDataException($"{LibraryName} isn't the library the program was linked with.");
        }

        var records = ReadRecords(library, Word(library, 0x10) * 16, Word(library, 6), out int end);
        return end == library.Length
            ? records
            : throw new InvalidDataException($"{LibraryName}'s records don't end where its file does.");
    }

    // where a frame or a target is, from what its number is of: a base, and how far past it.
    // A frame is the base alone, and a symbol's base is its group, or its segment when it has none
    private (int Base, int Past) Resolve(int kind, int number)
    {
        switch (kind)
        {
            case OfSegment:
                return (SegmentStart(number), 0);
            case OfGroup when number >= 1 && number <= _groups.Length:
                return (_groups[number - 1], 0);
            case Absolute:
                return (number * 16, 0);
            case OfSymbol when number >= 1 && number <= _symbolCount:
                var symbol = Mz.Image.Span.Slice(_symbols + 8 * (number - 1), 8);
                if (symbol[0] != (byte)'R')
                {
                    break;
                }

                int group = Word(symbol, 1), where = SegmentStart(Word(symbol, 3)) + Word(symbol, 5);
                int start = group != 0 ? _groups[group - 1] : SegmentStart(Word(symbol, 3));
                return (start, where - start);
        }

        throw new InvalidDataException($"A fixup names something that isn't known here: kind {kind}, number {number}.");
    }

    private ushort MemoryWord(int at) => BinaryPrimitives.ReadUInt16LittleEndian(_memory.AsSpan(at));

    private void Add(int at, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(_memory.AsSpan(at), (ushort)(MemoryWord(at) + value));

    // one named fixup, done as the startup code does it
    private void Fix(RtlinkFixups fixups, int at)
    {
        int flags = fixups.Flags, kind = (flags & Kind) >> 2, frameKind = (flags & FrameKind) >> 9;
        int frame = Resolve(frameKind, fixups.Frame).Base;
        var (targetBase, past) = Resolve((flags & TargetKind) >> 6, fixups.Target);
        int value = targetBase + past + fixups.Addend;

        // from the frame, or from the place itself, as a jump's or a call's operand is counted
        value -= (flags & FromFrame) != 0 ? frame & ~15 : at + (kind == 0 ? 1 : 2);
        switch (kind)
        {
            case 0:
                _memory[at] += (byte)value;
                return;
            case 4:
                _memory[at] += (byte)(value >> 8);
                return;
            case 1 or 3:
                Add(at, value);
                break;
        }

        if (kind is 2 or 3)
        {
            int where = kind == 3 ? at + 2 : at;
            if ((flags & Shifted) != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(_memory.AsSpan(where), (ushort)(MemoryWord(where) << 12));
            }

            Add(where, frame >> 4);
            if (frameKind != Absolute)
            {
                _relocations.Add(where);
            }
        }
    }

    private void Place(RtlinkRecord record)
    {
        int start = SegmentStart(record.Segment) + record.Offset;
        if (start + record.Length > _memory.Length)
        {
            throw new InvalidDataException($"Segment {record.Segment} goes past the end of the program's memory.");
        }

        record.Data?.CopyTo(_memory.AsMemory(start));
        foreach (var fixups in record.Fixups)
        {
            foreach (int place in fixups.Places)
            {
                if (fixups.Named)
                {
                    Fix(fixups, start + place);
                }
                else
                {
                    _relocations.Add(start + place);
                }
            }
        }
    }

    // the overlay manager's table and its thunks, found in the segment the program starts in
    private (IReadOnlyList<ProgramOverlay>, IReadOnlyList<ProgramThunk>, string) ReadManager()
    {
        int start = StartSegment * 16;
        int length = Records
            .Where(record => SegmentStart(record.Segment) == start)
            .Select(record => record.Offset + record.Length)
            .DefaultIfEmpty(0)
            .Max();
        var segment = _memory.AsSpan(start, length);

        // the manager says where its table is: where the count is, where the entries start,
        // and an entry's size, one after another
        int entries = -1, countAt = 0;
        for (int at = 0; at + 6 <= length && entries < 0; at += 2)
        {
            countAt = Word(segment, at);
            if (Word(segment, at + 4) == EntrySize && Word(segment, at + 2) == countAt + 4 && countAt + 4 + EntrySize <= length)
            {
                entries = countAt + 4;
            }
        }

        if (entries < 0)
        {
            return ([], [], "");
        }

        // the count is one more than there are
        var overlays = new List<ProgramOverlay>();
        for (int number = 1; number < Word(segment, countAt); number++)
        {
            int entry = entries + EntrySize * (number - 1);
            ushort load = Word(segment, entry), size = Word(segment, entry + 8);
            overlays.Add(new ProgramOverlay(
                Number: number,
                Status: Word(segment, entry + 6),
                RelocationCount: Word(segment, entry + 10),
                LoadSegment: load,
                EndSegment: (ushort)(load + size),
                FileParagraph: Word(segment, entry + 4),
                SizeParagraphs: size));
        }

        string name = "";
        int ending = segment[entries..].IndexOf(".OVL\0"u8);
        if (ending >= 0)
        {
            int from = segment[..(entries + ending)].LastIndexOf((byte)0) + 1;
            name = Encoding.ASCII.GetString(segment[from..(entries + ending + 4)]);
        }

        // the thunks run to the segment's end: call <the manager> / jmp far <the routine> /
        // dw <the overlay's number, from 0>, and every one calls the same place
        var thunks = new List<ProgramThunk>();
        int manager = -1;
        for (int at = length - ThunkSize; at >= 0 && segment[at] == 0xE8 && segment[at + 3] == 0xEA; at -= ThunkSize)
        {
            int called = (ushort)(at + 3 + Word(segment, at + 1)), index = Word(segment, at + 8);
            if (index >= overlays.Count || (manager >= 0 && called != manager))
            {
                break;
            }

            manager = called;
            thunks.Add(new ProgramThunk(StartSegment, (ushort)at, index + 1, Word(segment, at + 6), Word(segment, at + 4)));
        }

        thunks.Reverse();
        return (overlays, thunks, name);
    }

    // the linker's numbers, read one after another: a first byte under 80 is the number, and
    // otherwise its top bits say how many bytes follow, most significant first
    private struct Reader(ReadOnlyMemory<byte> file, int at)
    {
        public int At { get; private set; } = at;

        public int Number()
        {
            int first = Take(1).Span[0];
            if ((first & 0x80) == 0)
            {
                return first;
            }

            // 10xxxxxx has one byte after it, 110xxxxx two and 1110xxxx three
            int more = (first & 0x40) == 0 ? 1 : (first & 0x20) == 0 ? 2 : 3;
            if (more == 3 && (first & 0x10) != 0)
            {
                throw new InvalidDataException($"A number at {At - 1:X} isn't in a form .RTLink writes.");
            }

            int value = first & (0xFF >> (more + 1));
            foreach (byte next in Take(more).Span)
            {
                value = value << 8 | next;
            }

            return value;
        }

        public ushort Word() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2).Span);

        public ReadOnlyMemory<byte> Take(int count)
        {
            if (count < 0 || At + count > file.Length)
            {
                throw new InvalidDataException("A record goes past the end of its file.");
            }

            At += count;
            return file.Slice(At - count, count);
        }
    }
}

using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Programs;

/// <summary>An entry in the table PLINK86's overlay loader keeps.</summary>
/// <param name="Number">Its number, from 1, as the thunks have it.</param>
/// <param name="Status">The loader's flags for it, as the file has them.</param>
/// <param name="RelocationCount">How many places in it hold segments.</param>
/// <param name="LoadSegment">Where it loads, relative to the program's load segment.</param>
/// <param name="EndSegment">The first paragraph after it in memory.</param>
/// <param name="FileParagraph">Where its relocations start in its file, in paragraphs.</param>
/// <param name="SizeParagraphs">
/// The size of what the file holds for it; memory from there to <paramref name="EndSegment"/> starts as zero.
/// </param>
public sealed record Plink86Overlay(
    int Number,
    ushort Status,
    int RelocationCount,
    ushort LoadSegment,
    ushort EndSegment,
    int FileParagraph,
    int SizeParagraphs)
{
    /// <summary>The loader loads it as the program starts.</summary>
    public bool Preloaded => (Status & 0x4000) != 0;

    /// <summary>
    /// It is in the .EXE, after the root, and not in the .OVL. (Inferred: the program's data is
    /// the one overlay with this flag, and the one that's there.)
    /// </summary>
    public bool InExe => (Status & 0x2000) != 0;

    /// <summary>Where its contents start in its file: after the relocations, on a paragraph.</summary>
    public int ContentsOffset => FileParagraph * 16 + (RelocationCount * 4 + 15) / 16 * 16;
}

/// <summary>
/// A way into an overlay. The root calls the thunk, which calls the loader with the overlay's
/// number after the call, and the loader returns to a jump to the routine.
/// </summary>
/// <param name="Segment">The thunk's segment: the loader's table's.</param>
/// <param name="Offset">The thunk's offset.</param>
/// <param name="Overlay">The overlay's number.</param>
/// <param name="TargetSegment">The routine's segment.</param>
/// <param name="TargetOffset">The routine's offset.</param>
public readonly record struct Plink86Thunk(ushort Segment, ushort Offset, int Overlay, ushort TargetSegment, ushort TargetOffset);

/// <summary>
/// A program linked by PLINK86 (Phoenix Software Associates), with its overlays. The MZ header
/// describes only the root: the code that's always in memory, with the overlay loader in it. The
/// program starts in the loader, which has a table of the overlays; overlays that load at the
/// same place take each other's. The program's data is the last overlay, kept in the .EXE after
/// the root and loaded as the program starts. Every segment is relative to where the program
/// loads, as the files have them.
/// </summary>
public sealed class Plink86Program
{
    // how the program begins: cli / mov di,es / mov si,ss / mov cx,sp / mov dx,<the table's segment>
    private static ReadOnlySpan<byte> Entry => [0xFA, 0x8C, 0xC7, 0x8C, 0xD6, 0x8B, 0xCC, 0xBA];

    private readonly byte[]? _ovl;
    private readonly Dictionary<(ushort, ushort), Plink86Thunk> _thunksByAddress = [];

    /// <summary>Reads a program from the bytes of its .EXE and, when it has one, its .OVL.</summary>
    /// <exception cref="InvalidDataException">The .EXE isn't a program PLINK86 linked.</exception>
    public Plink86Program(byte[] exe, byte[]? ovl = null)
    {
        Mz = new MzExecutable(exe);
        _ovl = ovl;
        var root = Mz.Image.Span;
        int entry = Mz.Cs * 16 + Mz.Ip;
        if (entry + Entry.Length + 2 > root.Length || !root.Slice(entry, Entry.Length).SequenceEqual(Entry))
        {
            throw new InvalidDataException("Not a PLINK86 program: it doesn't start in PLINK86's loader.");
        }

        LoaderSegment = Mz.Cs;
        TableSegment = Word(root, entry + Entry.Length);
        int table = TableSegment * 16;
        MemoryEndSegment = Word(root, table + 4);

        // an entry's 16 bytes start 6 into the table: its status is the word the loader looks at
        var overlays = new List<Plink86Overlay>();
        int at = table;
        for (; Word(root, at + 6) != 0xFFFF; at += 16)
        {
            overlays.Add(new Plink86Overlay(
                Number: overlays.Count + 1,
                Status: Word(root, at + 6),
                RelocationCount: Word(root, at + 8),
                LoadSegment: Word(root, at + 10),
                EndSegment: Word(root, at + 12),
                FileParagraph: (int)BinaryPrimitives.ReadUInt32LittleEndian(root[(at + 16)..]),
                SizeParagraphs: Word(root, at + 20)));
        }

        Overlays = overlays;

        // after the table, the two files' names, then the thunks
        at += 8;
        ExeName = Name(root, ref at);
        OvlName = Name(root, ref at);

        var thunks = new List<Plink86Thunk>();
        for (; at + 12 <= root.Length && root[at] == 0x9A && root[at + 7] == 0xEA; at += 12)
        {
            var thunk = new Plink86Thunk(
                TableSegment, (ushort)(at - table), Word(root, at + 5), Word(root, at + 10), Word(root, at + 8));
            thunks.Add(thunk);
            _thunksByAddress[(thunk.Segment, thunk.Offset)] = thunk;
        }

        Thunks = thunks;
    }

    /// <summary>
    /// Reads a program from its .EXE, and the .OVL it names from the same folder, whatever
    /// case the file's name is in.
    /// </summary>
    public static Plink86Program Load(string exePath)
    {
        byte[] exe = File.ReadAllBytes(exePath);
        var withoutOverlays = new Plink86Program(exe);
        string folder = Path.GetDirectoryName(Path.GetFullPath(exePath))!;
        string? ovl = Directory.EnumerateFiles(folder)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), withoutOverlays.OvlName, StringComparison.OrdinalIgnoreCase));
        return ovl is null ? withoutOverlays : new Plink86Program(exe, File.ReadAllBytes(ovl));
    }

    /// <summary>The .EXE: its header, and the root as its image.</summary>
    public MzExecutable Mz { get; }

    /// <summary>The segment of PLINK86's loader, where the program starts.</summary>
    public ushort LoaderSegment { get; }

    /// <summary>The segment of the loader's table, with the thunks after it.</summary>
    public ushort TableSegment { get; }

    /// <summary>The first paragraph after the program's memory.</summary>
    public ushort MemoryEndSegment { get; }

    /// <summary>The .EXE's name, as the loader has it.</summary>
    public string ExeName { get; }

    /// <summary>The .OVL's name, as the loader has it.</summary>
    public string OvlName { get; }

    /// <summary>The overlays, in the table's order: an overlay's number is one more than its place.</summary>
    public IReadOnlyList<Plink86Overlay> Overlays { get; }

    /// <summary>The ways into the overlays, in the order they're in after the table.</summary>
    public IReadOnlyList<Plink86Thunk> Thunks { get; }

    /// <summary>The overlay with that number, from 1.</summary>
    public Plink86Overlay Overlay(int number) =>
        number >= 1 && number <= Overlays.Count
            ? Overlays[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number), number, $"The overlays go from 1 to {Overlays.Count}.");

    /// <summary>The thunk at an address, when there is one.</summary>
    public Plink86Thunk? ThunkAt(ushort segment, ushort offset) =>
        _thunksByAddress.TryGetValue((segment, offset), out var thunk) ? thunk : null;

    /// <summary>The places in an overlay that hold segments.</summary>
    public IReadOnlyList<Relocation> Relocations(Plink86Overlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var file = FileOf(overlay).Span;
        var relocations = new Relocation[overlay.RelocationCount];
        for (int i = 0; i < relocations.Length; i++)
        {
            int at = overlay.FileParagraph * 16 + i * 4;
            relocations[i] = new Relocation(Word(file, at + 2), Word(file, at));
        }

        return relocations;
    }

    /// <summary>An overlay's bytes as its file has them, to go at its load segment.</summary>
    public ReadOnlyMemory<byte> Contents(Plink86Overlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var file = FileOf(overlay);
        int size = overlay.SizeParagraphs * 16;
        if (overlay.ContentsOffset + size > file.Length)
        {
            throw new InvalidDataException($"Overlay {overlay.Number} goes past the end of its file.");
        }

        return file.Slice(overlay.ContentsOffset, size);
    }

    /// <summary>
    /// The program's memory with the root, the overlays loaded as it starts, and the ones
    /// numbered, each at its place. Segments stay relative to the load segment: nothing is added
    /// at the relocations.
    /// </summary>
    /// <exception cref="ArgumentException">Two of the overlays load over each other.</exception>
    public ProgramImage Image(params int[] overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        byte[] memory = new byte[MemoryEndSegment * 16];
        Mz.Image.CopyTo(memory);
        var relocations = new HashSet<int>(Mz.Relocations.Select(relocation => relocation.Linear));
        var loaded = new List<Plink86Overlay>();
        foreach (var overlay in Overlays.Where(overlay => overlay.Preloaded).Concat(overlays.Select(Overlay)))
        {
            if (loaded.Contains(overlay))
            {
                continue;
            }

            if (loaded.FirstOrDefault(other => overlay.LoadSegment < other.EndSegment && other.LoadSegment < overlay.EndSegment) is { } taken)
            {
                throw new ArgumentException($"Overlays {taken.Number} and {overlay.Number} load over each other.", nameof(overlays));
            }

            loaded.Add(overlay);
            Contents(overlay).CopyTo(memory.AsMemory(overlay.LoadSegment * 16));
            relocations.UnionWith(Relocations(overlay).Select(relocation => relocation.Linear));
        }

        return new ProgramImage(this, memory, relocations, loaded);
    }

    private ReadOnlyMemory<byte> FileOf(Plink86Overlay overlay) =>
        overlay.InExe
            ? Mz.File
            : _ovl ?? throw new InvalidOperationException($"Overlay {overlay.Number} is in {OvlName}, which wasn't given.");

    private static ushort Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);

    private static string Name(ReadOnlySpan<byte> bytes, ref int at)
    {
        int length = bytes[at..].IndexOf((byte)0);
        if (length < 0)
        {
            throw new InvalidDataException("The loader's table doesn't end in its files' names.");
        }

        string name = Encoding.ASCII.GetString(bytes.Slice(at, length));
        at += length + 1;
        return name;
    }
}

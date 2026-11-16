using System.Buffers.Binary;

namespace Legendary.Core.Programs;

/// <summary>An entry in the table a program's overlay loader keeps.</summary>
/// <param name="Number">Its number, from 1.</param>
/// <param name="Status">The loader's own word about it, as the file has it.</param>
/// <param name="RelocationCount">How many places in it hold segments.</param>
/// <param name="LoadSegment">Where it loads, relative to the program's load segment.</param>
/// <param name="EndSegment">The first paragraph after it in memory.</param>
/// <param name="FileParagraph">Where its relocations start in its file, in paragraphs.</param>
/// <param name="SizeParagraphs">
/// The size of what the file holds for it; memory from there to <paramref name="EndSegment"/> starts as zero.
/// </param>
/// <param name="Preloaded">The loader loads it as the program starts.</param>
/// <param name="InExe">It is in the .EXE, after the root, and not in the .OVL.</param>
public sealed record ProgramOverlay(
    int Number,
    ushort Status,
    int RelocationCount,
    ushort LoadSegment,
    ushort EndSegment,
    int FileParagraph,
    int SizeParagraphs,
    bool Preloaded = false,
    bool InExe = false)
{
    /// <summary>Where its contents start in its file: after the relocations, on a paragraph.</summary>
    public int ContentsOffset => FileParagraph * 16 + (RelocationCount * 4 + 15) / 16 * 16;
}

/// <summary>
/// A way into an overlay. The root calls the thunk, and the loader, with the overlay's number
/// from the thunk, has the overlay in memory before the routine is jumped to.
/// </summary>
/// <param name="Segment">The thunk's segment.</param>
/// <param name="Offset">The thunk's offset.</param>
/// <param name="Overlay">The overlay's number, from 1.</param>
/// <param name="TargetSegment">The routine's segment.</param>
/// <param name="TargetOffset">The routine's offset.</param>
public readonly record struct ProgramThunk(ushort Segment, ushort Offset, int Overlay, ushort TargetSegment, ushort TargetOffset);

/// <summary>
/// A program with overlays, whichever linker made it. The games were linked by two:
/// <i>Spellcasting 101</i>'s by PLINK86, and from <i>Spellcasting 201</i> by .RTLink/Plus. Both
/// keep a root that is always in memory, overlays that take each other's places, and thunks
/// the root calls to reach an overlay's routines; and both keep an overlay in the .OVL the
/// same way, its relocations and then its contents on a paragraph. They differ in how the
/// .EXE holds the root. Every segment is relative to where the program loads, as the files
/// have them.
/// </summary>
public abstract class OverlaidProgram
{
    private readonly byte[]? _ovl;
    private Dictionary<(ushort, ushort), ProgramThunk>? _thunksByAddress;

    /// <summary>A program from its .EXE's bytes and, when it has one, its .OVL's.</summary>
    protected OverlaidProgram(byte[] exe, byte[]? ovl)
    {
        Mz = new MzExecutable(exe);
        _ovl = ovl;
    }

    /// <summary>
    /// Reads a program from its .EXE and the files it names from the same folder, whatever
    /// case their names are in, as the linker that made it left it.
    /// </summary>
    /// <exception cref="InvalidDataException">The .EXE isn't a program either linker made.</exception>
    public static OverlaidProgram Load(string exePath) =>
        RtlinkProgram.IsRtlink(File.ReadAllBytes(exePath)) ? RtlinkProgram.Load(exePath) : Plink86Program.Load(exePath);

    /// <summary>The .EXE: its header, and what the header calls its image.</summary>
    public MzExecutable Mz { get; }

    /// <summary>The linker's name.</summary>
    public abstract string Linker { get; }

    /// <summary>The segment of the code that loads overlays.</summary>
    public ushort LoaderSegment { get; protected init; }

    /// <summary>The segment of the loader's table, with the thunks.</summary>
    public ushort TableSegment { get; protected init; }

    /// <summary>The first paragraph after the program's memory.</summary>
    public ushort MemoryEndSegment { get; protected init; }

    /// <summary>The .OVL's name, as the loader has it.</summary>
    public string OvlName { get; protected init; } = "";

    /// <summary>The overlays, in the table's order: an overlay's number is one more than its place.</summary>
    public IReadOnlyList<ProgramOverlay> Overlays { get; protected init; } = [];

    /// <summary>The ways into the overlays, in the order the program has them.</summary>
    public IReadOnlyList<ProgramThunk> Thunks { get; protected init; } = [];

    /// <summary>The overlay with that number, from 1.</summary>
    public ProgramOverlay Overlay(int number) =>
        number >= 1 && number <= Overlays.Count
            ? Overlays[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number), number, $"The overlays go from 1 to {Overlays.Count}.");

    /// <summary>The thunk at an address, when there is one.</summary>
    public ProgramThunk? ThunkAt(ushort segment, ushort offset)
    {
        _thunksByAddress ??= Thunks.ToDictionary(thunk => (thunk.Segment, thunk.Offset));
        return _thunksByAddress.TryGetValue((segment, offset), out var thunk) ? thunk : null;
    }

    /// <summary>The places in an overlay that hold segments.</summary>
    public IReadOnlyList<Relocation> Relocations(ProgramOverlay overlay)
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

    /// <summary>
    /// An overlay's bytes as its file has them, to go at its load segment. A file can stop
    /// short of the last overlay's size by less than a paragraph, and what is left is zeros.
    /// </summary>
    public ReadOnlyMemory<byte> Contents(ProgramOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var file = FileOf(overlay);
        int size = overlay.SizeParagraphs * 16;
        if (overlay.ContentsOffset + size > file.Length + 15)
        {
            throw new InvalidDataException($"Overlay {overlay.Number} goes past the end of its file.");
        }

        return file.Slice(overlay.ContentsOffset, Math.Min(size, file.Length - overlay.ContentsOffset));
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
        byte[] memory = Root(out var relocations);
        var loaded = new List<ProgramOverlay>();
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

    /// <summary>
    /// The program's memory with only the root in it, and the places in it that hold segments.
    /// </summary>
    protected abstract byte[] Root(out HashSet<int> relocations);

    /// <summary>The word at a place in some bytes.</summary>
    protected static ushort Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);

    /// <summary>The file with a name in a program's folder, its case aside; null when there is none.</summary>
    protected static string? Beside(string exePath, string name) =>
        Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(exePath))!)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));

    private ReadOnlyMemory<byte> FileOf(ProgramOverlay overlay) =>
        overlay.InExe
            ? Mz.File
            : _ovl ?? throw new InvalidOperationException($"Overlay {overlay.Number} is in {OvlName}, which wasn't given.");
}

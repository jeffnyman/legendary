using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Programs;

/// <summary>
/// A program linked by PLINK86 (Phoenix Software Associates), with its overlays. The MZ header
/// describes only the root: the code that's always in memory, with the overlay loader in it. The
/// program starts in the loader, which has a table of the overlays; overlays that load at the
/// same place take each other's. The program's data is the last overlay, kept in the .EXE after
/// the root and loaded as the program starts. Every segment is relative to where the program
/// loads, as the files have them.
/// </summary>
public sealed class Plink86Program : OverlaidProgram
{
    // how the program begins: cli / mov di,es / mov si,ss / mov cx,sp / mov dx,<the table's segment>
    private static ReadOnlySpan<byte> Entry => [0xFA, 0x8C, 0xC7, 0x8C, 0xD6, 0x8B, 0xCC, 0xBA];

    // an overlay's status: loaded as the program starts, and in the .EXE and not the .OVL
    // (inferred: the program's data is the one overlay with it, and the one that's there)
    private const int Preload = 0x4000, InExeFile = 0x2000;

    /// <summary>Reads a program from the bytes of its .EXE and, when it has one, its .OVL.</summary>
    /// <exception cref="InvalidDataException">The .EXE isn't a program PLINK86 linked.</exception>
    public Plink86Program(byte[] exe, byte[]? ovl = null)
        : base(exe, ovl)
    {
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
        var overlays = new List<ProgramOverlay>();
        int at = table;
        for (; Word(root, at + 6) != 0xFFFF; at += 16)
        {
            ushort status = Word(root, at + 6);
            overlays.Add(new ProgramOverlay(
                Number: overlays.Count + 1,
                Status: status,
                RelocationCount: Word(root, at + 8),
                LoadSegment: Word(root, at + 10),
                EndSegment: Word(root, at + 12),
                FileParagraph: (int)BinaryPrimitives.ReadUInt32LittleEndian(root[(at + 16)..]),
                SizeParagraphs: Word(root, at + 20),
                Preloaded: (status & Preload) != 0,
                InExe: (status & InExeFile) != 0));
        }

        Overlays = overlays;

        // after the table, the two files' names, then the thunks
        at += 8;
        ExeName = Name(root, ref at);
        OvlName = Name(root, ref at);

        var thunks = new List<ProgramThunk>();
        for (; at + 12 <= root.Length && root[at] == 0x9A && root[at + 7] == 0xEA; at += 12)
        {
            thunks.Add(new ProgramThunk(
                TableSegment, (ushort)(at - table), Word(root, at + 5), Word(root, at + 10), Word(root, at + 8)));
        }

        Thunks = thunks;
    }

    /// <summary>
    /// Reads a program from its .EXE, and the .OVL it names from the same folder, whatever
    /// case the file's name is in.
    /// </summary>
    public static new Plink86Program Load(string exePath)
    {
        byte[] exe = File.ReadAllBytes(exePath);
        var withoutOverlays = new Plink86Program(exe);
        return Beside(exePath, withoutOverlays.OvlName) is { } ovl ? new Plink86Program(exe, File.ReadAllBytes(ovl)) : withoutOverlays;
    }

    /// <inheritdoc/>
    public override string Linker => "PLINK86";

    /// <summary>The .EXE's name, as the loader has it.</summary>
    public string ExeName { get; }

    /// <inheritdoc/>
    protected override byte[] Root(out HashSet<int> relocations)
    {
        byte[] memory = new byte[MemoryEndSegment * 16];
        Mz.Image.CopyTo(memory);
        relocations = [.. Mz.Relocations.Select(relocation => relocation.Linear)];
        return memory;
    }

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

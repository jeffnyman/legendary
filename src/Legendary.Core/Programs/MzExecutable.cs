using System.Buffers.Binary;

namespace Legendary.Core.Programs;

/// <summary>
/// A place in a program's image that holds a segment, which DOS adds the load segment to.
/// </summary>
/// <param name="Segment">The segment of the place, relative to where the image loads.</param>
/// <param name="Offset">Its offset in that segment.</param>
public readonly record struct Relocation(ushort Segment, ushort Offset)
{
    /// <summary>The place as a count of bytes from the start of the image.</summary>
    public int Linear => Segment * 16 + Offset;
}

/// <summary>
/// A DOS program as its file has it: the MZ header, the image it describes, and the
/// relocations. Segments are relative to where the image loads.
/// </summary>
public sealed class MzExecutable
{
    private readonly byte[] _file;

    /// <summary>Reads a program from its file's bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't an MZ program.</exception>
    public MzExecutable(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < 0x1C || !(file.AsSpan(0, 2).SequenceEqual("MZ"u8) || file.AsSpan(0, 2).SequenceEqual("ZM"u8)))
        {
            throw new InvalidDataException("Not an MZ program.");
        }

        _file = file;
        ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at));

        int lastPage = Word(0x02), pages = Word(0x04), relocationCount = Word(0x06);
        HeaderSize = Word(0x08) * 16;
        MinAlloc = Word(0x0A);
        MaxAlloc = Word(0x0C);
        Ss = Word(0x0E);
        Sp = Word(0x10);
        Ip = Word(0x14);
        Cs = Word(0x16);
        int relocationOffset = Word(0x18);

        // the last page's count is of the bytes used in it, and 0 means all of it
        ImageEnd = pages * 512 - (512 - lastPage) % 512;
        if (HeaderSize > ImageEnd || ImageEnd > file.Length || relocationOffset + relocationCount * 4 > HeaderSize)
        {
            throw new InvalidDataException("The MZ header describes more than the file holds.");
        }

        var relocations = new Relocation[relocationCount];
        for (int i = 0; i < relocationCount; i++)
        {
            int at = relocationOffset + i * 4;
            relocations[i] = new Relocation(Word(at + 2), Word(at));
        }

        Relocations = relocations;
    }

    /// <summary>The whole file.</summary>
    public ReadOnlyMemory<byte> File => _file;

    /// <summary>The size of the header, in bytes: where the image starts in the file.</summary>
    public int HeaderSize { get; }

    /// <summary>Where the image the header describes ends in the file.</summary>
    public int ImageEnd { get; }

    /// <summary>The image: what DOS loads.</summary>
    public ReadOnlyMemory<byte> Image => _file.AsMemory(HeaderSize, ImageEnd - HeaderSize);

    /// <summary>The bytes in the file after the image, which DOS doesn't load.</summary>
    public int TrailingSize => _file.Length - ImageEnd;

    /// <summary>The image's size in paragraphs, rounded up.</summary>
    public int ImageParagraphs => (ImageEnd - HeaderSize + 15) / 16;

    /// <summary>The paragraphs the program needs above its image.</summary>
    public ushort MinAlloc { get; }

    /// <summary>The paragraphs the program asks for above its image.</summary>
    public ushort MaxAlloc { get; }

    /// <summary>The stack's segment as the program starts.</summary>
    public ushort Ss { get; }

    /// <summary>The stack pointer as the program starts.</summary>
    public ushort Sp { get; }

    /// <summary>The segment the program starts in.</summary>
    public ushort Cs { get; }

    /// <summary>The offset the program starts at.</summary>
    public ushort Ip { get; }

    /// <summary>The places in the image that hold segments.</summary>
    public IReadOnlyList<Relocation> Relocations { get; }
}

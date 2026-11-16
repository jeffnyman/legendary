using System.Buffers.Binary;

namespace Legendary.Core.Programs;

/// <summary>
/// A program's memory with some of its overlays loaded, as it would be at a load segment of
/// zero: the segments in it are as the files have them.
/// </summary>
public sealed class ProgramImage
{
    private readonly byte[] _memory;
    private readonly HashSet<int> _relocations;

    internal ProgramImage(OverlaidProgram program, byte[] memory, HashSet<int> relocations, IReadOnlyList<ProgramOverlay> loaded)
    {
        Program = program;
        _memory = memory;
        _relocations = relocations;
        Loaded = loaded;
    }

    /// <summary>The program this is the memory of.</summary>
    public OverlaidProgram Program { get; }

    /// <summary>The memory, from the program's first byte.</summary>
    public ReadOnlyMemory<byte> Memory => _memory;

    /// <summary>The overlays that are loaded.</summary>
    public IReadOnlyList<ProgramOverlay> Loaded { get; }

    /// <summary>The bytes from an address on.</summary>
    public ReadOnlySpan<byte> At(ushort segment, ushort offset) => _memory.AsSpan(segment * 16 + offset);

    /// <summary>The word at an address.</summary>
    public ushort Word(ushort segment, ushort offset) => BinaryPrimitives.ReadUInt16LittleEndian(At(segment, offset));

    /// <summary>Whether the word at a place, counted in bytes from the start, holds a segment.</summary>
    public bool IsRelocation(int linear) => _relocations.Contains(linear);

    /// <summary>The overlay a segment is in, or null for the root and for what isn't loaded.</summary>
    public ProgramOverlay? OverlayAt(ushort segment) =>
        Loaded.FirstOrDefault(overlay => overlay.LoadSegment <= segment && segment < overlay.EndSegment);
}

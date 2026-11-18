using System.Buffers.Binary;

namespace Legendary.Core.Sound;

/// <summary>A song of a music file, in one of its two formats.</summary>
/// <param name="Number">Its entry in the file, from 0.</param>
/// <param name="IsMidi">Whether it's a Standard MIDI File, for the MT-32; otherwise the AdLib's song.</param>
/// <param name="Shared">A word the two of a pair share, not yet understood.</param>
/// <param name="Data">The song's file, as whatever wrote it left it.</param>
public sealed record Song(int Number, bool IsMidi, int Shared, ReadOnlyMemory<byte> Data)
{
    /// <summary>
    /// Whether the AdLib's song is a MIDI file too, of the kind Ad Lib called .MDI, as it is
    /// from <i>Timequest</i>; otherwise it is a Visual Composer .ROL file.
    /// </summary>
    public bool IsAdLibMidi => !IsMidi && Data.Span.StartsWith("MThd"u8);
}

/// <summary>
/// A game's music file, S101_M1.MUS: songs, each kept twice, as a Standard MIDI File for a Roland
/// MT-32 and as a song for an AdLib card. It starts with 30 entries of ten bytes: where a song
/// starts, its length, 1 for MIDI or 0 for the AdLib's, and a word. <i>Timequest</i> has 120
/// entries, and from <i>Spellcasting 201</i> they are twelve bytes. The first song starts where
/// the table ends, after one byte, which says which table it is.
/// </summary>
public sealed class MusicFile
{
    private static readonly (int Count, int Size)[] Tables = [(30, 10), (120, 10), (120, 12)];

    /// <summary>Reads a music file from its bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't a music file.</exception>
    public MusicFile(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < 4)
        {
            throw new InvalidDataException("Not a music file: it's shorter than its table of songs.");
        }

        int first = BinaryPrimitives.ReadInt32LittleEndian(file);
        var (count, size) = Tables.FirstOrDefault(table => first == (table.Count * table.Size) + 1, Tables[0]);
        if (file.Length < count * size)
        {
            throw new InvalidDataException("Not a music file: it's shorter than its table of songs.");
        }

        var songs = new List<Song>();
        for (int number = 0; number < count; number++)
        {
            var entry = file.AsSpan(number * size, 10);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]);
            if (offset == 0)
            {
                continue;
            }

            if (offset < 0 || offset + length > file.Length)
            {
                throw new InvalidDataException($"Song {number} goes past the end of the file.");
            }

            songs.Add(new Song(
                number,
                BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]) == 1,
                BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]),
                file.AsMemory(offset, length)));
        }

        Songs = songs;
    }

    /// <summary>The songs, in the file's order.</summary>
    public IReadOnlyList<Song> Songs { get; }
}

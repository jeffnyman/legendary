using System.Buffers.Binary;

namespace Legendary.Core.Sound;

/// <summary>A song of a music file, in one of its two formats.</summary>
/// <param name="Number">Its entry in the file, from 0.</param>
/// <param name="IsMidi">Whether it's a Standard MIDI File, for the MT-32; otherwise an AdLib .ROL file.</param>
/// <param name="Shared">A word the two of a pair share, not yet understood.</param>
/// <param name="Data">The song's file, as whatever wrote it left it.</param>
public sealed record Song(int Number, bool IsMidi, int Shared, ReadOnlyMemory<byte> Data);

/// <summary>
/// A game's music file, S101_M1.MUS: songs, each kept twice, as a Standard MIDI File for a Roland
/// MT-32 and as an AdLib Visual Composer .ROL file for an AdLib card. It starts with 30 entries of
/// ten bytes: where a song starts, its length, 1 for MIDI or 0 for .ROL, and a word.
/// </summary>
public sealed class MusicFile
{
    private const int EntryCount = 30;

    /// <summary>Reads a music file from its bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't a music file.</exception>
    public MusicFile(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < EntryCount * 10)
        {
            throw new InvalidDataException("Not a music file: it's shorter than its table of songs.");
        }

        var songs = new List<Song>();
        for (int number = 0; number < EntryCount; number++)
        {
            var entry = file.AsSpan(number * 10, 10);
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

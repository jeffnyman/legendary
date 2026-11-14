using System.Buffers.Binary;

namespace Legendary.Core.Pictures;

/// <summary>A rectangle of a picture and the object that's there.</summary>
/// <param name="Object">The object's number.</param>
/// <param name="Left">Its left edge, in pairs of pixels.</param>
/// <param name="Top">Its top edge, in the EGA's rows.</param>
/// <param name="Right">Its right edge, in pairs of pixels.</param>
/// <param name="Bottom">Its bottom edge, in the EGA's rows.</param>
public readonly record struct Region(int Object, int Left, int Top, int Right, int Bottom)
{
    /// <summary>Whether a pixel of the EGA's picture is in the rectangle.</summary>
    public bool Contains(int x, int y) => x / 2 >= Left && x / 2 <= Right && y >= Top && y <= Bottom;
}

/// <summary>A picture's regions.</summary>
/// <param name="Picture">The picture's number in the picture files.</param>
/// <param name="Object">The object the rest of the picture is, or 0.</param>
/// <param name="Regions">The regions, in the file's order.</param>
public sealed record PictureRegions(int Picture, int Object, IReadOnlyList<Region> Regions);

/// <summary>
/// A game's region file, S101_R1.RGN, which goes with the picture files of its number and says
/// what object is where in each picture: what the mouse points at. It starts with a directory of
/// six bytes for each picture, to where the first picture's regions start, and a region is six
/// bytes: an object's number and a rectangle.
/// </summary>
public sealed class RegionFile
{
    /// <summary>Reads a region file from its bytes.</summary>
    /// <exception cref="InvalidDataException">A picture's regions go past the end of the file.</exception>
    public RegionFile(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at));

        // the directory ends where the first regions start
        var pictures = new List<PictureRegions>();
        int end = file.Length;
        for (int picture = 0; picture * 6 + 6 <= end; picture++)
        {
            int offset = Word(picture * 6), shown = Word(picture * 6 + 2), count = Word(picture * 6 + 4);
            if (offset == 0)
            {
                continue;
            }

            if (offset + count * 6 > file.Length)
            {
                throw new InvalidDataException($"Picture {picture}'s regions go past the end of the file.");
            }

            end = Math.Min(end, offset);
            var regions = new Region[count];
            for (int i = 0; i < count; i++)
            {
                var region = file.AsSpan(offset + i * 6, 6);
                regions[i] = new Region(BinaryPrimitives.ReadUInt16LittleEndian(region), region[2], region[3], region[4], region[5]);
            }

            pictures.Add(new PictureRegions(picture, shown, regions));
        }

        Pictures = pictures;
    }

    /// <summary>The pictures that have regions, in the directory's order.</summary>
    public IReadOnlyList<PictureRegions> Pictures { get; }
}

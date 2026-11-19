using System.Buffers.Binary;

namespace Legendary.Core.Pictures;

/// <summary>A rectangle of a picture and the object that's there.</summary>
/// <param name="Object">The object's number.</param>
/// <param name="Left">Its left edge, in pairs of pixels.</param>
/// <param name="Top">Its top edge, in the EGA's rows.</param>
/// <param name="Right">Its right edge, in pairs of pixels.</param>
/// <param name="Bottom">Its bottom edge, in the EGA's rows.</param>
/// <param name="Points">
/// In the later games' form, its corners, in pixels: two for a rectangle, more for an outline.
/// Null for a region of the first form, which is its rectangle alone.
/// </param>
public readonly record struct Region(int Object, int Left, int Top, int Right, int Bottom, IReadOnlyList<(int X, int Y)>? Points = null)
{
    /// <summary>Whether the region is an outline, and not a rectangle.</summary>
    public bool IsOutline => Points is { Count: > 2 };

    /// <summary>
    /// Whether a pixel of the picture is in the region: in its rectangle, and for an outline
    /// inside the outline too, by the count of its sides a line from the pixel crosses.
    /// </summary>
    public bool Contains(int x, int y)
    {
        if (Points is null)
        {
            return x / 2 >= Left && x / 2 <= Right && y >= Top && y <= Bottom;
        }

        if (x < Points.Min(point => point.X) || x > Points.Max(point => point.X) || y < Top || y > Bottom)
        {
            return false;
        }

        if (!IsOutline)
        {
            return true;
        }

        bool inside = false;
        for (int i = 0, last = Points.Count - 1; i < Points.Count; last = i++)
        {
            var (ax, ay) = Points[i];
            var (bx, by) = Points[last];
            if ((ay > y) != (by > y) && x < ax + ((double)(bx - ax) * (y - ay) / (by - ay)))
            {
                inside = !inside;
            }
        }

        return inside;
    }
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
/// <remarks>
/// <i>Gateway II</i> and <i>Companions of Xanth</i> keep a region another way: an object's
/// number, a count of points, and the points, each two words, across and down in pixels. Two
/// points are a rectangle's corners, and more are the corners of an outline, so that a region
/// can be the shape of the thing. A file is in that form when every picture's regions, read
/// that way, end where another picture's start, or the file ends.
/// </remarks>
public sealed class RegionFile
{
    /// <summary>Reads a region file from its bytes.</summary>
    /// <exception cref="InvalidDataException">A picture's regions go past the end of the file.</exception>
    public RegionFile(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at));

        if (Outlined(file) is { } outlined)
        {
            Pictures = outlined;
            return;
        }

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

    // the file read in the later games' form, or null when it isn't in it
    private static List<PictureRegions>? Outlined(byte[] file)
    {
        ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at));

        var entries = new List<(int Offset, int Shown, int Count)>();
        int end = file.Length;
        for (int at = 0; at + 6 <= end; at += 6)
        {
            int offset = Word(at);
            if (offset != 0)
            {
                end = Math.Min(end, offset);
            }

            entries.Add((offset, Word(at + 2), Word(at + 4)));
        }

        var starts = entries.Where(entry => entry.Offset != 0).Select(entry => entry.Offset).Append(file.Length).ToHashSet();
        var pictures = new List<PictureRegions>();
        for (int picture = 0; picture < entries.Count; picture++)
        {
            var (offset, shown, count) = entries[picture];
            if (offset == 0)
            {
                continue;
            }

            int at = offset;
            var regions = new Region[count];
            for (int i = 0; i < count; i++)
            {
                int corners = at + 4 <= file.Length ? Word(at + 2) : 0;
                if (corners is < 2 or > 512 || at + 4 + (4 * corners) > file.Length)
                {
                    return null;
                }

                var points = new (int X, int Y)[corners];
                for (int point = 0; point < points.Length; point++)
                {
                    points[point] = (Word(at + 4 + (4 * point)), Word(at + 6 + (4 * point)));
                }

                regions[i] = new Region(
                    Word(at), points.Min(point => point.X) / 2, points.Min(point => point.Y),
                    points.Max(point => point.X) / 2, points.Max(point => point.Y), points);
                at += 4 + (4 * points.Length);
            }

            if (count != 0 && !starts.Contains(at))
            {
                return null;
            }

            pictures.Add(new PictureRegions(picture, shown, regions));
        }

        return pictures;
    }

    /// <summary>The pictures that have regions, in the directory's order.</summary>
    public IReadOnlyList<PictureRegions> Pictures { get; }
}

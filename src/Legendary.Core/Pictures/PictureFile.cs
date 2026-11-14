using System.Buffers.Binary;

namespace Legendary.Core.Pictures;

/// <summary>A picture's entry in its file's directory.</summary>
/// <param name="Number">Its place in the directory, from 0.</param>
/// <param name="Offset">Where it starts in the file.</param>
/// <param name="PlaceCount">How many places to draw it at come before it, four bytes each.</param>
/// <param name="Kind">How it's kept: bit 0 set for no palette, and the rest the way it's packed.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record PictureEntry(int Number, int Offset, int PlaceCount, byte Kind, int Width, int Height)
{
    /// <summary>The way its pixels are packed.</summary>
    public PicturePacking Packing => (PicturePacking)(Kind >> 1);

    /// <summary>Whether it has a palette of its own, in an EGA file.</summary>
    public bool HasPalette => (Kind & 1) == 0;
}

/// <summary>The ways a picture's pixels are kept.</summary>
public enum PicturePacking
{
    /// <summary>By LZW, a pixel at a time: the EGA's.</summary>
    Lzw = 0,

    /// <summary>By Huffman's codes, a byte of eight pixels at a time: the CGA's.</summary>
    Huffman = 1,

    /// <summary>As they are, a bit a pixel.</summary>
    Plane = 4,

    /// <summary>As they are in four planes, each row as four rows of bits.</summary>
    Planes = 5,
}

/// <summary>A picture, unpacked.</summary>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Pixels">A byte a pixel, row by row: a color's number from 0 to 15.</param>
/// <param name="Palette">Sixteen of the EGA's 64 colors by number, or null for a picture of two colors.</param>
/// <param name="Places">Where it's drawn, across and down; none for a picture drawn where it's told.</param>
public sealed record Picture(int Width, int Height, byte[] Pixels, byte[]? Palette, IReadOnlyList<(int X, int Y)> Places);

/// <summary>
/// A game's picture file, for the EGA (S101_E1.PIC) or the CGA (S101_C1.PIC). It starts with a
/// directory of twelve bytes for each picture, to where the first picture starts. The two
/// displays' files differ only in that the CGA's have no palettes.
/// </summary>
public sealed class PictureFile
{
    /// <summary>The EGA's 16 colors as it starts, by their numbers among its 64.</summary>
    public static ReadOnlySpan<byte> DefaultPalette => [0, 1, 2, 3, 4, 5, 20, 7, 56, 57, 58, 59, 60, 61, 62, 63];

    private readonly byte[] _file;

    /// <summary>Reads a picture file from its bytes.</summary>
    /// <param name="file">The file.</param>
    /// <param name="ega">Whether it's one of the EGA's files, which have palettes.</param>
    /// <exception cref="InvalidDataException">The bytes aren't a picture file.</exception>
    public PictureFile(byte[] file, bool ega)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
        Ega = ega;
        int first = file.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(file) : 0;
        if (first < 12 || first % 12 != 0 || first > file.Length)
        {
            throw new InvalidDataException("Not a picture file: its directory doesn't end where its first picture starts.");
        }

        var entries = new PictureEntry[first / 12];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = file.AsSpan(12 * i, 12);
            entries[i] = new PictureEntry(
                i,
                BinaryPrimitives.ReadInt32LittleEndian(entry),
                entry[4],
                entry[5],
                BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]));
        }

        Entries = entries;
    }

    /// <summary>Whether this is one of the EGA's files.</summary>
    public bool Ega { get; }

    /// <summary>The directory, with its empty entries: those have an offset of 0.</summary>
    public IReadOnlyList<PictureEntry> Entries { get; }

    /// <summary>One of the EGA's 64 colors as red, green and blue: two bits of each.</summary>
    public static (byte Red, byte Green, byte Blue) EgaColor(int number) => (
        (byte)(((number >> 2) & 1) * 170 + ((number >> 5) & 1) * 85),
        (byte)(((number >> 1) & 1) * 170 + ((number >> 4) & 1) * 85),
        (byte)((number & 1) * 170 + ((number >> 3) & 1) * 85));

    /// <summary>Unpacks a picture.</summary>
    /// <exception cref="InvalidDataException">The picture is packed a way that isn't known, or its data runs out.</exception>
    public Picture Read(PictureEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            int at = entry.Offset;
            var places = new (int, int)[entry.PlaceCount];
            for (int i = 0; i < places.Length; i++, at += 4)
            {
                places[i] = (BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(at)), BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(at + 2)));
            }

            // the CGA's pictures are all of two colors, and its files have no palettes
            byte[]? palette = null;
            if (Ega && entry.HasPalette)
            {
                palette = _file.AsSpan(at, 16).ToArray();
                at += 16;
            }
            else if (Ega && entry.Packing == PicturePacking.Planes)
            {
                palette = DefaultPalette.ToArray();
            }

            int rowBytes = (entry.Width + 7) / 8;
            byte[] pixels = entry.Packing switch
            {
                PicturePacking.Lzw => UnpackLzw(at, entry.Width * entry.Height),
                PicturePacking.Huffman => Spread(UnpackHuffman(at, rowBytes * entry.Height), rowBytes, entry, 1),
                PicturePacking.Plane => Spread(_file.AsSpan(at, rowBytes * entry.Height), rowBytes, entry, 1),
                PicturePacking.Planes => Spread(_file.AsSpan(at, rowBytes * 4 * entry.Height), rowBytes, entry, 4),
                _ => throw new InvalidDataException($"Picture {entry.Number} is packed a way that isn't known: {entry.Kind:X2}."),
            };
            return new Picture(entry.Width, entry.Height, pixels, palette, places);
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException($"Picture {entry.Number}'s data runs out.", error);
        }
    }

    // rows of bits, a plane after another in each row, as a byte a pixel
    private static byte[] Spread(ReadOnlySpan<byte> rows, int rowBytes, PictureEntry entry, int planes)
    {
        byte[] pixels = new byte[entry.Width * entry.Height];
        for (int row = 0; row < entry.Height; row++)
        {
            for (int plane = 0; plane < planes; plane++)
            {
                var bits = rows.Slice((row * planes + plane) * rowBytes, rowBytes);
                for (int column = 0; column < entry.Width; column++)
                {
                    if ((bits[column / 8] >> (7 - column % 8) & 1) != 0)
                    {
                        pixels[row * entry.Width + column] |= (byte)(1 << plane);
                    }
                }
            }
        }

        return pixels;
    }

    // The CGA files' Huffman codes: a count less one, then for each code its length in bits, its
    // byte, and its bits in a byte, or in two when it's longer than eight. Then the picture's
    // bytes, a code each, read from each byte's high bit down.
    private byte[] UnpackHuffman(int at, int count)
    {
        var codes = new Dictionary<(int Length, int Bits), byte>();
        int entries = _file[at++] + 1;
        for (int i = 0; i < entries; i++)
        {
            int length = _file[at];
            byte value = _file[at + 1];
            int bits = length > 8 ? (_file[at + 2] << 8 | _file[at + 3]) >> (16 - length) : _file[at + 2] >> (8 - length);
            codes[(length, bits)] = value;
            at += length > 8 ? 4 : 3;
        }

        var stream = new BitReader(_file, at);
        byte[] found = new byte[count];
        for (int i = 0; i < count; i++)
        {
            // a picture of one byte over and over has one code, of no bits at all
            int length = 0, bits = 0;
            byte value;
            while (!codes.TryGetValue((length, bits), out value))
            {
                bits = bits << 1 | stream.Take(1);
                if (++length > 16)
                {
                    throw new InvalidDataException("A picture's Huffman codes don't match its bits.");
                }
            }

            found[i] = value;
        }

        return found;
    }

    // The EGA files' LZW, close to GIF's but not it. Codes start five bits wide, read from each
    // byte's high bit down. Codes 0 to 15 are pixels; 16 says the codes are a bit wider from now
    // on, and 17 starts the table over; the code with every bit set ends the picture.
    private byte[] UnpackLzw(int at, int count)
    {
        const int FirstString = 18, Widen = 16, StartOver = 17;
        var stream = new BitReader(_file, at);
        byte[] pixels = new byte[count];
        int written = 0;
        // a code's string is another code's and one pixel more
        var before = new int[1 << 12];
        var last = new byte[1 << 12];
        var stack = new byte[1 << 12];
        int width = 5, following = FirstString;

        byte Put(int code)
        {
            int depth = 0;
            for (; code >= FirstString; code = before[code])
            {
                stack[depth++] = last[code];
            }

            byte first = (byte)code;
            if (written < count)
            {
                pixels[written++] = first;
            }

            while (depth > 0 && written < count)
            {
                pixels[written++] = stack[--depth];
            }

            return first;
        }

        int previous = -1;
        byte firstPixel = 0;
        while (written < count)
        {
            int code = stream.Take(width);
            if (code == Widen)
            {
                width++;
                continue;
            }

            if (code == StartOver)
            {
                (width, following, previous) = (5, FirstString, -1);
                continue;
            }

            if (code == (1 << width) - 1)
            {
                break;
            }

            if (previous < 0)
            {
                // the first code after the table starts over is written, and nothing is added
                firstPixel = Put(code);
                previous = code;
                continue;
            }

            if (code >= following)
            {
                // a code not yet in the table is the last string and its own first pixel again
                firstPixel = Put(previous);
                if (written < count)
                {
                    pixels[written++] = firstPixel;
                }
            }
            else
            {
                firstPixel = Put(code);
            }

            (before[following], last[following]) = (previous, firstPixel);
            following++;
            previous = code;
        }

        return pixels;
    }

    // a file's bytes read a bit at a time, from each byte's high bit down
    private struct BitReader(byte[] file, int at)
    {
        private int _at = at;
        private int _held;
        private int _count;

        public int Take(int width)
        {
            while (_count < width)
            {
                _held = (_held << 8 | file[_at++]) & 0xFFFFFF;
                _count += 8;
            }

            _count -= width;
            return _held >> _count & ((1 << width) - 1);
        }
    }
}

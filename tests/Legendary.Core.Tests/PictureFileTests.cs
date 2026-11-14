using System.Security.Cryptography;
using Legendary.Core.Pictures;

namespace Legendary.Core.Tests;

/// <summary>
/// The pictures in a .PIC file, from files made here and from the player's copy of
/// <i>Spellcasting 101</i>. Without the game (in LEGENDARY_GAMES, or legend/sc101/game above the
/// tests), its tests pass without checking.
/// </summary>
public sealed class PictureFileTests
{
    private static PictureFile? Load(string name) =>
        TestData.Game("sc101/game") is { } folder && TestData.File(folder, name) is { } path
            ? new PictureFile(File.ReadAllBytes(path), ega: name.Contains("_e", StringComparison.OrdinalIgnoreCase))
            : null;

    private static string Hash(byte[] pixels) => Convert.ToHexStringLower(SHA256.HashData(pixels))[..16];

    /// <summary>
    /// A file of one picture, 3 by 2, packed by LZW with a palette and a place. Its codes, five
    /// bits each: 1, 2, 18 (which is 1 2), 19 (which isn't in the table yet: 2 1 2), and the end.
    /// </summary>
    [Fact]
    public void UnpacksLzwMadeHere()
    {
        byte[] file =
        [
            12, 0, 0, 0, 1, 0, 3, 0, 2, 0, 0, 0,
            7, 0, 9, 0,
            .. PictureFile.DefaultPalette,
            0b00001_000, 0b10_10010_1, 0b0011_1111, 0b1_0000000,
        ];

        var pictures = new PictureFile(file, ega: true);
        var entry = Assert.Single(pictures.Entries);
        Assert.Equal((PicturePacking.Lzw, true, 1), (entry.Packing, entry.HasPalette, entry.PlaceCount));

        var picture = pictures.Read(entry);
        Assert.Equal([1, 2, 1, 2, 2, 1], picture.Pixels);
        Assert.Equal(PictureFile.DefaultPalette.ToArray(), picture.Palette);
        Assert.Equal([(7, 9)], picture.Places);
    }

    /// <summary>A picture of 10 by 2 in four planes, as it is: only the last pixel of each row is set, to 5 and to 10.</summary>
    [Fact]
    public void UnpacksPlanesMadeHere()
    {
        byte[] file =
        [
            12, 0, 0, 0, 0, 0x0B, 10, 0, 2, 0, 0, 0,
            0, 0x40, 0, 0, 0, 0x40, 0, 0,
            0, 0, 0, 0x40, 0, 0, 0, 0x40,
        ];

        var picture = new PictureFile(file, ega: true).Read(new PictureFile(file, ega: true).Entries[0]);
        Assert.Equal(5, picture.Pixels[9]);
        Assert.Equal(10, picture.Pixels[19]);
        Assert.Equal(15, picture.Pixels.Sum(pixel => pixel));
    }

    [Fact]
    public void RefusesWhatIsNotAPictureFile()
    {
        Assert.Throws<InvalidDataException>(() => new PictureFile([5, 0, 0, 0, 1, 2], ega: true));
    }

    [Fact]
    public void EgaColorsAreTwoBitsEach()
    {
        Assert.Equal((0, 0, 0), PictureFile.EgaColor(0));
        Assert.Equal((255, 255, 255), PictureFile.EgaColor(63));
        Assert.Equal((170, 85, 0), PictureFile.EgaColor(20));
    }

    /// <summary>
    /// Each way of packing, against what the tools in Python unpack: the title for the EGA, by
    /// LZW, and for the CGA, by Huffman; the compass, in planes; and a picture of two colors.
    /// </summary>
    [Theory]
    [InlineData("s101_e1.pic", 38, 640, 350, "cff93ad4a3a25832")]
    [InlineData("s101_c1.pic", 38, 640, 200, "b4616da046831efe")]
    [InlineData("s101_e1.pic", 0, 130, 77, "0e0d357ee20ab32a")]
    [InlineData("s101_e1.pic", 33, 84, 13, "d0df16915968f62a")]
    [InlineData("s101_e3.pic", 101, 19, 16, "ba615c4a80256b7e")]
    public void Spellcasting101sPictures(string name, int number, int width, int height, string hash)
    {
        if (Load(name) is not { } pictures)
        {
            return;
        }

        var picture = pictures.Read(pictures.Entries[number]);
        Assert.Equal((width, height), (picture.Width, picture.Height));
        Assert.Equal(hash, Hash(picture.Pixels));
    }

    [Fact]
    public void TheTitleHasItsPaletteAndASpriteItsPlaces()
    {
        if (Load("s101_e1.pic") is not { } first || Load("s101_e3.pic") is not { } third)
        {
            return;
        }

        Assert.Equal("3f20212437272b1404072e380e1c3000", Convert.ToHexStringLower(first.Read(first.Entries[38]).Palette!));
        Assert.Equal([(115, 113), (145, 113), (176, 113), (206, 113), (236, 113)], third.Read(third.Entries[101]).Places);
    }

    /// <summary>Every picture in every file unpacks: 724 of them.</summary>
    [Fact]
    public void EveryPictureUnpacks()
    {
        if (TestData.Game("sc101/game") is not { } folder)
        {
            return;
        }

        int count = 0;
        foreach (string path in Directory.EnumerateFiles(folder).Where(path => path.EndsWith(".pic", StringComparison.OrdinalIgnoreCase)))
        {
            var pictures = new PictureFile(File.ReadAllBytes(path), ega: Path.GetFileName(path).Contains("_e", StringComparison.OrdinalIgnoreCase));
            foreach (var entry in pictures.Entries.Where(entry => entry.Offset != 0))
            {
                Assert.Equal(entry.Width * entry.Height, pictures.Read(entry).Pixels.Length);
                count++;
            }
        }

        Assert.Equal(724, count);
    }
}

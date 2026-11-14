using Legendary.Core.Pictures;

namespace Legendary.Core.Tests;

/// <summary>
/// The regions in a .RGN file, from a file made here and from the player's copy of
/// <i>Spellcasting 101</i>. Without the game (in LEGENDARY_GAMES, or legend/sc101/game above the
/// tests), its test passes without checking.
/// </summary>
public sealed class RegionFileTests
{
    /// <summary>A directory of two pictures, the second with one region, of object 300.</summary>
    [Fact]
    public void ReadsAFileMadeHere()
    {
        byte[] file =
        [
            0, 0, 0, 0, 0, 0,
            12, 0, 7, 0, 1, 0,
            0x2C, 0x01, 10, 20, 30, 40,
        ];

        var picture = Assert.Single(new RegionFile(file).Pictures);
        Assert.Equal((1, 7), (picture.Picture, picture.Object));
        var region = Assert.Single(picture.Regions);
        Assert.Equal(new Region(300, 10, 20, 30, 40), region);

        // across is in pairs of pixels, and the edges are in the rectangle
        Assert.True(region.Contains(20, 20) && region.Contains(61, 40));
        Assert.False(region.Contains(19, 20) || region.Contains(62, 40) || region.Contains(20, 41));
    }

    [Fact]
    public void RefusesRegionsPastTheEnd()
    {
        Assert.Throws<InvalidDataException>(() => new RegionFile([6, 0, 0, 0, 9, 0]));
    }

    [Fact]
    public void Spellcasting101sRegions()
    {
        if (TestData.Game("sc101/game") is not { } folder || TestData.File(folder, "s101_r1.rgn") is not { } path)
        {
            return;
        }

        var regions = new RegionFile(File.ReadAllBytes(path));
        Assert.Equal([44, 45, 51, 52, 64, 65, 66], regions.Pictures.Select(picture => picture.Picture));

        // the map of the game's start: its five rooms, by their numbers
        Assert.Equal([1, 2, 3, 4, 5], regions.Pictures[0].Regions.Select(region => region.Object));

        // the Bedchamber: the floor is object 233, across the bottom
        var bedchamber = regions.Pictures[1];
        Assert.Equal((1, 16), (bedchamber.Object, bedchamber.Regions.Count));
        Assert.Equal(new Region(233, 17, 137, 189, 167), bedchamber.Regions[0]);

        int count = Directory.EnumerateFiles(folder)
            .Where(file => file.EndsWith(".rgn", StringComparison.OrdinalIgnoreCase))
            .Sum(file => new RegionFile(File.ReadAllBytes(file)).Pictures.Sum(picture => picture.Regions.Count));
        Assert.Equal(906, count);
    }
}

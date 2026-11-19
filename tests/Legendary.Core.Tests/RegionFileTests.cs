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

    /// <summary>A picture with a rectangle and a triangle, in the later games' form.</summary>
    [Fact]
    public void ReadsOutlinesFromAFileMadeHere()
    {
        byte[] file =
        [
            6, 0, 9, 0, 2, 0,
            0x2C, 0x01, 2, 0, 10, 0, 20, 0, 30, 0, 40, 0,
            0x2D, 0x01, 3, 0, 100, 0, 50, 0, 140, 0, 50, 0, 100, 0, 90, 0,
        ];

        var picture = Assert.Single(new RegionFile(file).Pictures);
        Assert.Equal((0, 9, 2), (picture.Picture, picture.Object, picture.Regions.Count));

        // two points are a rectangle's corners, in pixels
        var box = picture.Regions[0];
        Assert.Equal((300, false), (box.Object, box.IsOutline));
        Assert.Equal([(10, 20), (30, 40)], box.Points);
        Assert.True(box.Contains(10, 20) && box.Contains(30, 40));
        Assert.False(box.Contains(9, 20) || box.Contains(31, 40));

        // more are an outline: a place in its rectangle can be outside it
        var triangle = picture.Regions[1];
        Assert.Equal((301, true), (triangle.Object, triangle.IsOutline));
        Assert.True(triangle.Contains(105, 55));
        Assert.False(triangle.Contains(135, 85));
    }

    [Fact]
    public void GatewayIIsAndXanthsRegions()
    {
        foreach (var (game, pictures, regions, outlines) in new[] { ("gateway2/game", 99, 797, 678), ("xanth/game", 74, 728, 591) })
        {
            if (TestData.Game(game) is not { } folder)
            {
                continue;
            }

            var files = Directory.EnumerateFiles(folder)
                .Where(file => file.EndsWith(".rgn", StringComparison.OrdinalIgnoreCase))
                .Select(file => new RegionFile(File.ReadAllBytes(file))).ToList();
            var all = files.SelectMany(file => file.Pictures).ToList();
            Assert.Equal(pictures, all.Count);
            Assert.Equal(regions, all.Sum(picture => picture.Regions.Count));
            Assert.Equal(outlines, all.Sum(picture => picture.Regions.Count(region => region.IsOutline)));
            Assert.All(all.SelectMany(picture => picture.Regions), region => Assert.NotNull(region.Points));
        }
    }

    [Fact]
    public void XanthsBedroom()
    {
        if (TestData.Game("xanth/game") is not { } folder || TestData.File(folder, "xanth_02.rgn") is not { } path)
        {
            return;
        }

        // the bedroom: the desk is a rectangle across the bottom, and the Post-It an outline
        // on the computer, where the game's own tour clicks it
        var bedroom = new RegionFile(File.ReadAllBytes(path)).Pictures[0];
        Assert.Equal((0, 34), (bedroom.Picture, bedroom.Object));
        Assert.Equal((35, false), (bedroom.Regions[0].Object, bedroom.Regions[0].IsOutline));
        Assert.Equal([(0, 85), (263, 121)], bedroom.Regions[0].Points);

        // the picture is at 51, 4 on the screen, and the tour clicks the Post-It at 235, 71
        var postIt = bedroom.Regions.Last(region => region.Contains(235 - 51, 71 - 4));
        Assert.Equal((44, true), (postIt.Object, postIt.IsOutline));
    }
}

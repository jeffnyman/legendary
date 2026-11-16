using System.Text;
using Legendary.Core.Programs;

namespace Legendary.Core.Tests;

/// <summary>
/// PLINK86's overlays, from the player's copies of <i>Spellcasting 101</i> (1990) and
/// <i>Timequest</i> (1991). Without a game (in LEGENDARY_GAMES, or legend/sc101/game above the
/// tests), its tests pass without checking.
/// </summary>
public sealed class Plink86Tests
{
    private static Plink86Program? Load(string game, string exe) =>
        TestData.Game(game) is { } folder && TestData.File(folder, exe) is { } path ? Plink86Program.Load(path) : null;

    [Fact]
    public void Spellcasting101sTable()
    {
        if (Load("sc101/game", "s101main.exe") is not { } program)
        {
            return;
        }

        Assert.Equal(5724, program.Mz.Relocations.Count);
        Assert.Equal((0x2E95, 0x0322), (program.Mz.Cs, program.Mz.Ip));
        Assert.Equal(0x2E95, program.LoaderSegment);
        Assert.Equal(0x2F1C, program.TableSegment);
        Assert.Equal(0x54D6, program.MemoryEndSegment);
        Assert.Equal("S101MAIN.EXE", program.ExeName);
        Assert.Equal("S101MAIN.OVL", program.OvlName);
        Assert.Equal(14, program.Overlays.Count);
        Assert.Equal(700, program.Thunks.Count);

        // two overlays share the first place and eleven the second; the data has the third
        Assert.Equal([2, 11, 1], program.Overlays.GroupBy(overlay => overlay.LoadSegment).Select(group => group.Count()));
        var data = program.Overlay(14);
        Assert.True(data.Preloaded && data.InExe);
        Assert.Equal(program.MemoryEndSegment, data.EndSegment);
        Assert.All(program.Overlays.Take(13), overlay => Assert.False(overlay.Preloaded || overlay.InExe));

        Assert.Equal(new ProgramThunk(0x2F1C, 0x0192, 3, 0x3670, 0x054E), program.ThunkAt(0x2F1C, 0x0192));
    }

    /// <summary>
    /// The overlays fill their files with nothing between them or left over: each starts where
    /// the one before ends, the last in the .OVL ends at its end, and the data at the .EXE's.
    /// </summary>
    [Theory]
    [InlineData("sc101/game", "s101main.exe")]
    [InlineData("tquest/game", "timemain.exe")]
    public void TheOverlaysFillTheirFiles(string game, string exe)
    {
        if (Load(game, exe) is not { } program)
        {
            return;
        }

        string folder = TestData.Game(game)!;
        int at = 0;
        foreach (var overlay in program.Overlays.Where(overlay => !overlay.InExe))
        {
            Assert.Equal(at, overlay.FileParagraph * 16);
            at = overlay.ContentsOffset + overlay.SizeParagraphs * 16;
        }

        Assert.Equal(new FileInfo(TestData.File(folder, program.OvlName)!).Length, at);

        var data = Assert.Single(program.Overlays, overlay => overlay.InExe);
        Assert.Equal(program.Mz.ImageEnd + 16, data.FileParagraph * 16);
        Assert.Equal(program.Mz.File.Length, data.ContentsOffset + data.SizeParagraphs * 16);

        // every thunk goes into the overlay it names
        Assert.All(program.Thunks, thunk =>
        {
            var overlay = program.Overlay(thunk.Overlay);
            Assert.InRange(thunk.TargetSegment, overlay.LoadSegment, overlay.EndSegment - 1);
        });
    }

    [Fact]
    public void Spellcasting101sMemory()
    {
        if (Load("sc101/game", "s101main.exe") is not { } program)
        {
            return;
        }

        var image = program.Image(3);
        Assert.Equal([14, 3], image.Loaded.Select(overlay => overlay.Number));

        // the root: a message a room's exit gives
        Assert.StartsWith("If you want to enter the fireplace", Text(image.At(0x2701, 0)));

        // the data: Microsoft C's notice starts its data segment, which its startup code names
        Assert.StartsWith("MS Run-Time Library - Copyright (c) 1988, Microsoft Corp", Text(image.At(0x4517, 8)));
        Assert.Equal(0x4517, image.Word(0x1F1D, 0x002B));
        Assert.True(image.IsRelocation(0x1F1D * 16 + 0x002B));

        // overlay 3: the routine a thunk goes to starts as a C function does
        Assert.True(image.At(0x3670, 0x054E).StartsWith<byte>([0x55, 0x8B, 0xEC]));
        Assert.Equal(3, image.OverlayAt(0x3670)?.Number);
        Assert.Null(image.OverlayAt(0x313A));

        // 3 and 4 load at the same place
        Assert.Throws<ArgumentException>(() => program.Image(3, 4));
    }

    private static string Text(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes[..bytes.IndexOf((byte)0)]);
}

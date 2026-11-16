using System.Text;
using Legendary.Core.Programs;

namespace Legendary.Core.Tests;

/// <summary>
/// A program linked by .RTLink/Plus, from the player's copy of <i>Spellcasting 201</i> (1991).
/// Without the game (in LEGENDARY_GAMES, or legend/sc201/game above the tests), its tests pass
/// without checking.
/// </summary>
public sealed class RtlinkTests
{
    private static RtlinkProgram? Load() =>
        TestData.Game("sc201/game") is { } folder && TestData.File(folder, "s201main.exe") is { } path ? RtlinkProgram.Load(path) : null;

    [Fact]
    public void A_number_is_one_to_four_bytes()
    {
        // a segment, a length of 23C2 in two bytes, no offset, and that the bytes follow,
        // which they don't
        byte[] file = [0x4C, 0xA3, 0xC2, 0x00, 0x01];
        Assert.Throws<InvalidDataException>(() => RtlinkProgram.ReadRecords(file, 0, 1, out _));

        // a stretch that is only cleared has no bytes, and three bytes can follow the first
        byte[] cleared = [0x0E, 0xE0, 0x01, 0x02, 0x03, 0xC0, 0x00, 0xAC, 0x00, 0x00];
        var record = Assert.Single(RtlinkProgram.ReadRecords(cleared, 0, 1, out int end));
        Assert.Equal((14, 0x010203, 0xAC, 10), (record.Segment, record.Length, record.Offset, end));
        Assert.Null(record.Data);
        Assert.Empty(record.Fixups);

        // a plain list: flags 1000, a count, and that many places
        byte[] plain = [0x01, 0x02, 0x00, 0x01, 0xAA, 0xBB, 0x01, 0x00, 0x10, 0x02, 0x09, 0x81, 0x2C];
        var fixups = Assert.Single(Assert.Single(RtlinkProgram.ReadRecords(plain, 0, 1, out _)).Fixups);
        Assert.False(fixups.Named);
        Assert.Equal([9, 0x12C], fixups.Places);
    }

    [Fact]
    public void A_program_that_isnt_one_is_refused()
    {
        Assert.False(RtlinkProgram.IsRtlink([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => new RtlinkProgram([1, 2, 3]));
    }

    [Fact]
    public void Spellcasting201sTable()
    {
        if (Load() is not { } program)
        {
            return;
        }

        // the MZ header has no relocations, and starts the program in the stub
        Assert.Empty(program.Mz.Relocations);
        Assert.Equal((0x0037, 0x0071), (program.Mz.Cs, program.Mz.Ip));
        Assert.Equal((0x019A, 0x2EF0, 0x0491), (program.LinkerVersion, program.StartSegment, program.StartOffset));
        Assert.Equal((175, 40, 58), (program.SegmentCount, program.Records.Count, program.LibraryRecords.Count));
        Assert.Equal(("S201.RTL", "S201MAIN.OVL"), (program.LibraryName, program.OvlName));
        Assert.Equal(0x5813, program.MemoryEndSegment);
        Assert.Equal((0x2EF0, 0x2EF0), (program.LoaderSegment, program.TableSegment));
        Assert.Equal(".RTLink/Plus", program.Linker);

        // every fixup in the program itself is a plain relocation; the library's name things
        Assert.Equal(5608, program.Records.Sum(record => record.Fixups.Sum(fixups => fixups.Places.Count)));
        Assert.All(program.Records.SelectMany(record => record.Fixups), fixups => Assert.False(fixups.Named));
        Assert.Equal(3804, program.LibraryRecords.Sum(record => record.Fixups.Sum(fixups => fixups.Places.Count)));
        Assert.All(program.LibraryRecords.SelectMany(record => record.Fixups), fixups => Assert.True(fixups.Named));

        // 22 overlays in three places, and 790 thunks to the manager's last byte
        Assert.Equal(22, program.Overlays.Count);
        Assert.Equal([4, 2, 16], program.Overlays.GroupBy(overlay => overlay.LoadSegment).Select(group => group.Count()));
        Assert.All(program.Overlays, overlay => Assert.False(overlay.Preloaded || overlay.InExe));
        Assert.Equal(790, program.Thunks.Count);
        Assert.Equal(new ProgramThunk(0x2EF0, 0x0B80, 3, 0x3252, 0x0401), program.ThunkAt(0x2EF0, 0x0B80));
        Assert.Equal(new ProgramThunk(0x2EF0, 0x2A52, 4, 0x31E1, 0x0000), program.Thunks[^1]);
    }

    /// <summary>
    /// The overlays fill the .OVL, each starting where the one before ends, but for the few
    /// bytes the file stops short of the last one's size; and every thunk goes into its overlay.
    /// </summary>
    [Fact]
    public void TheOverlaysFillTheirFile()
    {
        if (Load() is not { } program)
        {
            return;
        }

        int at = 0;
        foreach (var overlay in program.Overlays)
        {
            Assert.Equal(at, overlay.FileParagraph * 16);
            at = overlay.ContentsOffset + overlay.SizeParagraphs * 16;
        }

        long size = new FileInfo(TestData.File(TestData.Game("sc201/game")!, program.OvlName)!).Length;
        Assert.InRange(at - size, 0, 15);
        Assert.Equal(program.Overlays[^1].SizeParagraphs * 16 - (at - size), program.Contents(program.Overlays[^1]).Length);

        Assert.All(program.Thunks, thunk =>
        {
            var overlay = program.Overlay(thunk.Overlay);
            Assert.InRange(thunk.TargetSegment, overlay.LoadSegment, overlay.EndSegment - 1);
        });
    }

    [Fact]
    public void Spellcasting201sMemory()
    {
        if (Load() is not { } program)
        {
            return;
        }

        var image = program.Image(3);
        Assert.Equal([3], image.Loaded.Select(overlay => overlay.Number));
        Assert.Equal(program.MemoryEndSegment * 16, image.Memory.Length);

        // the program's first record is code, put where its table has segment 76: at the start
        Assert.Equal(0, program.SegmentStart(76));
        Assert.True(image.At(0, 0).StartsWith<byte>([0x55, 0x8B, 0xEC]));

        // a plain relocation, in the first routine's far call to the run-time
        Assert.Equal(0x1408, image.Word(0x023C, 0x000B));
        Assert.True(image.IsRelocation(0x023C * 16 + 0x000B));

        // the library puts Microsoft C's notice at the data's start, before the program's own data
        Assert.StartsWith("MS Run-Time Library - Copyright (c) 1988, Microsoft Corp", Text(image.At(0x4974, 8)));

        // and its fixups link it to the program: a far pointer in the run-time to a routine
        // of the program's, which the file has as nothing at all
        Assert.Equal((0x1DF2, 0x0D68), (image.Word(0x1413, 0x000C), image.Word(0x1413, 0x000E)));
        Assert.True(image.IsRelocation(0x1413 * 16 + 0x000E));

        // overlay 3: the routine the first thunk goes to starts as a C function does
        Assert.True(image.At(0x3252, 0x0401).StartsWith<byte>([0x55, 0x8B, 0xEC]));
        Assert.Equal(3, image.OverlayAt(0x3252)?.Number);
        Assert.Null(image.OverlayAt(0x3958));

        // 3 and 4 load at the same place
        Assert.Throws<ArgumentException>(() => program.Image(3, 4));
    }

    [Fact]
    public void A_program_is_read_as_its_linker_left_it()
    {
        if (TestData.Game("sc201/game") is { } folder && TestData.File(folder, "s201main.exe") is { } rtlink)
        {
            Assert.IsType<RtlinkProgram>(OverlaidProgram.Load(rtlink));
        }

        if (TestData.Game("sc101/game") is { } first && TestData.File(first, "s101main.exe") is { } plink86)
        {
            var program = Assert.IsType<Plink86Program>(OverlaidProgram.Load(plink86));
            Assert.Equal("PLINK86", program.Linker);
        }
    }

    private static string Text(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes[..bytes.IndexOf((byte)0)]);
}

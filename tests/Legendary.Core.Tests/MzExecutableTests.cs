using Legendary.Core.Programs;

namespace Legendary.Core.Tests;

/// <summary>The MZ header, from a program made here: these need no game.</summary>
public sealed class MzExecutableTests
{
    /// <summary>A header of 32 bytes with one relocation, an image of 48, and 5 bytes after it.</summary>
    private static byte[] Program()
    {
        byte[] file = new byte[32 + 48 + 5];
        ushort[] header = [0x5A4D, 80, 1, 1, 2, 0x10, 0xFFFF, 3, 0x100, 0, 4, 1, 0x1C, 0];
        for (int i = 0; i < header.Length; i++)
        {
            BitConverter.TryWriteBytes(file.AsSpan(i * 2), header[i]);
        }

        // the relocation: offset 6 in segment 2
        file[0x1C] = 6;
        file[0x1E] = 2;
        file[32] = 0xAA;
        return file;
    }

    [Fact]
    public void ReadsTheHeader()
    {
        var mz = new MzExecutable(Program());

        Assert.Equal(32, mz.HeaderSize);
        Assert.Equal(48, mz.Image.Length);
        Assert.Equal(0xAA, mz.Image.Span[0]);
        Assert.Equal(3, mz.ImageParagraphs);
        Assert.Equal(5, mz.TrailingSize);
        Assert.Equal((1, 4), (mz.Cs, mz.Ip));
        Assert.Equal((3, 0x100), (mz.Ss, mz.Sp));
        Assert.Equal(0x10, mz.MinAlloc);
        Assert.Equal(new Relocation(2, 6), Assert.Single(mz.Relocations));
        Assert.Equal(38, mz.Relocations[0].Linear);
    }

    [Fact]
    public void RefusesWhatIsNotAProgram()
    {
        Assert.Throws<InvalidDataException>(() => new MzExecutable(new byte[64]));
        Assert.Throws<InvalidDataException>(() => new Plink86Program(Program()));
    }
}

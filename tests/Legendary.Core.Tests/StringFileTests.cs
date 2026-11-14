using Legendary.Core.Text;

namespace Legendary.Core.Tests;

/// <summary>
/// The text packed into a string file, from one made here and from the player's copies of
/// <i>Spellcasting 101</i> and <i>Timequest</i>. Without a game (in LEGENDARY_GAMES, or
/// legend/sc101/game above the tests), its tests pass without checking.
/// </summary>
public sealed class StringFileTests
{
    private static StringFile? Load(string game, string name) =>
        TestData.Game(game) is { } folder && TestData.File(folder, name) is { } path
            ? new StringFile(File.ReadAllBytes(path))
            : null;

    /// <summary>
    /// A file of one block with two strings, "ab" and "the b". The tree gives the end one bit,
    /// 0; 'a' is 10, 'b' 110, and the word "the " 111.
    /// </summary>
    [Fact]
    public void UnpacksAFileMadeHere()
    {
        byte[] file =
        [
            1, 0, // one block
            9, 0, 2, 0, 2, 0, // 9 bytes unpacked, 2 packed, 2 strings
            3, 0, // three nodes
            0, 0, 1, 0, // node 0: the end, or node 1
            0x9F, 0xFF, 2, 0, // node 1: 'a', or node 2
            0x9E, 0xFF, 0x80, 0xFF, // node 2: 'b', or word 128
            1, 0, 0, 0, 5, 0, // one word, from 0 to 5
            (byte)'t', (byte)'h', (byte)'e', (byte)' ', 0,
            0b0_011_01, // a, b, the end, from the low bit up; the rest of the byte is skipped
            0b0_011_111, // "the ", b, the end
        ];

        var strings = new StringFile(file);

        var block = Assert.Single(strings.Blocks);
        Assert.Equal(new StringBlock(0, 9, 2, 2, file.Length - 2), block);
        Assert.Equal(["the "], strings.Words);
        Assert.Equal(["ab", "the b"], strings.Strings(block));
    }

    [Fact]
    public void RefusesWhatIsNotAStringFile()
    {
        Assert.Throws<InvalidDataException>(() => new StringFile([200, 0, 1, 2, 3]));
    }

    /// <summary>
    /// Every block unpacks to the size and the count of strings the table has for it, which is
    /// how the packing was checked.
    /// </summary>
    [Theory]
    [InlineData("sc101/game", "s101str.dat", 69, 1204)]
    [InlineData("tquest/game", "timestr.dat", 120, 1475)]
    public void EveryBlockUnpacksToItsSize(string game, string name, int blocks, int count)
    {
        if (Load(game, name) is not { } strings)
        {
            return;
        }

        Assert.Equal(blocks, strings.Blocks.Count);
        Assert.Equal(128, strings.Words.Count);
        Assert.Equal(count, strings.Blocks.Sum(block => block.StringCount));
        Assert.All(strings.Blocks, block =>
        {
            var unpacked = strings.Strings(block);
            Assert.Equal(block.StringCount, unpacked.Count);
            Assert.Equal(block.UnpackedSize, unpacked.Sum(text => text.Length + 1));
        });
    }

    [Fact]
    public void Spellcasting101sText()
    {
        if (Load("sc101/game", "s101str.dat") is not { } strings)
        {
            return;
        }

        Assert.Equal(218, strings.TreeSize);
        Assert.Equal("A ", strings.Words[0]);
        Assert.Equal("Balmoral ", strings.Words[1]);

        var first = strings.Strings(strings.Blocks[0]);
        Assert.StartsWith("This game requires 131,000 bytes of free memory to be successfully completed.", first[0]);
        Assert.Equal(" Press <Esc> to abort, any other key to continue.", first[2]);
    }
}

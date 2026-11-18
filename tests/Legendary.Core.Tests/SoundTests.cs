using System.Security.Cryptography;
using System.Text;
using Legendary.Core.Sound;

namespace Legendary.Core.Tests;

/// <summary>
/// The music and the sounds, from files made here and from the player's copy of
/// <i>Spellcasting 101</i>. Without the game (in LEGENDARY_GAMES, or legend/sc101/game above the
/// tests), its tests pass without checking.
/// </summary>
public sealed class SoundTests
{
    private static byte[]? Read(string name) =>
        TestData.Game("sc101/game") is { } folder && TestData.File(folder, name) is { } path ? File.ReadAllBytes(path) : null;

    /// <summary>A sound of four bytes, each played twice, with a zero that ends it before its length does.</summary>
    [Fact]
    public void ReadsASoundMadeHere()
    {
        byte[] file = new byte[32 + 6];
        "STEVE"u8.CopyTo(file);
        file[5] = 2;
        file[6] = 72;
        file[7] = 6;
        byte[] sound = [36, 72, 1, 80, 0, 9];
        sound.CopyTo(file, 32);

        var real = new RealSound(file);

        Assert.Equal(2, real.Repeat);
        Assert.Equal([36, 72, 1, 80], real.Widths.ToArray());
        Assert.Equal(1193182 / 72.0 / 2, real.Rate);

        // half a pulse is half the level, and a width past the pulse's is the whole of it
        Assert.Equal([127, 255, 3, 255], real.ToPcm());
    }

    [Fact]
    public void RefusesWhatIsNotASound()
    {
        Assert.Throws<InvalidDataException>(() => new RealSound(new byte[64]));
        Assert.Throws<InvalidDataException>(() => new MusicFile(new byte[10]));
    }

    /// <summary>A music file of one song, four bytes long, at its 301st byte.</summary>
    [Fact]
    public void ReadsMusicMadeHere()
    {
        byte[] file = new byte[301 + 4];
        byte[] entry = [0x2D, 0x01, 0, 0, 4, 0, 1, 0, 7, 0];
        entry.CopyTo(file, 0);
        "MThd"u8.CopyTo(file.AsSpan(301));

        var song = Assert.Single(new MusicFile(file).Songs);
        Assert.Equal((0, true, 7), (song.Number, song.IsMidi, song.Shared));
        Assert.Equal("MThd", Encoding.ASCII.GetString(song.Data.Span));
    }

    /// <summary>
    /// The later games' table, 120 entries of twelve bytes: the first song starts where it
    /// ends, which says which table it is. The AdLib's song is a MIDI file there too.
    /// </summary>
    [Fact]
    public void ReadsTheLaterTableOfSongs()
    {
        byte[] file = new byte[1441 + 5];
        byte[] entry = [0xA1, 0x05, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0];
        entry.CopyTo(file, 12 * 119);
        file[0] = 0xA1;
        file[1] = 0x05;
        file[4] = 4;
        "MThd"u8.CopyTo(file.AsSpan(1441));

        var songs = new MusicFile(file).Songs;
        Assert.Equal([0, 119], songs.Select(song => song.Number));
        Assert.All(songs, song => Assert.True(!song.IsMidi && song.IsAdLibMidi));
    }

    [Fact]
    public void Spellcasting101sSoundOfAChaptersStart()
    {
        if (Read("chapter1.rs") is not { } file)
        {
            return;
        }

        var sound = new RealSound(file);
        Assert.Equal((2, 27498), (sound.Repeat, sound.Widths.Length));
        Assert.Equal(8286, (int)Math.Round(sound.Rate));
        Assert.Equal("52386b32c2105ed9", Convert.ToHexStringLower(SHA256.HashData(sound.Widths.Span))[..16]);
    }

    /// <summary>Each song is there twice: a MIDI file, and then an AdLib .ROL file.</summary>
    [Fact]
    public void Spellcasting101sMusic()
    {
        if (Read("s101_m1.mus") is not { } file)
        {
            return;
        }

        var music = new MusicFile(file);
        Assert.Equal(8, music.Songs.Count);
        Assert.All(music.Songs.Chunk(2), pair =>
        {
            Assert.True(pair[0].IsMidi && !pair[1].IsMidi);
            Assert.Equal(pair[0].Shared, pair[1].Shared);
            Assert.StartsWith("MThd", Encoding.ASCII.GetString(pair[0].Data.Span[..4]));
            Assert.Equal("\\roll\\default", Encoding.ASCII.GetString(pair[1].Data.Span.Slice(4, 13)));
        });
        Assert.Equal((8483, 5761), (music.Songs[0].Data.Length, music.Songs[1].Data.Length));
    }
}

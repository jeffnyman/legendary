using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Text;

/// <summary>A word of a game's vocabulary.</summary>
/// <param name="Number">Its number, from 1, as the program numbers them.</param>
/// <param name="Text">The word.</param>
/// <param name="Parts">
/// Its parts of speech, a bit each: 1 a verb, 2 a noun, 4 an adjective, 8 a preposition, 16 an
/// article, 32 a direction. A word with none is one of the parser's own.
/// </param>
/// <param name="Synonym">The number of the word it stands for, or 0.</param>
/// <param name="SynonymParts">The parts of speech it stands for that word as.</param>
public sealed record VocabularyWord(int Number, string Text, int Parts, int Synonym, int SynonymParts)
{
    /// <summary>Whether the word can be a verb.</summary>
    public bool IsVerb => (Parts & 1) != 0;
}

/// <summary>
/// A game's vocabulary, <c>VOCAB.DAT</c>, in whichever of its two forms. <i>Spellcasting
/// 101</i>'s is plain: the size of the words' text, the text, each word ending in a zero, then
/// a count and eight bytes for each word: where its text starts, its parts of speech, the
/// number of a word it is a synonym of, and the parts of speech it is that word's synonym as.
/// <i>Spellcasting 201</i>'s is packed: a tree of Huffman's codes, the text packed by it, a
/// count and four bytes for each word, where its text starts and its parts of speech, and
/// the synonyms apart, in groups.
/// </summary>
public static class Vocabulary
{
    /// <summary>The words of a vocabulary file, in the order of their numbers.</summary>
    /// <exception cref="InvalidDataException">The file isn't a vocabulary.</exception>
    public static IReadOnlyList<VocabularyWord> Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 4)
        {
            throw new InvalidDataException("A vocabulary has at least the size of its text and a count of its words.");
        }

        int size = BinaryPrimitives.ReadUInt16LittleEndian(file);
        if (2 + size + 2 > file.Length || 2 + size + 2 + (8 * BinaryPrimitives.ReadUInt16LittleEndian(file[(2 + size)..])) != file.Length)
        {
            // not the plain form: the packed one, or nothing
            return ReadPacked(file);
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(file[(2 + size)..]);
        int table = 2 + size + 2;

        var words = new List<VocabularyWord>(count);
        for (int number = 1; number <= count; number++)
        {
            var entry = file[(table + (8 * (number - 1)))..];
            int start = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            if (start >= size)
            {
                throw new InvalidDataException($"Word {number} of the vocabulary starts past the end of the text.");
            }

            var text = file[(2 + start)..(2 + size)];
            int end = text.IndexOf((byte)0);
            words.Add(new VocabularyWord(
                number,
                Encoding.ASCII.GetString(end < 0 ? text : text[..end]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[6..])));
        }

        return words;
    }

    // The packed form. The tree is a count of words and then the words, in pairs, one for a
    // 0 bit and one for a 1: a word from zero up is where in the tree the next pair is, and
    // one below zero is a character, as its complement, and ends the code. The last pair is
    // the root. The text is its size unpacked and then the codes, read from each byte's low
    // bit up. The table is in alphabetical order, and a word's number is its place in it. A
    // group of synonyms is a word's number, a count, and that many pairs: a word that stands
    // for it, and the parts of speech it stands for it as.
    //
    // The word before the codes is the text's size unpacked in Spellcasting 201, and in
    // Spellcasting 301 how many bytes the codes are: the file is read the first way, and the
    // second if that doesn't leave the rest ending where the file does.
    private static List<VocabularyWord> ReadPacked(ReadOnlySpan<byte> file)
    {
        try
        {
            return ReadPacked(file, sizeIsOfCodes: false);
        }
        catch (InvalidDataException)
        {
            return ReadPacked(file, sizeIsOfCodes: true);
        }
    }

    private static List<VocabularyWord> ReadPacked(ReadOnlySpan<byte> file, bool sizeIsOfCodes)
    {
        try
        {
            int links = BinaryPrimitives.ReadUInt16LittleEndian(file);
            var tree = file.Slice(2, 2 * links);
            int at = 2 + (2 * links);
            int size = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
            int bit = (at + 2) * 8, end = sizeIsOfCodes ? (at + 2 + size) * 8 : int.MaxValue;
            var unpacked = new List<byte>(size);
            for (int node = links - 2; bit < end && (sizeIsOfCodes || unpacked.Count < size); bit++)
            {
                int link = BinaryPrimitives.ReadInt16LittleEndian(tree[(2 * (node + (file[bit >> 3] >> (bit & 7) & 1)))..]);
                if (link >= 0)
                {
                    node = link;
                }
                else
                {
                    unpacked.Add((byte)~link);
                    node = links - 2;
                }
            }

            byte[] text = [.. unpacked];
            at = sizeIsOfCodes ? at + 2 + size : (bit + 7) >> 3;
            int count = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
            int table = at + 2;
            at = table + (4 * count);
            var synonyms = new Dictionary<int, (int Word, int Parts)>();
            int groups = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]);
            for (at += 2; groups > 0; groups--)
            {
                int head = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]), members = BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 2)..]);
                for (at += 4; members > 0; members--, at += 4)
                {
                    synonyms[BinaryPrimitives.ReadUInt16LittleEndian(file[at..])] = (head, BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 2)..]));
                }
            }

            if (at != file.Length)
            {
                throw new InvalidDataException("The vocabulary's words don't end where its file does.");
            }

            var words = new List<VocabularyWord>(count);
            for (int number = 1; number <= count; number++)
            {
                int start = BinaryPrimitives.ReadUInt16LittleEndian(file[(table + (4 * (number - 1)))..]);
                int zero = Array.IndexOf(text, (byte)0, start);
                var (synonym, parts) = synonyms.GetValueOrDefault(number);
                words.Add(new VocabularyWord(
                    number,
                    Encoding.ASCII.GetString(text, start, (zero < 0 ? text.Length : zero) - start),
                    BinaryPrimitives.ReadUInt16LittleEndian(file[(table + (4 * (number - 1)) + 2)..]),
                    synonym,
                    parts));
            }

            return words;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException("The vocabulary's words don't end where its file does.", ex);
        }
    }
}

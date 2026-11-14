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
/// A game's vocabulary, <c>VOCAB.DAT</c>: the size of the words' text, the text, each word
/// ending in a zero, then a count and eight bytes for each word: where its text starts, its
/// parts of speech, the number of a word it is a synonym of, and the parts of speech it is that
/// word's synonym as.
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
        if (2 + size + 2 > file.Length)
        {
            throw new InvalidDataException("The vocabulary's text is longer than its file.");
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(file[(2 + size)..]);
        int table = 2 + size + 2;
        if (table + (8 * count) != file.Length)
        {
            throw new InvalidDataException("The vocabulary's words don't end where its file does.");
        }

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
}

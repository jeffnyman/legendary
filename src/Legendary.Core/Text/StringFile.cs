using System.Buffers.Binary;
using System.Text;

namespace Legendary.Core.Text;

/// <summary>An entry in the table that starts a string file.</summary>
/// <param name="Number">Its number, from 0.</param>
/// <param name="UnpackedSize">The size of its strings, each with the zero that ends it.</param>
/// <param name="PackedSize">Its size in the file.</param>
/// <param name="StringCount">How many strings are in it.</param>
/// <param name="Offset">Where it starts in the file.</param>
public sealed record StringBlock(int Number, int UnpackedSize, int PackedSize, int StringCount, int Offset);

/// <summary>
/// A game's string file, S101STR.DAT or TIMESTR.DAT: the text that isn't in its program, in
/// blocks of strings. The file is a table of the blocks, a Huffman tree, a list of words, and
/// the blocks. A string is the tree's codes, read from each byte's low bit up, and ends at the
/// code for zero; the next starts on the next byte. A code is a character, or, from 128 up, a
/// word from the list: the commonest in the text, most with the space after them.
/// </summary>
public sealed class StringFile
{
    private readonly byte[] _file;

    // a pair for each node: where a 0 bit goes and where a 1 bit goes. Above zero is a node;
    // at or below it, a code, negated
    private readonly (short Zero, short One)[] _tree;

    /// <summary>Reads a string file from its bytes.</summary>
    /// <exception cref="InvalidDataException">The bytes aren't a string file of this kind.</exception>
    public StringFile(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
        try
        {
            int blockCount = Word(0);
            int at = 2 + 6 * blockCount;

            _tree = new (short, short)[Word(at)];
            for (int node = 0; node < _tree.Length; node++)
            {
                _tree[node] = ((short)Word(at + 2 + 4 * node), (short)Word(at + 4 + 4 * node));
            }

            at += 2 + 4 * _tree.Length;

            // the words: a count, where each starts and where the last ends, then each and a zero
            int wordCount = Word(at);
            int starts = at + 2;
            at += 2 + 2 * (wordCount + 1);
            var words = new string[wordCount];
            for (int word = 0; word < wordCount; word++)
            {
                int start = Word(starts + 2 * word), end = Word(starts + 2 * word + 2);
                words[word] = Encoding.ASCII.GetString(file, at + start, end - start - 1);
            }

            Words = words;
            at += Word(starts + 2 * wordCount);

            var blocks = new StringBlock[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                int entry = 2 + 6 * block;
                blocks[block] = new StringBlock(block, Word(entry), Word(entry + 2), Word(entry + 4), at);
                at += blocks[block].PackedSize;
            }

            Blocks = blocks;
            if (at != file.Length)
            {
                throw new InvalidDataException($"The string blocks end at {at:X}, and the file at {file.Length:X}.");
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("Not a string file: its tables go past its end.", error);
        }
    }

    /// <summary>The blocks, in the file's order.</summary>
    public IReadOnlyList<StringBlock> Blocks { get; }

    /// <summary>The words the text is packed with: code 128 is the first.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>How many nodes the Huffman tree has.</summary>
    public int TreeSize => _tree.Length;

    /// <summary>A block's strings, unpacked.</summary>
    /// <exception cref="InvalidDataException">The block's codes go past its end.</exception>
    public IReadOnlyList<string> Strings(StringBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var packed = _file.AsSpan(block.Offset, block.PackedSize);
        var strings = new string[block.StringCount];
        var text = new StringBuilder();
        int bit = 0;
        for (int i = 0; i < strings.Length; i++)
        {
            text.Clear();
            for (int node = 0; ;)
            {
                if (bit >> 3 >= packed.Length)
                {
                    throw new InvalidDataException($"Block {block.Number}'s strings go past its end.");
                }

                bool one = (packed[bit >> 3] >> (bit & 7) & 1) != 0;
                bit++;
                node = one ? _tree[node].One : _tree[node].Zero;
                if (node > 0)
                {
                    continue;
                }

                if (node == 0)
                {
                    break;
                }

                if (-node >= 128)
                {
                    text.Append(Words[-node - 128]);
                }
                else
                {
                    text.Append((char)-node);
                }

                node = 0;
            }

            strings[i] = text.ToString();
            bit = (bit + 7) & ~7;
        }

        return strings;
    }

    private int Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(at, 2));
}

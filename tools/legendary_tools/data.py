"""The game's data files, as far as they are understood."""

from __future__ import annotations

import struct
from dataclasses import dataclass


@dataclass(frozen=True)
class StringBlock:
    """An entry in the table that starts ...STR.DAT."""

    number: int
    unpacked_size: int  # of its strings, each with the zero that ends it
    packed_size: int
    strings: int
    offset: int  # where it starts in the file


class StringFile:
    """
    ...STR.DAT: the game's text that isn't in its program, in blocks of strings.

    The file is a table of the blocks, a Huffman tree, a list of words, and the blocks. A
    string is the tree's codes, read from each byte's low bit up, and ends at the code for
    zero; the next starts on the next byte. A code is a character, or, from 128 up, a word
    from the list: the commonest in the text, most with the space after them.
    """

    def __init__(self, data: bytes):
        self.data = data
        (count,) = struct.unpack_from("<H", data, 0)
        table = [struct.unpack_from("<HHH", data, 2 + 6 * number) for number in range(count)]
        at = 2 + 6 * count

        # the tree: a count of nodes, then a pair of words for each, one for a 0 bit and one
        # for a 1. A word above zero is the node to go on to; one at or below it is a
        # character, negated, and ends the code
        (nodes,) = struct.unpack_from("<H", data, at)
        self.tree = [struct.unpack_from("<hh", data, at + 2 + 4 * node) for node in range(nodes)]
        at += 2 + 4 * nodes

        # the words: a count, where each starts and where the last ends, then each and a zero
        (words,) = struct.unpack_from("<H", data, at)
        starts = struct.unpack_from(f"<{words + 1}H", data, at + 2)
        at += 2 + 2 * (words + 1)
        self.words = [
            data[at + start : at + end - 1].decode("ascii") for start, end in zip(starts, starts[1:])
        ]
        at += starts[-1]

        self.blocks: list[StringBlock] = []
        for number, (unpacked, packed, strings) in enumerate(table):
            self.blocks.append(StringBlock(number, unpacked, packed, strings, at))
            at += packed
        if at != len(data):
            raise ValueError(f"the string blocks end at {at:#x}, and the file at {len(data):#x}")

    def block(self, number: int) -> StringBlock:
        if not 0 <= number < len(self.blocks):
            raise ValueError(f"there is no block {number}: they go from 0 to {len(self.blocks) - 1}")
        return self.blocks[number]

    def strings(self, block: StringBlock) -> list[str]:
        """A block's strings, unpacked."""
        packed = self.data[block.offset : block.offset + block.packed_size]
        strings = []
        bit = 0
        for _ in range(block.strings):
            text = []
            node = 0
            while True:
                node = self.tree[node][packed[bit >> 3] >> (bit & 7) & 1]
                bit += 1
                if node > 0:
                    continue
                if node == 0:
                    break
                text.append(self.words[-node - 128] if -node >= 128 else chr(-node))
                node = 0
            strings.append("".join(text))
            bit = (bit + 7) & ~7
        return strings


# a word's parts of speech, a bit each
PARTS = {1: "verb", 2: "noun", 4: "adjective", 8: "preposition", 16: "article", 32: "direction"}


def parts_of_speech(bits: int) -> str:
    """The parts of speech in a word's bits, by name; a word with none is one of the parser's own."""
    return ", ".join(name for bit, name in PARTS.items() if bits & bit) or "special"


@dataclass(frozen=True)
class Word:
    """A word of the vocabulary."""

    number: int  # from 1, as the program numbers them
    text: str
    parts: int  # its parts of speech
    synonym: int  # the word it stands for, or 0
    synonym_parts: int  # the parts of speech it stands for that word as


def vocabulary(data: bytes) -> list[Word]:
    """
    VOCAB.DAT: the size of the words' text, the text, each word ending in a zero, then a count
    and eight bytes for each word: where its text starts, its parts of speech, the number of a
    word it is a synonym of, and the parts of speech it is that word's synonym as.
    """
    (size,) = struct.unpack_from("<H", data, 0)
    (count,) = struct.unpack_from("<H", data, 2 + size)
    if 2 + size + 2 + 8 * count != len(data):
        raise ValueError("the vocabulary's words don't end where its file does")
    words = []
    for number in range(1, count + 1):
        start, parts, synonym, synonym_parts = struct.unpack_from(
            "<HHHH", data, 2 + size + 2 + 8 * (number - 1)
        )
        text = data[2 + start : data.index(b"\0", 2 + start)].decode("ascii")
        words.append(Word(number, text, parts, synonym, synonym_parts))
    return words

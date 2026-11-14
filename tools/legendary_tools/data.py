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


def vocabulary(data: bytes) -> tuple[list[str], int]:
    """
    The words that start VOCAB.DAT, in the file's order, and where they end. A byte comes
    first, then each word and a zero; what follows the words isn't read yet.
    """
    words = []
    at = 1
    while at < len(data):
        end = data.find(b"\0", at)
        word = data[at:end]
        if end < 0 or not word or not all(0x20 <= byte < 0x7F for byte in word):
            break
        words.append(word.decode("ascii"))
        at = end + 1
    return words, at

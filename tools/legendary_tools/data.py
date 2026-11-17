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
    zero. A code is a character, or, from 128 up, a word from the list: the commonest in the
    text, most with the space after them.

    There are two forms of it. In Spellcasting 101's the tree is a count of nodes and a pair
    of words for each, a word above zero the node to go on to and one at or below it a
    character, negated; the first node is the root; and each string starts on a new byte. In
    Spellcasting 201's the tree is as its vocabulary's is: a count of words and the words, in
    pairs, a word from zero up where in the tree the next pair is and one below it a
    character's complement; the last pair is the root; and a string starts at the bit after
    the one before it ends. The tree is kept here in the second form, whichever the file has.
    """

    def __init__(self, data: bytes):
        self.data = data
        (count,) = struct.unpack_from("<H", data, 0)
        self._table = [struct.unpack_from("<HHH", data, 2 + 6 * number) for number in range(count)]
        for packed in (False, True):
            try:
                if self._read(2 + 6 * count, packed):
                    return
            except (struct.error, UnicodeDecodeError):
                continue
        raise ValueError("the string blocks don't end where the file does")

    def _read(self, at: int, packed: bool) -> bool:
        """The tree, the words and the blocks, read as one form or the other; whether they fit the file."""
        data = self.data
        (count,) = struct.unpack_from("<H", data, at)
        if packed:
            self.tree = list(struct.unpack_from(f"<{count}h", data, at + 2))
            self.root = count - 2
        else:
            # a node's number is made its place, and a character negated is made its complement
            links = struct.unpack_from(f"<{2 * count}h", data, at + 2)
            self.tree = [2 * link if link > 0 else link - 1 for link in links]
            self.root = 0
        at += 2 + 2 * len(self.tree)
        self.byte_aligned = not packed

        # the words: a count, where each starts and where the last ends, then each and a zero
        (words,) = struct.unpack_from("<H", data, at)
        starts = struct.unpack_from(f"<{words + 1}H", data, at + 2)
        at += 2 + 2 * (words + 1)
        self.words = [
            data[at + start : at + end - 1].decode("ascii") for start, end in zip(starts, starts[1:])
        ]
        at += starts[-1]

        self.blocks: list[StringBlock] = []
        for number, (unpacked, size, strings) in enumerate(self._table):
            self.blocks.append(StringBlock(number, unpacked, size, strings, at))
            at += size
        return at == len(data)

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
            node = self.root
            while True:
                node = self.tree[node + (packed[bit >> 3] >> (bit & 7) & 1)]
                bit += 1
                if node >= 0:
                    continue
                code = ~node
                if code == 0:
                    break
                text.append(self.words[code - 128] if code >= 128 else chr(code))
                node = self.root
            strings.append("".join(text))
            if self.byte_aligned:
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
    """VOCAB.DAT, in whichever of its two forms: plain, as Spellcasting 101 has it, or packed."""
    (size,) = struct.unpack_from("<H", data, 0)
    if 2 + size + 2 <= len(data):
        (count,) = struct.unpack_from("<H", data, 2 + size)
        if 2 + size + 2 + 8 * count == len(data):
            return _plain_vocabulary(data, size, count)
    try:
        return _packed_vocabulary(data)
    except (struct.error, IndexError, ValueError):
        raise ValueError("the vocabulary's words don't end where its file does") from None


def _plain_vocabulary(data: bytes, size: int, count: int) -> list[Word]:
    """
    The size of the words' text, the text, each word ending in a zero, then a count and eight
    bytes for each word: where its text starts, its parts of speech, the number of a word it
    is a synonym of, and the parts of speech it is that word's synonym as.
    """
    words = []
    for number in range(1, count + 1):
        start, parts, synonym, synonym_parts = struct.unpack_from(
            "<HHHH", data, 2 + size + 2 + 8 * (number - 1)
        )
        text = data[2 + start : data.index(b"\0", 2 + start)].decode("ascii")
        words.append(Word(number, text, parts, synonym, synonym_parts))
    return words


def _vocabulary_tables(data: bytes, at: int) -> tuple[list[tuple[int, int]], dict[int, tuple[int, int]]]:
    """A packed vocabulary's table of words and its synonyms, which end the file."""
    (count,) = struct.unpack_from("<H", data, at)
    table = [struct.unpack_from("<HH", data, at + 2 + 4 * index) for index in range(count)]
    at += 2 + 4 * count
    synonyms: dict[int, tuple[int, int]] = {}
    (groups,) = struct.unpack_from("<H", data, at)
    at += 2
    for _ in range(groups):
        head, members = struct.unpack_from("<HH", data, at)
        for member in range(members):
            word, parts = struct.unpack_from("<HH", data, at + 4 + 4 * member)
            synonyms[word] = (head, parts)
        at += 4 + 4 * members
    if at != len(data):
        raise ValueError("the vocabulary's words don't end where its file does")
    return table, synonyms


def _packed_vocabulary(data: bytes) -> list[Word]:
    """
    Spellcasting 201's: the words' text packed by Huffman's codes, and the synonyms apart.

    The file is a tree, the text, a table of the words, and the synonyms. The tree is a count
    of words and then the words, in pairs, one for a 0 bit and one for a 1: a word from zero
    up is where in the tree the next pair is, and one below zero is a character, as its
    complement, and ends the code. The last pair is the root. The text is its size unpacked
    and then the codes, read from each byte's low bit up; each word ends in a zero, and they
    are in no order. In Spellcasting 301 the size is of the codes, in bytes, and not of the
    text. The table is a count and four bytes for each word, in alphabetical
    order: where its text starts, and its parts of speech. The synonyms are a count of groups,
    and each group a word's number, a count, and that many pairs: a word that stands for it,
    and the parts of speech it stands for it as.
    """
    (links,) = struct.unpack_from("<H", data, 0)
    tree = struct.unpack_from(f"<{links}h", data, 2)
    at = 2 + 2 * links
    (size,) = struct.unpack_from("<H", data, at)
    bit = (at + 2) * 8
    text = bytearray()
    node = links - 2
    # the size is the text's, unpacked, in Spellcasting 201, and in Spellcasting 301 it is how
    # many bytes the text is packed into: whichever has the rest end where the file does
    packed_end = (at + 2 + size) * 8
    table = synonyms = None
    while bit < min(packed_end, len(data) * 8):
        link = tree[node + (data[bit >> 3] >> (bit & 7) & 1)]
        bit += 1
        if link >= 0:
            node = link
            continue
        text.append(~link)
        node = links - 2
        if len(text) == size:
            try:
                table, synonyms = _vocabulary_tables(data, (bit + 7) >> 3)
                break
            except (struct.error, IndexError, ValueError):
                pass
    if table is None or synonyms is None:
        table, synonyms = _vocabulary_tables(data, at + 2 + size)
    return [
        Word(number, text[start : text.index(0, start)].decode("ascii"), parts, *synonyms.get(number, (0, 0)))
        for number, (start, parts) in enumerate(table, 1)
    ]

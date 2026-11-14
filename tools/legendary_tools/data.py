"""The game's data files, as far as they are understood."""

from __future__ import annotations

import struct
from dataclasses import dataclass


@dataclass(frozen=True)
class StringBlock:
    """An entry in the table that starts ...STR.DAT. The text itself is packed."""

    number: int
    unpacked_size: int
    packed_size: int
    strings: int


def string_blocks(data: bytes) -> list[StringBlock]:
    """
    The table that starts ...STR.DAT: a count, then three words for each block. What the
    words mean is inferred from their sums: the middle ones add up to nearly the file's size.
    """
    (count,) = struct.unpack_from("<H", data, 0)
    return [StringBlock(number, *struct.unpack_from("<HHH", data, 2 + 6 * number)) for number in range(count)]


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

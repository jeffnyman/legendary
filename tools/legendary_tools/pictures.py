"""The game's pictures: its .PIC files, for the EGA (..._E1.PIC) and the CGA (..._C1.PIC).

A file starts with a directory of twelve bytes for each picture, to where the first picture
starts:

    0  where the picture starts in the file, or 0 for none
    4  how many places the picture starts with: two words each, across and down, to draw it at
    5  how it is kept: bit 0 set for no palette, and the rest the way it's packed
    6  its width and then its height, in pixels
    10 a word not yet understood: zero for most, and a round number for a few, perhaps a time

In an EGA file, a picture with a palette starts with it: 16 bytes, the EGA's 64 colors by
number. Then the pixels, in one of four ways:

- as they are, a bit a pixel, the rows one after another;
- as they are in four planes, each row as four rows of bits, blue first;
- packed by Huffman's codes, a byte of eight pixels at a time, which the CGA's are;
- packed by LZW, a pixel at a time, which the EGA's are.

The LZW is close to GIF's but not it. Codes start five bits wide, read from each byte's high
bit down. Codes 0 to 15 are pixels; 16 says the codes are a bit wider from now on, and 17
starts the table over; the code with every bit set ends the picture.

The games after *Spellcasting 101* pack their pictures the same four ways, and say which
another way. The byte at 5 is bits there: bit 0 set for a palette, where it was set for none,
and then 02 for LZW, 04 for Huffman's codes, 20 for a plane as it is, and 40 for four. So a 2
or a 3 there is Huffman's codes in the first game and LZW in the others, and which a file is
has to be told from its other pictures, or from the game's other files.

There, a picture of sixteen colors without a palette is a piece of the one before it that
has one, to be drawn over it at its places, and is in that one's colors. An entry with a
palette and no size is a palette alone.

Pictures are given here as a byte a pixel, a color's number from 0 to 15.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass

RAW_PLANE, RAW_PLANES, HUFFMAN, LZW = 4, 5, 1, 0
# the later games' bits for the same, and the kinds only each has: a 2 or a 3 is in both
_LATER = {0x02: LZW, 0x04: HUFFMAN, 0x20: RAW_PLANE, 0x40: RAW_PLANES}
_FIRST_ONLY, _LATER_ONLY = {0, 1, 8, 9, 10, 11}, {4, 0x20, 0x21, 0x40, 0x41}

# the EGA's 16 colors as it starts, by their numbers among its 64
DEFAULT_PALETTE = bytes([0, 1, 2, 3, 4, 5, 20, 7, 56, 57, 58, 59, 60, 61, 62, 63])


def ega_color(number: int) -> tuple[int, int, int]:
    """One of the EGA's 64 colors: two bits each of red, green and blue."""
    return (
        (number >> 2 & 1) * 170 + (number >> 5 & 1) * 85,
        (number >> 1 & 1) * 170 + (number >> 4 & 1) * 85,
        (number & 1) * 170 + (number >> 3 & 1) * 85,
    )


@dataclass(frozen=True)
class Entry:
    """A picture's entry in its file's directory."""

    number: int
    offset: int
    places: int  # how many places to draw it at come before it
    kind: int
    width: int
    height: int
    unknown: int
    later: bool = False  # whether the kind is read as the games after the first have it

    @property
    def packing(self) -> int:
        if self.later:
            # one bit, and the palette's: the games after these two set others, not yet known
            return _LATER.get(self.kind & ~1, -1)
        return self.kind >> 1

    @property
    def has_palette(self) -> bool:
        return bool(self.kind & 1) if self.later else not self.kind & 1


@dataclass(frozen=True)
class Picture:
    """A picture, unpacked."""

    width: int
    height: int
    pixels: bytes  # a byte a pixel, row by row
    palette: bytes | None  # 16 of the EGA's colors by number; None for a picture of two colors
    places: list[tuple[int, int]]  # where it's drawn, across and down; none for where it's told

    def colors(self) -> list[tuple[int, int, int]]:
        """The 16 colors the pixels' numbers stand for, as red, green and blue."""
        if self.palette is None:
            return [(0, 0, 0)] + [(255, 255, 255)] * 15
        return [ega_color(number) for number in self.palette]

    def png(self) -> bytes:
        """The picture as a PNG file's bytes."""

        def chunk(kind: bytes, body: bytes) -> bytes:
            return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

        rows = b"".join(
            b"\0" + self.pixels[row * self.width : (row + 1) * self.width] for row in range(self.height)
        )
        return (
            b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 3, 0, 0, 0))
            + chunk(b"PLTE", b"".join(bytes(color) for color in self.colors()))
            + chunk(b"IDAT", zlib.compress(rows))
            + chunk(b"IEND", b"")
        )


class _Bits:
    """A file's bytes read a bit at a time, from each byte's high bit down."""

    def __init__(self, data: bytes, at: int):
        self.data = data
        self.at = at
        self.held = 0
        self.count = 0

    def take(self, width: int) -> int:
        while self.count < width:
            self.held = (self.held << 8 | self.data[self.at]) & 0xFFFFFF
            self.at += 1
            self.count += 8
        self.count -= width
        return self.held >> self.count & ((1 << width) - 1)


def _unpack_lzw(data: bytes, at: int, count: int) -> bytes:
    """Pixels packed by the EGA files' LZW."""
    bits = _Bits(data, at)
    pixels = bytearray()
    before: dict[int, int] = {}  # a code's string is another code's
    last: dict[int, int] = {}  # and one pixel more
    width = 5
    following = 18  # the next code to be given a string

    def code() -> int | None:
        """The next code that is a string's; None at the picture's end."""
        nonlocal width, following
        while True:
            found = bits.take(width)
            if found == 16:
                width += 1
            elif found == 17:
                width, following = 5, 18
                return None if start() is None else code()
            else:
                return None if found == (1 << width) - 1 else found

    def put(found: int) -> int:
        """Write a code's string, and give its first pixel."""
        string = []
        while found >= 18:
            string.append(last[found])
            found = before[found]
        string.append(found)
        pixels.extend(reversed(string))
        return found

    previous = first = 0

    def start() -> int | None:
        """The first code after the table starts over is written and nothing is added."""
        nonlocal previous, first
        found = code()
        if found is not None:
            previous, first = found, put(found)
        return found

    if start() is None:
        return bytes(pixels)
    while len(pixels) < count and (found := code()) is not None:
        if found >= following:
            # a code not yet in the table is the last string and its own first pixel again
            first = put(previous)
            pixels.append(first)
        else:
            first = put(found)
        before[following], last[following] = previous, first
        following += 1
        previous = found
    return bytes(pixels)


def _unpack_huffman(data: bytes, at: int, count: int) -> bytes:
    """Bytes packed by the CGA files' Huffman codes."""
    # the codes: a count less one, then for each its length in bits, its byte, and its bits
    # in a byte, or in two when it's longer than eight
    codes: dict[tuple[int, int], int] = {}
    entries = data[at] + 1
    at += 1
    for _ in range(entries):
        length, value = data[at], data[at + 1]
        size = 2 if length > 8 else 1
        bits = int.from_bytes(data[at + 2 : at + 2 + size], "big") >> (8 * size - length)
        codes[(length, bits)] = value
        at += 2 + size
    stream = _Bits(data, at)
    found = bytearray()
    while len(found) < count:
        length = bits = 0
        while (length, bits) not in codes:
            bits = bits << 1 | stream.take(1)
            length += 1
            if length > 16:
                raise ValueError("a picture's Huffman codes don't match its bits")
        found.append(codes[(length, bits)])
    return bytes(found)


def _spread(rows: bytes, row_bytes: int, width: int, height: int, planes: int) -> bytes:
    """Rows of bits, a plane after another in each row, as a byte a pixel."""
    pixels = bytearray(width * height)
    for row in range(height):
        for plane in range(planes):
            start = (row * planes + plane) * row_bytes
            for column in range(width):
                if rows[start + column // 8] >> (7 - column % 8) & 1:
                    pixels[row * width + column] |= 1 << plane
    return bytes(pixels)


def _kinds(files: list[bytes]) -> set[int]:
    """The kinds of picture in files' directories."""
    kinds = set()
    for data in files:
        (first,) = struct.unpack_from("<I", data, 0)
        if first % 12 == 0 and 12 <= first <= len(data):
            kinds |= {
                data[12 * number + 5]
                for number in range(first // 12)
                if data[12 * number : 12 * number + 4] != bytes(4)
            }
    return kinds


def is_later(files: list[bytes]) -> bool | None:
    """
    Whether picture files are the later games' kind, from the kinds of picture in them. None
    when they have only the two kinds that both have.
    """
    kinds = _kinds(files)
    return True if kinds & _LATER_ONLY else False if kinds & _FIRST_ONLY else None


def is_known(files: list[bytes]) -> bool:
    """
    Whether every picture in the files is of a kind that is read. The games after
    Spellcasting 201 have kinds with more bits set, and what the old bits mean there isn't
    known either, so none of theirs are read.
    """
    return not _kinds(files) - _FIRST_ONLY - _LATER_ONLY - {2, 3}


class PictureFile:
    """A .PIC file. The EGA's and the CGA's differ only in that the CGA's have no palettes."""

    def __init__(self, data: bytes, ega: bool, later: bool | None = None):
        self.data = data
        self.ega = ega
        (first,) = struct.unpack_from("<I", data, 0)
        if first % 12 or not 12 <= first <= len(data):
            raise ValueError("not a picture file: its directory doesn't end where its first picture starts")
        if later is None:
            later = is_later([data])
        # a file of 2s and 3s alone: the first game has those for the CGA only, and the
        # others for the EGA only
        self.later = ega if later is None else later
        self.entries = [
            Entry(number, *struct.unpack_from("<IBBHHH", data, 12 * number), self.later)
            for number in range(first // 12)
        ]

    def pictures(self) -> list[Entry]:
        """The entries that have a picture."""
        return [entry for entry in self.entries if entry.offset]

    def _palette_at(self, entry: Entry) -> bytes:
        at = entry.offset + 4 * entry.places
        return self.data[at : at + 16]

    def read(self, entry: Entry) -> Picture:
        at = entry.offset
        places = [struct.unpack_from("<HH", self.data, at + 4 * index) for index in range(entry.places)]
        at += 4 * entry.places
        # the CGA's pictures are all of two colors, and its files have no palettes
        palette = None
        if entry.has_palette and (self.ega or self.later):
            palette = self.data[at : at + 16]
            at += 16
        elif self.ega and entry.packing == RAW_PLANES:
            palette = DEFAULT_PALETTE
        elif self.ega and self.later and entry.packing == LZW:
            # a piece of the picture before it that has a palette, in that one's colors
            before = [other for other in self.entries[: entry.number] if other.offset and other.has_palette]
            palette = self._palette_at(before[-1]) if before else DEFAULT_PALETTE
        row_bytes = (entry.width + 7) // 8
        if entry.width == 0 or entry.height == 0:
            # a palette alone, or nothing
            pixels = b""
        elif entry.packing == LZW:
            pixels = _unpack_lzw(self.data, at, entry.width * entry.height)
            pixels = pixels.ljust(entry.width * entry.height, b"\0")
        elif entry.packing == HUFFMAN:
            rows = _unpack_huffman(self.data, at, row_bytes * entry.height)
            pixels = _spread(rows, row_bytes, entry.width, entry.height, 1)
        elif entry.packing in (RAW_PLANE, RAW_PLANES):
            planes = 4 if entry.packing == RAW_PLANES else 1
            rows = self.data[at : at + row_bytes * planes * entry.height]
            pixels = _spread(rows, row_bytes, entry.width, entry.height, planes)
        else:
            raise ValueError(f"picture {entry.number} is packed a way that isn't known: {entry.kind:#04x}")
        return Picture(entry.width, entry.height, pixels, palette, places)

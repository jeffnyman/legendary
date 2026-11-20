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

*Spellcasting 301* and *Gateway* keep theirs a third way, for more displays. The byte at 5
is two things: its low four bits are the display the picture is for, 1 the EGA, 2 the VGA
in 16 colors, 3 the VGA in 256, 4 a screen of 16 colors wider than the VGA's, and 5, which
Eric the Unready has, the Super VGA's 640 by 480 in 256; and its high four are bits, 10 for
a palette and 20 or 40 for the pixels, which are packed one way now, by PKWARE's implode.
A palette is 16 of the EGA's colors by number for the EGA, and for the VGA three bytes a
color, red, green and blue from 0 to 63: 16 colors, or 256 for displays 3 and 5. The
pixels, unpacked, are a byte each in 256 colors; two to a byte in 16, the left one in the
byte's low half; and eight to a byte in two colors. A row starts on a byte.

Imploded data starts with two bytes, 0 for bytes as they are and the size of the window as
a power of two, 4 to 6 for 1,024 to 4,096. Then bits, from each byte's low bit up. A 0 bit is
followed by a byte. A 1 bit is followed by a length, as a Huffman code and some bits more,
and a distance back, as a Huffman code for its high bits and its low bits as they are: that
many bytes are copied from that far back. A length of 519 ends it.

*Mission Critical* keeps its a fourth way. Its directory is 256 entries of twenty bytes:

    0  where the picture starts in the file, or 0 for none
    4  how many bytes its pixels are in the file
    8  its kind: 0 a picture, 3 a change to one, and 4 not yet understood
    9  FFh when the picture starts with colors, and 0 when it has none
    10 where it is drawn, across and down, when it is a piece of another
    14 its width and then its height, in pixels
    18 a word not yet understood

A picture with colors starts with two words, where in the 256 colors its own start and how
many it has, both counted in bytes at three a color, and then the colors, red, green and blue
from 0 to 63. Nearly all have the whole 256. The pixels are a byte each. When their size in
the file is the width times the height they are as they are; otherwise they are packed as
LHA packs a file by its method `-lh4-`: Lempel and Ziv's copying over a window of 4,096
bytes, with the lengths and distances in Huffman's codes that are sent anew for each block.

A block starts with a 16-bit count of its codes, then three tables of code lengths. The first
is for the lengths of the second, and has 19 of them, each three bits, or more for a length
over 6: a 7 and then a 1 bit for each one more; after the third there are two bits for how
many zeros follow. The second has up to 510, for a byte or a length of copy, each written
with the first's codes, where 0, 1 and 2 are runs of zeros: one, 3 to 18, and 20 to 531. The
third has 13, for a distance, kept as the first is but with no run after the third. A table
with a count of 0 is one code alone, given next. A code below 256 is a byte; one above is
a copy of that less 253 bytes, and then a distance's code says how many bits of distance
follow, with the top one left out. Bits are read from each byte's high bit down.

A picture of kind 3 is not pixels but a change to pixels: each byte is to be exclusive-ored
with the pixel already there, so a 0 leaves it. A door's two leaves, opened, are a change
to the picture of the door shut. Which picture a change is to, the file doesn't say; it is
found here by which one the change makes a picture of and not noise.

Pictures are given here as a byte a pixel, a color's number.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass

RAW_PLANE, RAW_PLANES, HUFFMAN, LZW = 4, 5, 1, 0
# the later games' bits for the same, and the kinds only each has: a 2 or a 3 is in both
_LATER = {0x02: LZW, 0x04: HUFFMAN, 0x20: RAW_PLANE, 0x40: RAW_PLANES}
_FIRST_ONLY, _LATER_ONLY = {0, 1, 8, 9, 10, 11}, {4, 0x20, 0x21, 0x40, 0x41}
IMPLODED = 6  # the one way the games from Spellcasting 301 on pack a picture's pixels
LHA, RAW_BYTES = 7, 8  # and Mission Critical's two: LHA's -lh4-, and a byte a pixel as it is

_CHANGE = 3  # the kind of a picture of Mission Critical's that is a change to another
# for finding which picture a change is to: how many pixels are looked at, how little they may
# differ from their neighbors for the result to be a picture, how much makes it noise, and how
# few pixels are too few to tell by
_SAMPLES, _SMOOTH, _NOISE, _SMALL = 3000, 24.0, 40.0, 600
_BANDS, _FEW = 8, 20  # the bands a change is looked at in, and how few pixels of one say nothing

# Mission Critical's directory: 256 entries of twenty bytes
_SIZED_ENTRY, _SIZED_COUNT = 20, 256

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
    imploded: bool = False  # or as the games from Spellcasting 301 on have it
    sized: bool = False  # or the entry is Mission Critical's, which has the four below
    size: int = 0  # how many bytes its pixels are in the file
    colors: int = 0  # FFh when it starts with colors
    across: int = 0  # where it's drawn, when it's a piece of another
    down: int = 0

    @property
    def display(self) -> int:
        """The display an imploded picture is for: 1 the EGA, 2 the VGA in 16 colors, 3 in 256."""
        return self.kind & 0x0F

    @property
    def packing(self) -> int:
        if self.sized:
            return RAW_BYTES if self.size == self.width * self.height else LHA
        if self.imploded:
            return IMPLODED if self.kind & 0x60 else -1
        if self.later:
            # one bit, and the palette's: the games after these two set others, not yet known
            return _LATER.get(self.kind & ~1, -1)
        return self.kind >> 1

    @property
    def has_palette(self) -> bool:
        if self.sized:
            return self.colors == 0xFF
        if self.imploded:
            return bool(self.kind & 0x10)
        return bool(self.kind & 1) if self.later else not self.kind & 1


@dataclass(frozen=True)
class Picture:
    """A picture, unpacked."""

    width: int
    height: int
    pixels: bytes  # a byte a pixel, row by row
    palette: bytes | None  # 16 of the EGA's colors by number; None for a picture of two colors
    places: list[tuple[int, int]]  # where it's drawn, across and down; none for where it's told
    rgb: bytes | None = None  # or its colors as they are: red, green and blue, a byte each

    def colors(self) -> list[tuple[int, int, int]]:
        """The colors the pixels' numbers stand for, as red, green and blue."""
        if self.rgb is not None:
            return [(self.rgb[at], self.rgb[at + 1], self.rgb[at + 2]) for at in range(0, len(self.rgb), 3)]
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


def _lengths(packed: list[int]) -> tuple[list[int], list[int]]:
    """
    A Huffman code as implode has it, from its lengths kept short: a byte is a length in its
    low four bits and, in its high four, how many symbols more have it. What's given is how
    many codes there are of each length, and the symbols in the codes' order.
    """
    lengths: list[int] = []
    for byte in packed:
        lengths += [byte & 15] * ((byte >> 4) + 1)
    count = [0] * 16
    for length in lengths:
        count[length] += 1
    start = [0] * 16
    for length in range(1, 15):
        start[length + 1] = start[length] + count[length]
    symbols = [0] * len(lengths)
    for symbol, length in enumerate(lengths):
        if length:
            symbols[start[length]] = symbol
            start[length] += 1
    return count, symbols


# implode's codes for a length and for a distance's high bits, and what a length's symbol is:
# where it starts, and how many bits more say how far past that
_LENGTH_CODE = _lengths([2, 35, 36, 53, 38, 23])
_DISTANCE_CODE = _lengths([2, 20, 53, 230, 247, 151, 248])
_LENGTH_BASE = (3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264)
_LENGTH_EXTRA = (0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8)
_END = 519

# the displays of 256 colors: the VGA's, and the bigger screen's that Eric the Unready has
_IN_256 = (3, 5)


def explode(data: bytes, at: int) -> bytes:
    """Bytes packed by PKWARE's implode, unpacked, from where they start in a file."""
    coded, window = data[at], data[at + 1]
    if coded != 0 or not 4 <= window <= 6:
        raise ValueError(f"not imploded data, or of a kind that isn't read: {coded:02x} {window:02x}")
    bit = (at + 2) * 8

    def take(width: int) -> int:
        nonlocal bit
        value = 0
        for place in range(width):
            value |= (data[bit >> 3] >> (bit & 7) & 1) << place
            bit += 1
        return value

    def symbol(code: tuple[list[int], list[int]]) -> int:
        # the codes are kept with their bits turned over, a 0 for a 1
        count, symbols = code
        found = first = index = 0
        for length in range(1, 14):
            found |= take(1) ^ 1
            if found < first + count[length]:
                return symbols[index + found - first]
            index += count[length]
            first = (first + count[length]) << 1
            found <<= 1
        raise ValueError("imploded data has a code that isn't one")

    unpacked = bytearray()
    try:
        while True:
            if not take(1):
                unpacked.append(take(8))
                continue
            which = symbol(_LENGTH_CODE)
            length = _LENGTH_BASE[which] + take(_LENGTH_EXTRA[which])
            if length == _END:
                return bytes(unpacked)
            # a length of two has a distance with two low bits, whatever the window
            low = 2 if length == 2 else window
            distance = (symbol(_DISTANCE_CODE) << low) + take(low) + 1
            if distance > len(unpacked):
                raise ValueError("imploded data copies from before its start")
            for _ in range(length):
                unpacked.append(unpacked[-distance])
    except IndexError:
        raise ValueError("imploded data goes past the end of its file") from None


def _code_table(lengths: list[int], bits: int) -> list[int]:
    """
    A Huffman code from its lengths, as a table to look the next `bits` bits up in: each entry
    is a symbol and, in its low five bits, the length of its code. The codes are in order of
    length and then of symbol.
    """
    table = [0] * (1 << bits)
    code = 0
    for length in range(1, bits + 1):
        for symbol, its in enumerate(lengths):
            if its == length:
                start = code << (bits - length)
                entry = symbol << 5 | length
                for index in range(start, start + (1 << (bits - length))):
                    table[index] = entry
                code += 1
        code <<= 1
    return table


class _LhaBits:
    """Bytes read as bits from each one's high bit down, with a look ahead of sixteen."""

    def __init__(self, data: bytes):
        self.data = data
        self.at = 0
        self.held = 0
        self.count = 0

    def _fill(self, width: int) -> None:
        # past the end the bits are zeros, to look ahead into: `over` says when they've been used
        while self.count < width:
            chunk = self.data[self.at : self.at + 8]
            self.at += 8
            self.held = self.held << 64 | int.from_bytes(chunk.ljust(8, b"\0"), "big")
            self.count += 64

    def take(self, width: int) -> int:
        if self.count < width:
            self._fill(width)
        self.count -= width
        found = self.held >> self.count
        self.held &= (1 << self.count) - 1
        return found

    def symbol(self, table: list[int] | int) -> int:
        """The next symbol by a table of codes, or the one symbol a table without codes has."""
        if isinstance(table, int):
            return table
        if self.count < 16:
            self._fill(16)
        entry = table[self.held >> (self.count - 16)]
        if not entry:
            raise ValueError("packed data has a code that isn't one")
        self.count -= entry & 31
        self.held &= (1 << self.count) - 1
        return entry >> 5

    @property
    def over(self) -> bool:
        """Whether more bits have been read than there are."""
        return 8 * self.at - self.count > 8 * len(self.data)


def _lha_lengths(bits: _LhaBits, count_bits: int, limit: int, run_after: int) -> list[int] | int:
    """A table of code lengths as -lh4- keeps the first and the third; one symbol, for a count of 0."""
    count = bits.take(count_bits)
    if count == 0:
        return bits.take(count_bits)
    lengths = [0] * limit
    index = 0
    while index < min(count, limit):
        length = bits.take(3)
        if length == 7:
            while bits.take(1):
                length += 1
        lengths[index] = length
        index += 1
        if index == run_after:
            index += bits.take(2)
    return _code_table(lengths, 16)


_LHA_CODES, _LHA_LENGTHS, _LHA_DISTANCES = 510, 19, 13


def unpack_lha(data: bytes, size: int) -> bytes:  # noqa: PLR0912
    """Bytes packed by LHA's -lh4-, unpacked to the size given."""
    bits = _LhaBits(data)
    unpacked = bytearray()
    left = 0
    codes: list[int] | int = 0
    distances: list[int] | int = 0
    while len(unpacked) < size:
        if left == 0:
            left = bits.take(16)
            first = _lha_lengths(bits, 5, _LHA_LENGTHS, 3)
            count = bits.take(9)
            if count == 0:
                codes = bits.take(9)
            else:
                lengths = [0] * _LHA_CODES
                index = 0
                while index < min(count, _LHA_CODES):
                    found = bits.symbol(first)
                    if found > 2:
                        lengths[index] = found - 2
                        index += 1
                    else:
                        # a run of zeros: one, or 3 and up, or 20 and up
                        index += 1 if found == 0 else bits.take(4) + 3 if found == 1 else bits.take(9) + 20
                codes = _code_table(lengths, 16)
            distances = _lha_lengths(bits, 4, _LHA_DISTANCES, -1)
            if left == 0 or bits.over:
                raise ValueError("packed data ends before its picture does")
        left -= 1
        found = bits.symbol(codes)
        if found < 256:
            unpacked.append(found)
            continue
        length = found - 253
        high = bits.symbol(distances)
        distance = ((1 << (high - 1)) + bits.take(high - 1) if high else 0) + 1
        if distance > len(unpacked):
            raise ValueError("packed data copies from before its start")
        if distance >= length:
            unpacked += unpacked[-distance : len(unpacked) - distance + length]
        else:
            for _ in range(length):
                unpacked.append(unpacked[-distance])
    if bits.over:
        raise ValueError("packed data ends before its picture does")
    return bytes(unpacked[:size])


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


def is_imploded(files: list[bytes]) -> bool:
    """
    Whether picture files are the kind the games from Spellcasting 301 on have: a display
    in the low four bits of a picture's kind, with a bit above for how it's packed.
    """
    return any(kind & 0x0E and kind & 0x70 for kind in _kinds(files))


def is_known(files: list[bytes]) -> bool:
    """
    Whether every picture in the files is of a kind that is read: one of the three ways the
    games keep them, and not something else again.
    """
    return is_imploded(files) or not _kinds(files) - _FIRST_ONLY - _LATER_ONLY - {2, 3}


class PictureFile:
    """A .PIC file. The EGA's and the CGA's differ only in that the CGA's have no palettes."""

    def __init__(self, data: bytes, ega: bool, later: bool | None = None, imploded: bool | None = None):
        self.data = data
        self.ega = ega
        (first,) = struct.unpack_from("<I", data, 0)
        self.sized = first == _SIZED_ENTRY * _SIZED_COUNT <= len(data)
        self._changes: dict[int, tuple[int | None, bytes | None]] | None = None
        if self.sized:
            # Mission Critical's: nothing of the other games' kinds applies
            self.later, self.imploded = False, False
            self.entries = [
                Entry(
                    number,
                    offset,
                    0,
                    kind,
                    width,
                    height,
                    unknown,
                    False,
                    False,
                    True,
                    size,
                    colors,
                    across,
                    down,
                )
                for number, (offset, size, kind, colors, across, down, width, height, unknown) in enumerate(
                    struct.iter_unpack("<IIBBhhHHH", data[:first])
                )
            ]
            return
        if first % 12 or not 12 <= first <= len(data):
            raise ValueError("not a picture file: its directory doesn't end where its first picture starts")
        if later is None:
            later = is_later([data])
        # a file of 2s and 3s alone: the first game has those for the CGA only, and the
        # others for the EGA only
        self.later = ega if later is None else later
        self.imploded = is_imploded([data]) if imploded is None else imploded
        self.entries = [
            Entry(number, *struct.unpack_from("<IBBHHH", data, 12 * number), self.later, self.imploded)
            for number in range(first // 12)
        ]

    def pictures(self) -> list[Entry]:
        """The entries that have a picture."""
        return [entry for entry in self.entries if entry.offset]

    def _palette_at(self, entry: Entry) -> bytes:
        at = entry.offset + 4 * entry.places
        return self.data[at : at + 16]

    def _colors_of(self, entry: Entry) -> tuple[bytes | None, bytes | None]:
        """
        An imploded picture's colors, as the EGA's numbers or as red, green and blue: its
        own palette, or that of the picture before it that has one, which it is a piece of.
        """
        source = entry
        if not entry.has_palette:
            before = [other for other in self.entries[: entry.number] if other.offset and other.has_palette]
            source = before[-1] if before else entry
        if not source.has_palette:
            return None, None
        start = source.offset + 4 * source.places
        if source.display == 1:
            return self.data[start : start + 16], None
        # the VGA's colors go from 0 to 63
        size = 3 * (256 if source.display in _IN_256 else 16)
        return None, bytes(value * 255 // 63 for value in self.data[start : start + size])

    def _read_imploded(self, entry: Entry) -> Picture:
        """A picture of the third kind: its places, its palette if it has one, and its pixels."""
        at = entry.offset
        places = [struct.unpack_from("<HH", self.data, at + 4 * index) for index in range(entry.places)]
        if entry.width == 0 or entry.height == 0:
            return Picture(entry.width, entry.height, b"", None, places)
        if entry.packing != IMPLODED:
            raise ValueError(f"picture {entry.number} is packed a way that isn't known: {entry.kind:#04x}")
        palette, rgb = self._colors_of(entry)
        at += 4 * entry.places
        if entry.has_palette:
            at += 16 if entry.display == 1 else 768 if entry.display in _IN_256 else 48

        unpacked = explode(self.data, at)
        width, height = entry.width, entry.height
        # how many pixels are in a byte is told by how many bytes there are
        if len(unpacked) == width * height and entry.display in _IN_256:
            pixels = unpacked
            # with no palette to be had, shades of gray show what's there
            rgb = rgb or bytes(shade for shade in range(256) for _ in range(3))
        elif len(unpacked) == (width + 1) // 2 * height:
            row_bytes = (width + 1) // 2
            pixels = bytes(
                unpacked[row * row_bytes + column // 2] >> (4 * (column & 1)) & 15
                for row in range(height)
                for column in range(width)
            )
            if rgb is None and palette is None:
                palette = DEFAULT_PALETTE
        elif len(unpacked) == (width + 7) // 8 * height:
            pixels = _spread(unpacked, (width + 7) // 8, width, height, 1)
            palette, rgb = None, None
        else:
            raise ValueError(f"picture {entry.number} unpacks to {len(unpacked)} bytes, which isn't its size")
        return Picture(width, height, pixels, palette, places, rgb)

    def _sized_colors(self, entry: Entry) -> bytes:
        """
        The 256 colors a picture of Mission Critical's is shown in: each picture's own, put
        over those of the pictures before it in the file, up to this one.
        """
        colors = bytearray(768)
        for other in self.entries[: entry.number + 1]:
            if other.offset and other.has_palette:
                start, size = struct.unpack_from("<HH", self.data, other.offset)
                colors[start : start + size] = self.data[other.offset + 4 : other.offset + 4 + size]
        # the VGA's colors go from 0 to 63
        return bytes(value * 255 // 63 for value in colors[:768])

    def _read_sized(self, entry: Entry) -> Picture:
        """A picture of Mission Critical's: its colors if it has them, and its pixels."""
        places = [(entry.across, entry.down)] if entry.across or entry.down else []
        pixels = self._sized_pixels(entry)
        colors = entry
        if entry.kind == _CHANGE and entry.width and entry.height:
            under, changed = self.changes().get(entry.number, (None, None))
            if under is not None and changed is not None:
                # what the change makes of the picture it changes, in that picture's colors
                pixels, colors = changed, self.entries[under]
        return Picture(entry.width, entry.height, pixels, None, places, self._sized_colors(colors))

    def _sized_pixels(self, entry: Entry) -> bytes:
        """The bytes a picture of Mission Critical's unpacks to."""
        at = entry.offset
        if entry.has_palette:
            at += 4 + struct.unpack_from("<H", self.data, at + 2)[0]
        packed = self.data[at : at + entry.size]
        return unpack_lha(packed, entry.width * entry.height) if entry.packing == LHA else packed

    def changed(self, entry: Entry) -> int | None:
        """The picture a change is taken to be to, by its number; None when none was found."""
        return self.changes().get(entry.number, (None, None))[0]

    def changes(self) -> dict[int, tuple[int | None, bytes | None]]:  # noqa: PLR0912, PLR0915
        """
        For each change in a file of Mission Critical's, the picture it is taken to change and
        what it makes of it, where it changes it.

        The file doesn't say which picture a change is to; the program knows. But a change
        put over the wrong picture makes noise, and over the right one makes a picture, so the
        right one is found by how smooth the result is: how little a pixel differs from the
        one beside it. The changes are gone through in order with the picture as it stands,
        since changes come in runs, each to what the last left. A change that makes noise of
        that is tried over every whole picture in the file, and started again from the best.
        One that makes noise of them all is left as it is: its picture is in another file.
        """
        if self._changes is not None:
            return self._changes
        found: dict[int, tuple[int | None, bytes | None]] = {}
        self._changes = found
        whole = [each for each in self.entries if each.offset and each.width and each.kind != _CHANGE]
        pixels: dict[int, bytes] = {}
        colors: dict[int, bytes] = {}

        def fits(change: Entry, width: int, height: int) -> bool:
            return (
                change.across >= 0
                and change.down >= 0
                and change.across + change.width <= width
                and change.down + change.height <= height
            )

        def roughness(change: Entry, mask: bytes, under: bytes | bytearray, width: int, rgb: bytes) -> float:
            """
            How much pixels differ from their neighbors with the change made, on a few thousand
            of them: the most of any of eight bands down the change, since a change over the
            wrong picture can be right in most of it and noise in a corner.
            """
            count = change.width * change.height
            step = max(1, count // _SAMPLES)
            totals, pairs = [0] * _BANDS, [0] * _BANDS
            for index in range(0, count - 1, step):
                column = index % change.width
                if column == change.width - 1:
                    continue
                at = (index // change.width + change.down) * width + column + change.across
                first, second = 3 * (under[at] ^ mask[index]), 3 * (under[at + 1] ^ mask[index + 1])
                band = index * _BANDS // count
                totals[band] += (
                    abs(rgb[first] - rgb[second])
                    + abs(rgb[first + 1] - rgb[second + 1])
                    + abs(rgb[first + 2] - rgb[second + 2])
                )
                pairs[band] += 1
            return max((total / many for total, many in zip(totals, pairs) if many >= _FEW), default=0.0)

        standing: bytearray | None = None  # the picture as the changes so far have left it
        base: Entry | None = None
        for change in self.entries:
            if not (change.offset and change.kind == _CHANGE and change.width and change.height):
                continue
            mask = self._sized_pixels(change)
            small = change.width * change.height < _SMALL
            score = float("inf")
            if small:
                # too few pixels to tell by: it is taken to change the picture as it stands, or
                # the whole picture just before it when one has come since
                before = next((other for other in reversed(whole) if other.number < change.number), None)
                if before is not None and (base is None or base.number < before.number):
                    if before.number not in pixels:
                        pixels[before.number] = self._sized_pixels(before)
                        colors[before.number] = self._sized_colors(before)
                    standing, base = bytearray(pixels[before.number]), before
                score = 0.0
            elif standing is not None and base is not None and fits(change, base.width, base.height):
                score = roughness(change, mask, standing, base.width, colors[base.number])
            if score > _SMOOTH:
                # noise, or nothing to change yet: every whole picture in the file is tried
                best, best_score = None, score
                for other in whole:
                    if not fits(change, other.width, other.height):
                        continue
                    if other.number not in pixels:
                        pixels[other.number] = self._sized_pixels(other)
                        colors[other.number] = self._sized_colors(other)
                    # colors that are nearly all one would make anything look smooth
                    if len({colors[other.number][index : index + 3] for index in range(0, 768, 3)}) < 16:
                        continue
                    tried = roughness(change, mask, pixels[other.number], other.width, colors[other.number])
                    if tried < best_score:
                        best, best_score = other, tried
                if best is not None:
                    standing, base, score = bytearray(pixels[best.number]), best, best_score
            if standing is None or base is None or not fits(change, base.width, base.height):
                found[change.number] = (None, None)
                continue
            if score > _NOISE and not small:
                # it makes noise of every picture here
                found[change.number] = (None, None)
                continue
            start = change.down * base.width + change.across
            rows = []
            for row in range(change.height):
                at = start + row * base.width
                line = bytes(
                    under ^ over
                    for under, over in zip(
                        standing[at : at + change.width], mask[row * change.width : (row + 1) * change.width]
                    )
                )
                standing[at : at + change.width] = line
                rows.append(line)
            found[change.number] = (base.number, b"".join(rows))
        return found

    def read(self, entry: Entry) -> Picture:
        if self.sized:
            return self._read_sized(entry)
        if self.imploded:
            return self._read_imploded(entry)
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

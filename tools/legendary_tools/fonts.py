"""The game's fonts: its .FNT files, from Spellcasting 301 on.

A file is ten bytes and then the letters:

    0  a byte that is 1
    1  how many bits a pixel is: 1, or 8 in one font of Mission Critical's
    2  how many bytes a row of a letter is
    3  the first character it has, and at 4 the last: 21 and 7F, `!` to DEL
    5  for a font whose letters differ in width, the gap left between letters; else 0
    6  how many rows a letter is
    7  for a font whose letters are all one width, the width; else FF
    8  FF when a second table follows the widths; else 0, or 1 in two of Mission Critical's
    9  zero

A font whose letters differ in width then has a table of 128 bytes, one for each character's
code from 0: its width in pixels. Most have a second after it, of a byte whose two halves are
small numbers, not yet understood; eight of Mission Critical's thirteen fonts have only the
first. A font of one width has neither.

Then each character's rows, a bit a pixel from each byte's top bit, the first character
first.

The font of eight bits a pixel has a byte a pixel, a color's number, and 0 where there is
none. After its letters are 32 bytes with a bit for each of the 256 colors, from each
byte's low bit, set for the colors the font brings with it; and then those colors, red,
green and blue from 0 to 63.
"""

from __future__ import annotations

from dataclasses import dataclass

from .pictures import Picture

_HEADER = 10
_TABLE = 128


@dataclass(frozen=True)
class Font:
    """A font: its letters' rows, and how wide each is."""

    height: int
    row_bytes: int
    first: int
    last: int
    gap: int  # the pixels left between one letter and the next, where the file says
    fixed_width: int | None  # every letter's width, or None when they differ
    widths: bytes  # by a character's code, when they differ
    hints: bytes  # the second table, as it is
    rows: bytes
    depth: int = 1  # how many bits a pixel is: 1, or 8 for a font in colors
    colors: bytes = b""  # the colors a font in colors brings: a color's number, and its three parts

    def width(self, code: int) -> int:
        if self.fixed_width is not None:
            return self.fixed_width
        return self.widths[code] if code < len(self.widths) else 0

    def has(self, code: int) -> bool:
        return self.first <= code <= self.last

    def glyph(self, code: int) -> list[list[int]]:
        """A character's pixels, row by row, as wide as the character is."""
        size = self.height * self.row_bytes
        start = (code - self.first) * size
        if self.depth == 8:
            wide = min(self.width(code), self.row_bytes)
            return [
                list(self.rows[start + row * self.row_bytes : start + row * self.row_bytes + wide])
                for row in range(self.height)
            ]
        wide = min(self.width(code), 8 * self.row_bytes)
        return [
            [
                self.rows[start + row * self.row_bytes + column // 8] >> (7 - column % 8) & 1
                for column in range(wide)
            ]
            for row in range(self.height)
        ]

    def sheet(self, across: int = 16) -> Picture:
        """Every character, in rows of so many, each in a cell as wide as the widest."""
        codes = list(range(self.first, self.last + 1))
        cell = max(self.width(code) for code in codes) + 2
        tall = self.height + 2
        down = -(-len(codes) // across)
        width, height = across * cell + 2, down * tall + 2
        pixels = bytearray(width * height)
        for index, code in enumerate(codes):
            left, top = 2 + index % across * cell, 2 + index // across * tall
            for row, bits in enumerate(self.glyph(code)):
                for column, bit in enumerate(bits):
                    pixels[(top + row) * width + left + column] = bit
        return Picture(width, height, bytes(pixels), None, [], self._rgb())

    def _rgb(self) -> bytes | None:
        """A font in colors' 256 colors: its own, and white for the rest but 0, which is none."""
        if self.depth != 8:
            return None
        rgb = bytearray(b"\0\0\0" + b"\xff" * 765)
        for at in range(0, len(self.colors), 4):
            number = self.colors[at]
            rgb[3 * number : 3 * number + 3] = bytes(
                part * 255 // 63 for part in self.colors[at + 1 : at + 4]
            )
        return bytes(rgb)

    def line(self, text: str) -> Picture:
        """Some text set in the font, each letter after the last by its width and the gap."""
        codes = [ord(letter) for letter in text]
        # a space is as wide as the file says, or as an `n` where it says nothing
        advance = [self.width(code) or self.width(ord("n")) for code in codes]
        gap = self.gap if self.fixed_width is None else 0
        width = sum(advance) + gap * len(codes) + 2
        pixels = bytearray(width * (self.height + 2))
        left = 1
        for code, wide in zip(codes, advance):
            if self.has(code):
                for row, bits in enumerate(self.glyph(code)):
                    for column, bit in enumerate(bits):
                        pixels[(1 + row) * width + left + column] = bit
            left += wide + gap
        return Picture(width, self.height + 2, bytes(pixels), None, [], self._rgb())


_COLOR_BITS = 32


def read_font(data: bytes) -> Font:
    if len(data) < _HEADER or data[0] != 1 or data[1] not in (1, 8):
        raise ValueError("not a font file: it doesn't start as one does")
    depth, row_bytes, first, last, gap, height, fixed, second = data[1:9]
    proportional = fixed == 0xFF
    tables = (2 if second == 0xFF else 1) if proportional else 0
    at = _HEADER + tables * _TABLE
    end = at + (last - first + 1) * height * row_bytes
    colors = bytearray()
    if depth == 8 and last >= first and len(data) >= end + _COLOR_BITS:
        # which of the 256 colors the font brings, and then each of them
        numbers = [number for number in range(256) if data[end + number // 8] >> (number % 8) & 1]
        for index, number in enumerate(numbers):
            colors += (
                bytes([number]) + data[end + _COLOR_BITS + 3 * index : end + _COLOR_BITS + 3 * index + 3]
            )
        if end + _COLOR_BITS + 3 * len(numbers) != len(data):
            raise ValueError("not a font file: its colors don't end where the file does")
    elif last < first or end != len(data):
        raise ValueError("not a font file: its letters don't end where the file does")
    return Font(
        height,
        row_bytes,
        first,
        last,
        gap,
        None if proportional else fixed,
        data[_HEADER : _HEADER + _TABLE] if proportional else b"",
        data[_HEADER + _TABLE : at] if tables == 2 else b"",
        data[at:end],
        depth,
        bytes(colors),
    )

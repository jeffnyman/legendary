"""The game's fonts: its .FNT files, from Spellcasting 301 on.

A file is ten bytes and then the letters:

    0  two bytes, both 1
    2  how many bytes a row of a letter is
    3  the first character it has, and at 4 the last: 21 and 7F, `!` to DEL
    5  for a font whose letters differ in width, the gap left between letters; else 0
    6  how many rows a letter is
    7  for a font whose letters are all one width, the width, a word; else FFFF
    9  zero

A font whose letters differ in width then has two tables of 128 bytes, one for each
character's code from 0: its width in pixels, and a byte whose two halves are small numbers,
not yet understood. A font of one width has neither.

Then each character's rows, a bit a pixel from each byte's top bit, the first character
first.
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
        return Picture(width, height, bytes(pixels), None, [])

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
        return Picture(width, self.height + 2, bytes(pixels), None, [])


def read_font(data: bytes) -> Font:
    if len(data) < _HEADER or data[0] != 1 or data[1] != 1:
        raise ValueError("not a font file: it doesn't start as one does")
    row_bytes, first, last, gap, height = data[2:7]
    fixed = int.from_bytes(data[7:9], "little")
    proportional = fixed == 0xFFFF
    at = _HEADER + (2 * _TABLE if proportional else 0)
    size = (last - first + 1) * height * row_bytes
    if last < first or at + size != len(data):
        raise ValueError("not a font file: its letters don't end where the file does")
    return Font(
        height,
        row_bytes,
        first,
        last,
        gap,
        None if proportional else fixed,
        data[_HEADER : _HEADER + _TABLE] if proportional else b"",
        data[_HEADER + _TABLE : at] if proportional else b"",
        data[at:],
    )

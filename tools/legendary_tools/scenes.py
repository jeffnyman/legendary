"""Mission Critical's filmed scenes: the .Q files on its CDs, Legend's own moving pictures.

A file starts with 26 bytes:

    0   39h 68h
    2   a version, two bytes: 4.1 to 5.3 here
    4   the picture's width and height, counted in blocks: 160 by 72
    8   a block's width and height: 4 by 4, so 640 by 288, the game's letter-box
    10  how many frames
    16  how many frames a second: 10

Then chunks to the file's end, each a word for its kind and a long for its size:

    0     sound, a frame's worth: 4,410 bytes
    1     the 256 colors, red, green and blue from 0 to 63
    2     a frame
    5     a table of patterns, two bytes each, for the frames after it
    6, 7  marks with nothing after them, whatever their size says: 6 keeps the picture
          that is there for the frames to draw over, and 7 changes what a run is, as below
    8     the 44 bytes that start a WAVE file, which say what the sound is
    9     a frame that is whole
    10    nothing: it fills the file out so that every frame takes as long to read from a CD
    11    a frame of changes
    FFFFh the end

The sound is the chunks of kind 0 one after another: 16 bits, one channel, 22,050 a second.

A frame is its blocks from the top left, a row at a time, each drawn over the frame before.
A block starts with a byte. Below F8h it is a color, and the next byte is a second color:

- the same color twice is a block of that color;
- else a third byte follows, a pattern. With its top bit clear it is the number of a pattern
  in the table; with it set, it and a fourth byte are the pattern. A pattern is two bytes,
  for the lower two rows and then the upper two, a bit a pixel from the top bit, the first
  color where the bit is clear and the second where it's set.

From F8h the byte is a code:

    F8  over the kept picture: F8 again is its block here; FB and sixteen bytes is a
        block of pixels in which F8 lets the kept picture show; any other byte is a color
        and two bytes of pattern, the kept picture showing where a bit is set
    F9  and a count: again, that many times, what was just done
    FA  and a word: the block with that number, as it is now
    FB  and a count: a run of that many blocks, of the kind just done
    FC  and a byte: the block some way off, by a table of 256 ways in the program
    FD  and a byte: the block that many and one more after this one
    FE  and a byte: the block that many and one more before
    FF  nothing: the block stays as it was

Blocks after this one still hold the frame before, so FD copies from that, and FE from this
frame. What F9 and FB do depends on the block before them. After a copy, F9 is more blocks
copied the same way and FB is that many more, each with a byte, or a word after FA, saying
from where. After a block of one color FB is that many colors; after one of two colors it is
that many patterns in the same colors. Otherwise F9 is the block before again, or after FF
more blocks left alone, and FB is whole blocks, sixteen bytes each. A frame of changes, and
any frame after a mark of kind 7, has FB as whole blocks always.

The kept picture is what the game had on the screen when the scene started, a room's from a
.PIC file, and isn't in the scene's file: here it is black.

A whole block that starts with a byte from F9h to FEh isn't sixteen pixels but a block of
three to eight colors: the bits that say which color each pixel is, two each in four bytes
for three or four colors and three each in six bytes for more, and then the colors.
"""

from __future__ import annotations

import re
import struct
import zlib
from collections.abc import Iterator

from .le import LeExecutable

_HEADER = 26
SOUND, COLORS, FRAME, PATTERNS, KEEP, WHOLE_RUNS, WAVE, WHOLE_FRAME, FILL, CHANGES, END = (
    0,
    1,
    2,
    5,
    6,
    7,
    8,
    9,
    10,
    11,
    0xFFFF,
)

# mov bl,[edx+1] / mov ecx,[ebx*4+ways] / lea esi,[ecx+edi]: a block copied from some way off
_WAYS = re.compile(rb"\x8a\x5a\x01\x8b\x0c\x9d(....)\x8d\x34\x39", re.DOTALL)

# what the block before was, for F9 and FB
_START, _SKIPPED, _SOLID, _PATTERN, _TABLED, _KEPT, _NUMBERED, _WAY, _AHEAD, _BEHIND = range(10)


def read_ways(program: LeExecutable) -> tuple[int, ...]:
    """The program's table of where FC copies a block from: 256 distances in a picture 640 across."""
    code = next(each for each in program.objects if each.is_code)
    found = _WAYS.search(program.contents(code))
    if not found:
        raise ValueError("the program's table for the filmed scenes wasn't found")
    return struct.unpack("<256i", program.read(struct.unpack("<I", found[1])[0], 1024))


class Scene:
    """A .Q file: what it says of itself, its sound, and its frames."""

    def __init__(self, data: bytes, ways: tuple[int, ...]):
        if len(data) < _HEADER or data[:2] != b"\x39\x68":
            raise ValueError("not a filmed scene: it doesn't start as one does")
        self.data = data
        self.ways = ways
        self.version = (data[2], data[3])
        self.across, self.down, self.frames = struct.unpack_from("<HHxxH", data, 4)
        self.block = (data[8], data[9])
        self.rate = data[16] or 10
        self.width, self.height = self.across * data[8], self.down * data[9]
        self.chunks: list[tuple[int, int, int]] = []  # a kind, where its bytes start, and how many
        at = _HEADER
        while at + 6 <= len(data):
            kind, size = struct.unpack_from("<HI", data, at)
            if kind in (KEEP, WHOLE_RUNS):
                size = 0
            self.chunks.append((kind, at + 6, size))
            at += 6 + size
            if kind == END:
                break
        if not self.chunks or self.chunks[-1][0] != END:
            raise ValueError("not a filmed scene: its chunks don't come to an end")
        if self.block != (4, 4):
            raise ValueError("a filmed scene with blocks of a size that isn't read")

    @property
    def pictures(self) -> int:
        """How many frames the file has, counted."""
        return sum(1 for kind, _, _ in self.chunks if kind in (FRAME, WHOLE_FRAME, CHANGES))

    @property
    def seconds(self) -> float:
        return self.pictures / self.rate

    def sound(self) -> bytes | None:
        """The scene's sound as a WAV file's bytes; None for a scene without any."""
        start = next((self.data[at : at + size] for kind, at, size in self.chunks if kind == WAVE), b"")
        samples = b"".join(self.data[at : at + size] for kind, at, size in self.chunks if kind == SOUND)
        if len(start) != 44 or not samples:
            return None
        return (
            start[:4]
            + struct.pack("<I", 36 + len(samples))
            + start[8:40]
            + struct.pack("<I", len(samples))
            + samples
        )

    def play(self) -> Iterator[tuple[bytes, bytes]]:
        """Each frame in turn: its pixels, a byte each, and the 256 colors it's shown in, three bytes each."""
        picture = bytearray(self.width * self.height)
        kept: bytes | None = None
        patterns = bytes(512)
        colors = bytes(768)
        whole_runs = False
        for kind, at, size in self.chunks:
            if kind == COLORS:
                colors = bytes(value * 255 // 63 for value in self.data[at : at + 768].ljust(768, b"\0"))
            elif kind == PATTERNS:
                patterns = self.data[at : at + size].ljust(512, b"\0")
            elif kind == KEEP:
                kept = bytes(picture)
            elif kind == WHOLE_RUNS:
                whole_runs = True
            elif kind in (FRAME, WHOLE_FRAME, CHANGES):
                used = _Frame(self, picture, kept, patterns, whole_runs or kind == CHANGES).draw(at)
                # one frame in the game stops two blocks short, and the program draws them
                # from what comes next in the file, as this does: a little over is let be
                if not size <= used <= size + 32:
                    raise ValueError(f"a frame of {size} bytes was drawn from {used}")
                yield bytes(picture), colors

    def apng(self, path: str, limit: int | None = None) -> int:
        """Writes the scene's frames as an animated PNG, a frame at a time; how many were written."""

        def chunk(kind: bytes, body: bytes) -> bytes:
            return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

        count = min(self.pictures, limit or self.pictures)
        sequence = 0
        with open(path, "wb") as file:
            file.write(b"\x89PNG\r\n\x1a\n")
            file.write(chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 2, 0, 0, 0)))
            file.write(chunk(b"acTL", struct.pack(">II", count, 0)))
            for number, (pixels, colors) in enumerate(self.play()):
                if number >= count:
                    break
                table = [colors[3 * index : 3 * index + 3] for index in range(256)]
                lines = b"".join(
                    b"\0" + b"".join(table[pixel] for pixel in pixels[row : row + self.width])
                    for row in range(0, len(pixels), self.width)
                )
                control = struct.pack(
                    ">IIIIIHHBB", sequence, self.width, self.height, 0, 0, 1, self.rate, 0, 0
                )
                file.write(chunk(b"fcTL", control))
                sequence += 1
                if number == 0:
                    file.write(chunk(b"IDAT", zlib.compress(lines, 6)))
                else:
                    file.write(chunk(b"fdAT", struct.pack(">I", sequence) + zlib.compress(lines, 6)))
                    sequence += 1
            file.write(chunk(b"IEND", b""))
        return count


class _Frame:
    """A frame being drawn over the picture."""

    def __init__(
        self, scene: Scene, picture: bytearray, kept: bytes | None, patterns: bytes, whole_runs: bool
    ):
        self.data = scene.data
        self.ways = scene.ways
        self.across = scene.across
        self.count = scene.across * scene.down
        self.width = scene.width
        self.picture = picture
        self.kept = kept
        self.patterns = patterns
        self.whole_runs = whole_runs

    def _place(self, block: int) -> int:
        return (block // self.across) * 4 * self.width + (block % self.across) * 4

    def _copy(self, block: int, source: int, picture: bytes | bytearray | None = None) -> None:
        """A block from a place in the picture, or in another."""
        picture = self.picture if picture is None else picture
        if not 0 <= source <= len(picture) - 3 * self.width - 4:
            raise ValueError("a frame copies a block from outside the picture")
        to = self._place(block)
        for row in range(0, 4 * self.width, self.width):
            self.picture[to + row : to + row + 4] = picture[source + row : source + row + 4]

    def _put(self, block: int, rows: list[bytes]) -> None:
        to = self._place(block)
        for index, row in enumerate(rows):
            self.picture[to + index * self.width : to + index * self.width + 4] = row

    @staticmethod
    def _masks(lower: int, upper: int) -> list[int]:
        """A pattern's four rows, top first: a bit a pixel, the leftmost the highest."""
        return [upper >> 4, upper & 15, lower >> 4, lower & 15]

    def _two(self, block: int, first: int, second: int, lower: int, upper: int) -> None:
        self._put(
            block,
            [
                bytes(second if mask >> (3 - pixel) & 1 else first for pixel in range(4))
                for mask in self._masks(lower, upper)
            ],
        )

    def _tabled(self, number: int) -> tuple[int, int]:
        return self.patterns[2 * number], self.patterns[2 * number + 1]

    def _whole(self, block: int, at: int) -> int:
        """A whole block, or one of three to eight colors; how many bytes it was."""
        data = self.data
        code = data[at]
        if code < 0xF8:
            self._put(block, [data[at + 4 * row : at + 4 * row + 4] for row in range(4)])
            return 16
        if code in (0xF8, 0xFF):
            raise ValueError("a frame has a whole block of a kind that isn't read")
        many = code - 0xF6
        bits, size = (2, 4) if many <= 4 else (3, 6)
        which = int.from_bytes(data[at + 1 : at + 1 + size], "little")
        colors = data[at + 1 + size : at + 1 + size + many].ljust(8, b"\0")
        self._put(
            block,
            [
                bytes(colors[which >> (bits * (4 * row + pixel)) & ((1 << bits) - 1)] for pixel in range(4))
                for row in range(4)
            ],
        )
        return 1 + size + many

    def _over_kept(self, block: int, at: int) -> tuple[int, int]:
        """A block drawn over the kept picture: how many bytes it was, and what it was."""
        if self.kept is None:
            raise ValueError("a frame draws over a kept picture, and none was kept")
        data, place = self.data, self._place(block)
        under = [self.kept[place + row * self.width : place + row * self.width + 4] for row in range(4)]
        second = data[at + 1]
        if second == 0xF8:
            self._put(block, under)
            return 2, _KEPT
        if second == 0xFB:
            pixels = data[at + 2 : at + 18]
            self._put(
                block,
                [
                    bytes(
                        under[row][pixel] if pixels[4 * row + pixel] == 0xF8 else pixels[4 * row + pixel]
                        for pixel in range(4)
                    )
                    for row in range(4)
                ],
            )
            return 18, _START
        masks = self._masks(data[at + 2], data[at + 3])
        self._put(
            block,
            [
                bytes(under[row][pixel] if masks[row] >> (3 - pixel) & 1 else second for pixel in range(4))
                for row in range(4)
            ],
        )
        return 4, _START

    def draw(self, start: int) -> int:  # noqa: PLR0912, PLR0915
        """Draws the frame that starts at a place in the file; how many bytes it was."""
        data, at, block, before = self.data, start, 0, _START
        from_where = 0  # what the last copy was told: a way, or how far ahead or behind
        first = second = 0  # the last two colors
        while block < self.count:
            code = data[at]
            if code < 0xF8:
                first, second = code, data[at + 1]
                if first == second:
                    self._put(block, [bytes([first]) * 4] * 4)
                    at, before = at + 2, _SOLID
                elif data[at + 2] & 0x80:
                    self._two(block, first, second, data[at + 2], data[at + 3])
                    at, before = at + 4, _PATTERN
                else:
                    self._two(block, first, second, *self._tabled(data[at + 2]))
                    at, before = at + 3, _TABLED
                block += 1
            elif code == 0xF8:
                size, before = self._over_kept(block, at)
                at += size
                block += 1
            elif code == 0xF9:
                for _ in range(data[at + 1]):
                    if before == _SKIPPED:
                        pass
                    elif before == _BEHIND:
                        self._copy(block, self._place(block - from_where - 1))
                    elif before == _AHEAD:
                        self._copy(block, self._place(block + from_where + 1))
                    elif before == _WAY:
                        self._copy(block, self._place(block) + self.ways[from_where])
                    elif before == _KEPT:
                        self._copy(block, self._place(block), self.kept)
                    else:
                        self._copy(block, self._place(block - 1))
                    block += 1
                at, before = at + 2, _START
            elif code == 0xFA:
                self._copy(block, self._place(struct.unpack_from("<H", data, at + 1)[0]))
                at, block, before = at + 3, block + 1, _NUMBERED
            elif code == 0xFB:
                count, at = data[at + 1], at + 2
                kind = _START if self.whole_runs else before
                for _ in range(count):
                    if kind == _BEHIND:
                        self._copy(block, self._place(block - data[at] - 1))
                        at += 1
                    elif kind == _AHEAD:
                        self._copy(block, self._place(block + data[at] + 1))
                        at += 1
                    elif kind == _WAY:
                        self._copy(block, self._place(block) + self.ways[data[at]])
                        at += 1
                    elif kind == _NUMBERED:
                        self._copy(block, self._place(struct.unpack_from("<H", data, at)[0]))
                        at += 2
                    elif kind == _SOLID:
                        self._put(block, [bytes([data[at]]) * 4] * 4)
                        at += 1
                    elif kind == _PATTERN or (kind == _TABLED and data[at] & 0x80):
                        self._two(block, first, second, data[at], data[at + 1])
                        at += 2
                    elif kind == _TABLED:
                        self._two(block, first, second, *self._tabled(data[at]))
                        at += 1
                    else:
                        at += self._whole(block, at)
                    block += 1
                before = _START
            elif code == 0xFF:
                at, block, before = at + 1, block + 1, _SKIPPED
            else:
                from_where = data[at + 1]
                if code == 0xFC:
                    self._copy(block, self._place(block) + self.ways[from_where])
                    before = _WAY
                elif code == 0xFD:
                    self._copy(block, self._place(block + from_where + 1))
                    before = _AHEAD
                else:
                    self._copy(block, self._place(block - from_where - 1))
                    before = _BEHIND
                at, block = at + 2, block + 1
        return at - start

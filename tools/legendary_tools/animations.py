"""The game's animations: Autodesk Animator's .FLI files, which Gateway II's CD has.

Gateway II's moving pictures, its title and the scenes between its parts, are files named
.PIC beside its pictures, and are nothing of Legend's: they are .FLI files as Autodesk
Animator wrote them, 320 by 200 in 256 colors. A file starts with 128 bytes:

    0   the file's size, four bytes
    4   AF11
    6   how many frames
    8   the width, and at 10 the height
    12  8, the bits a pixel
    16  how long a frame is shown, in seventieths of a second

Then the frames. A frame is its size, four bytes, F1FA, how many chunks, eight bytes more,
and the chunks, each its size, four bytes, a kind, and what the kind says:

    11  colors: packets of how many colors to skip, how many to set (0 for 256), and
        three bytes each, red, green and blue from 0 to 63
    12  what changed: the first line that did and how many lines; for each, a count of
        packets, each a count of pixels to skip and then a count: above zero, that many
        bytes as they are; below, the next byte that many times
    13  all black
    15  the whole picture, a line at a time: a byte that isn't used, then packets to the
        line's end: a count above zero is the next byte that many times, one below is that
        many bytes as they are
    16  the whole picture, as it is

The frames are given as they are shown, each the one before with its changes made.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass

_HEADER = 128
_FLI = 0xAF11
_FRAME = 0xF1FA
_COLORS, _CHANGES, _BLACK, _WHOLE, _RAW = 11, 12, 13, 15, 16


def is_animation(data: bytes) -> bool:
    """Whether a file is a .FLI, whatever it is called."""
    return len(data) >= _HEADER and struct.unpack_from("<H", data, 4)[0] == _FLI


@dataclass(frozen=True)
class Animation:
    """An animation: its size, how long a frame is shown, and each frame in red, green and blue."""

    width: int
    height: int
    seventieths: int
    frames: list[bytes]

    @property
    def seconds(self) -> float:
        return len(self.frames) * self.seventieths / 70

    def apng(self) -> bytes:
        """The animation as an animated PNG, which a browser plays."""

        def chunk(kind: bytes, body: bytes) -> bytes:
            return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

        def packed(frame: bytes) -> bytes:
            row = 3 * self.width
            lines = b"".join(b"\0" + frame[at : at + row] for at in range(0, len(frame), row))
            return zlib.compress(lines, 6)

        header = struct.pack(">IIBBBBB", self.width, self.height, 8, 2, 0, 0, 0)
        out = [b"\x89PNG\r\n\x1a\n", chunk(b"IHDR", header)]
        out.append(chunk(b"acTL", struct.pack(">II", len(self.frames), 0)))
        sequence = 0
        for number, frame in enumerate(self.frames):
            control = struct.pack(
                ">IIIIIHHBB", sequence, self.width, self.height, 0, 0, max(self.seventieths, 1), 70, 0, 0
            )
            out.append(chunk(b"fcTL", control))
            sequence += 1
            if number == 0:
                out.append(chunk(b"IDAT", packed(frame)))
            else:
                out.append(chunk(b"fdAT", struct.pack(">I", sequence) + packed(frame)))
                sequence += 1
        out.append(chunk(b"IEND", b""))
        return b"".join(out)


def _signed(byte: int) -> int:
    return byte - 256 if byte > 127 else byte


class _Screen:
    """The picture as it stands, and its colors, which each frame's chunks change."""

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height
        self.pixels = bytearray(width * height)
        self.palette = bytearray(768)

    def frame(self) -> bytes:
        table = [bytes(self.palette[3 * color : 3 * color + 3]) for color in range(256)]
        return b"".join(table[pixel] for pixel in self.pixels)

    def colors(self, data: bytes, at: int) -> None:
        (packets,) = struct.unpack_from("<H", data, at)
        at += 2
        color = 0
        for _ in range(packets):
            color += data[at]
            many = data[at + 1] or 256
            values = data[at + 2 : at + 2 + 3 * many]
            self.palette[3 * color : 3 * color + len(values)] = bytes(value * 255 // 63 for value in values)
            color += many
            at += 2 + 3 * many

    def whole(self, data: bytes, at: int) -> None:
        for line in range(self.height):
            at += 1
            column = 0
            while column < self.width:
                many = _signed(data[at])
                at += 1
                place = line * self.width + column
                if many > 0:
                    self.pixels[place : place + many] = bytes([data[at]]) * many
                    at += 1
                else:
                    many = -many
                    self.pixels[place : place + many] = data[at : at + many]
                    at += many
                column += many

    def changes(self, data: bytes, at: int) -> None:
        first, lines = struct.unpack_from("<HH", data, at)
        at += 4
        for line in range(first, first + lines):
            packets = data[at]
            at += 1
            column = 0
            for _ in range(packets):
                column += data[at]
                many = _signed(data[at + 1])
                at += 2
                place = line * self.width + column
                if many > 0:
                    self.pixels[place : place + many] = data[at : at + many]
                    at += many
                else:
                    many = -many
                    self.pixels[place : place + many] = bytes([data[at]]) * many
                    at += 1
                column += many

    def chunk(self, data: bytes, at: int, kind: int) -> None:
        if kind == _COLORS:
            self.colors(data, at)
        elif kind == _CHANGES:
            self.changes(data, at)
        elif kind == _WHOLE:
            self.whole(data, at)
        elif kind == _BLACK:
            self.pixels[:] = bytes(len(self.pixels))
        elif kind == _RAW:
            self.pixels[:] = data[at : at + len(self.pixels)]
        else:
            raise ValueError(f"an animation's chunk of kind {kind} isn't read")


def read_animation(data: bytes) -> Animation:
    if not is_animation(data):
        raise ValueError("not an animation: it doesn't start as a .FLI does")
    count, width, height, depth = struct.unpack_from("<HHHH", data, 6)
    (speed,) = struct.unpack_from("<H", data, 16)
    if depth != 8:
        raise ValueError(f"an animation of {depth} bits a pixel isn't read")
    screen = _Screen(width, height)
    frames = []
    at = _HEADER
    for _ in range(count):
        if at + 16 > len(data):
            break
        size, magic, chunks = struct.unpack_from("<IHH", data, at)
        if magic != _FRAME:
            raise ValueError(f"an animation's frame at {at:#x} doesn't start as one does")
        inside = at + 16
        for _ in range(chunks):
            chunk_size, kind = struct.unpack_from("<IH", data, inside)
            screen.chunk(data, inside + 6, kind)
            inside += chunk_size
        frames.append(screen.frame())
        at += size
    return Animation(width, height, speed, frames)

"""Mission Critical's battle game: the ships it draws, and the stars of its map.

A ship's file, BATTLE.DAT or FRIGATE.DAT or one of twelve more, is the ship as lines in
space, twice: once in full, and once with fewer lines, for when it is far off. It starts
with four words, how many points and how many lines the first has, and how many the second.
Then the first's points, three words each, across, down and deep, signed, with the ship's
middle at 0; its lines, two words each, the numbers of the two points a line joins; and the
second's points and lines the same way. That is the whole file.

FTLSTARS.DAT is the stars a ship can go to, a hundred bytes each:

    0   the star's name, and a zero
    24  its right ascension, as text: 14h 48m 57s
    36  its declination, as text, with @ for the degree's sign: -61@ 22' 30"
    52  three floats: those two in degrees, and how far it is, in light years
    64  what kind of star it is, as astronomers write it: G2 V
    72  three floats: how far again, and the declination and the right ascension in radians
    84  a long, by the kind of star: 176 for G, 272 for M
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

from .pictures import Picture


@dataclass(frozen=True)
class Model:
    """A ship as lines: its points in space, and the pairs of them a line joins."""

    points: list[tuple[int, int, int]]
    lines: list[tuple[int, int]]

    def extent(self) -> list[tuple[int, int]]:
        """How far it reaches each way: least and most, along, across and up."""
        return [(min(axis), max(axis)) for axis in zip(*self.points)]


@dataclass(frozen=True)
class Ship:
    """A ship's file: the ship in full, and with fewer lines."""

    full: Model
    far: Model

    def picture(self, scale: int = 3) -> Picture:
        """Both drawn from three sides each: from above, from the side, and from ahead."""
        spans = [
            max(abs(value) for point in self.full.points for value in point[axis : axis + 1])
            for axis in range(3)
        ]
        across, down, deep = (scale * span + 6 for span in spans)
        views = [(0, 1, across, down), (0, 2, across, deep), (1, 2, down, deep)]
        width = sum(2 * view[2] for view in views)
        row = 2 * max(view[3] for view in views)
        pixels = bytearray(width * 2 * row)
        for index, model in enumerate((self.full, self.far)):
            left = 0
            for first, second, half_wide, _ in views:
                middle_x, middle_y = left + half_wide, index * row + row // 2
                for start, end in model.lines:
                    here, there = model.points[start], model.points[end]
                    _line(
                        pixels,
                        width,
                        (middle_x + scale * here[first], middle_y + scale * here[second]),
                        (middle_x + scale * there[first], middle_y + scale * there[second]),
                    )
                left += 2 * half_wide
        return Picture(width, 2 * row, bytes(pixels), None, [])


def _line(pixels: bytearray, width: int, start: tuple[int, int], end: tuple[int, int]) -> None:
    """A line of pixels from one place to another, by Bresenham's steps."""
    (x, y), (to_x, to_y) = start, end
    step_x, step_y = (1 if to_x >= x else -1), (1 if to_y >= y else -1)
    far_x, far_y = abs(to_x - x), abs(to_y - y)
    error = far_x - far_y
    while True:
        if 0 <= x < width and 0 <= y < len(pixels) // width:
            pixels[y * width + x] = 1
        if (x, y) == (to_x, to_y):
            return
        if 2 * error > -far_y:
            error, x = error - far_y, x + step_x
        elif 2 * error < far_x:
            error, y = error + far_x, y + step_y
        else:
            error, x, y = error - far_y + far_x, x + step_x, y + step_y


def read_ship(data: bytes) -> Ship:
    """A ship's file."""
    if len(data) < 8:
        raise ValueError("not a ship's file: it is too short")
    points, lines, far_points, far_lines = struct.unpack_from("<4H", data, 0)
    if 8 + 6 * (points + far_points) + 4 * (lines + far_lines) != len(data):
        raise ValueError("not a ship's file: its points and lines don't end where the file does")

    def model(at: int, how_many: int, joined: int) -> Model:
        found = [struct.unpack_from("<3h", data, at + 6 * index) for index in range(how_many)]
        pairs = [struct.unpack_from("<2H", data, at + 6 * how_many + 4 * index) for index in range(joined)]
        if any(point >= how_many for pair in pairs for point in pair):
            raise ValueError("not a ship's file: a line joins a point it hasn't got")
        return Model(found, pairs)

    return Ship(model(8, points, lines), model(8 + 6 * points + 4 * lines, far_points, far_lines))


@dataclass(frozen=True)
class Star:
    name: str
    right_ascension: str
    declination: str
    degrees: tuple[float, float]  # the two as numbers
    light_years: float
    kind: str


def read_stars(data: bytes) -> list[Star]:
    """The stars of FTLSTARS.DAT."""
    if not data or len(data) % 100:
        raise ValueError("not a file of stars: it isn't a hundred bytes a star")

    def text(record: bytes, at: int, size: int) -> str:
        return record[at : at + size].split(b"\0")[0].decode("latin-1").strip()

    stars = []
    for at in range(0, len(data), 100):
        record = data[at : at + 100]
        ascension, declination, far = struct.unpack_from("<3f", record, 52)
        stars.append(
            Star(
                text(record, 0, 24),
                text(record, 24, 12),
                text(record, 36, 16).replace("@", "°"),
                (ascension, declination),
                far,
                text(record, 64, 8),
            )
        )
    return stars

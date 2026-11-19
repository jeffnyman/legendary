"""The regions of the game's pictures: its .RGN files, which say what is where in a picture.

A region file goes with the picture files of its number: S101_R1.RGN with S101_E1.PIC and
S101_C1.PIC. It starts with a directory of six bytes for each of their pictures, to where the
first picture's regions start:

    0  where the picture's regions start in the file, or 0 when it has none
    2  an object's number, or 0: by its use, what the rest of the picture is
    4  how many regions it has

A region is six bytes: an object's number, and a rectangle in the picture as four bytes, its
left, top, right and bottom. Across is counted in pairs of pixels, so that a picture 379 wide
fits a byte; down is in the EGA's rows.

Gateway II and Companions of Xanth keep a region another way: an object's number, a count
of points, and the points, each two words, across and down in pixels. Two points are a
rectangle's corners, and more are the corners of an outline, so that a region can be the
shape of the thing.

The regions are what the mouse points at. In a room's picture they are its things, and in a
map they are its rooms.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass


@dataclass(frozen=True)
class Region:
    """A rectangle of a picture and the object that's there."""

    object: int
    left: int  # in pairs of pixels
    top: int
    right: int
    bottom: int
    # in the later games' form: its corners, in pixels; two for a rectangle, more for an outline
    points: tuple[tuple[int, int], ...] = ()


@dataclass(frozen=True)
class PictureRegions:
    """A picture's regions."""

    picture: int  # its number in the picture files
    object: int  # the object the rest of the picture is, or 0
    regions: list[Region]


def read_regions(data: bytes) -> list[PictureRegions]:
    """The pictures that have regions, in the directory's order."""
    # the directory ends where the first regions start
    end = len(data)
    entries = []
    at = 0
    while at + 6 <= end:
        offset, shown, count = struct.unpack_from("<HHH", data, at)
        if offset:
            end = min(end, offset)
        entries.append((offset, shown, count))
        at += 6
    outlined = _outlined(data, entries)
    if outlined is not None:
        return outlined
    found = []
    for picture, (offset, shown, count) in enumerate(entries):
        if not offset:
            continue
        if offset + 6 * count > len(data):
            raise ValueError(f"picture {picture}'s regions go past the end of the file")
        regions = [Region(*struct.unpack_from("<HBBBB", data, offset + 6 * index)) for index in range(count)]
        found.append(PictureRegions(picture, shown, regions))
    return found


def _outlined(data: bytes, entries: list[tuple[int, int, int]]) -> list[PictureRegions] | None:
    """
    The same, read in the later games' form, or None when the file isn't in it: it is when
    every picture's regions, read that way, end where the next picture's start.
    """
    starts = sorted({offset for offset, _, _ in entries if offset} | {len(data)})
    found = []
    for picture, (offset, shown, count) in enumerate(entries):
        if not offset:
            continue
        at = offset
        regions = []
        for _ in range(count):
            if at + 4 > len(data):
                return None
            thing, corners = struct.unpack_from("<HH", data, at)
            if not 2 <= corners <= 512 or at + 4 + 4 * corners > len(data):
                return None
            points = tuple(struct.iter_unpack("<HH", data[at + 4 : at + 4 + 4 * corners]))
            across, down = [x for x, _ in points], [y for _, y in points]
            regions.append(Region(thing, min(across) // 2, min(down), max(across) // 2, max(down), points))
            at += 4 + 4 * corners
        # a picture with no regions of its own may share a start with the one after it
        if count and at not in starts:
            return None
        found.append(PictureRegions(picture, shown, regions))
    return found

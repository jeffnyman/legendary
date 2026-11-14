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
    found = []
    for picture, (offset, shown, count) in enumerate(entries):
        if not offset:
            continue
        if offset + 6 * count > len(data):
            raise ValueError(f"picture {picture}'s regions go past the end of the file")
        regions = [Region(*struct.unpack_from("<HBBBB", data, offset + 6 * index)) for index in range(count)]
        found.append(PictureRegions(picture, shown, regions))
    return found

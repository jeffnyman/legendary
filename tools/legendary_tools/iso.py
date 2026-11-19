"""A CD's image, read for its files: ISO 9660, as the collections of the games came.

A game from a CD keeps on its hard disk only what it must, and reads the rest from the
disc: Gateway II's pictures are there and nowhere else. This reads a disc's image far
enough to list a folder and take a file out of it, and no further: plain ISO 9660, with
the names as the disc has them, in capitals and with a version after a semicolon. The image
is the disc's data alone, 2,048 bytes a sector, or a raw one, 2,352 bytes a sector.

A disc is sectors of 2,048 bytes. Sector 16 is the volume's descriptor, `CD001`, with the
record of the root folder at 156. A folder is records one after another, none across a
sector's end:

    0   the record's length; 0 for no more in this sector
    2   where the file or folder starts, a sector's number, four bytes
    10  its size, four bytes
    25  flags: 2 for a folder
    32  its name's length, and at 33 the name
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

_SECTOR = 2048
# a raw image keeps each sector as the disc has it: 2,352 bytes, with the 2,048 of a mode 1
# sector 16 bytes in, after its sync and header and before its error correction
_RAW_SECTOR, _RAW_DATA = 2352, 16


@dataclass(frozen=True)
class Entry:
    """A file or a folder of a disc."""

    name: str  # as the disc has it, without the version
    sector: int
    size: int
    folder: bool


class Disc:
    """A disc's image, read a piece at a time: it is hundreds of megabytes."""

    def __init__(self, path: str):
        self.path = path
        self._raw = False
        volume = self._read(16 * _SECTOR, _SECTOR)
        if volume[1:6] != b"CD001":
            # or a raw image, as a .img or .bin with a .cue beside it is
            self._raw = True
            volume = self._read(16 * _SECTOR, _SECTOR)
        if volume[1:6] != b"CD001":
            raise ValueError(f"{path} isn't an ISO 9660 image")
        sector, size = struct.unpack_from("<I4xI", volume, 156 + 2)
        self.root = Entry("", sector, size, True)

    def _read(self, at: int, size: int) -> bytes:
        """Bytes of the disc's data, from a place counted in its 2,048-byte sectors."""
        with open(self.path, "rb") as file:
            if not self._raw:
                file.seek(at)
                return file.read(size)
            found = bytearray()
            while len(found) < size:
                sector, inside = divmod(at + len(found), _SECTOR)
                file.seek(sector * _RAW_SECTOR + _RAW_DATA + inside)
                found += file.read(min(_SECTOR - inside, size - len(found)))
            return bytes(found)

    def entries(self, folder: Entry) -> list[Entry]:
        """What a folder holds, in the disc's order."""
        data = self._read(folder.sector * _SECTOR, folder.size)
        found = []
        at = 0
        while at < len(data):
            length = data[at]
            if length == 0:
                # the rest of the sector is empty: a record doesn't cross into the next
                at = (at // _SECTOR + 1) * _SECTOR
                continue
            sector, size = struct.unpack_from("<I4xI", data, at + 2)
            name = data[at + 33 : at + 33 + data[at + 32]]
            # the first two are the folder itself and the one above it
            if name not in (b"\0", b"\1"):
                text = name.decode("latin-1").split(";")[0].rstrip(".")
                found.append(Entry(text, sector, size, bool(data[at + 25] & 2)))
            at += length
        return found

    def find(self, path: str) -> Entry | None:
        """A file or folder by its path from the root, whatever its case."""
        entry = self.root
        for part in [part for part in path.replace("\\", "/").split("/") if part]:
            if not entry.folder:
                return None
            entry = next((e for e in self.entries(entry) if e.name.upper() == part.upper()), None)
            if entry is None:
                return None
        return entry

    def read(self, entry: Entry) -> bytes:
        return self._read(entry.sector * _SECTOR, entry.size)

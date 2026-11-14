"""A program linked by PLINK86 (Phoenix Software Associates), with its overlays.

The MZ header describes only the root: the code that is always in memory, with PLINK86's
overlay loader in it. The program starts in the loader, which has a table of the overlays. Each
has a place in memory it loads at, and overlays that share a place take each other's. The
program's data is an overlay too, the last, kept in the .EXE after the root and loaded as the
program starts.

A call into an overlay goes by a thunk in the table's segment, after the table:

    call far <the loader>      9A oo oo ss ss
    dw <the overlay's number>
    jmp far <the routine>      EA oo oo ss ss

The loader reads the number after the call, loads that overlay if it isn't in memory, and
returns past the number to the jump.

Every segment here is relative to where the program loads, as the file has them.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

from .mz import MzExecutable, Relocation

# how the program begins: cli / mov di,es / mov si,ss / mov cx,sp / mov dx,<the table's segment>
_ENTRY = bytes.fromhex("fa8cc78cd68bccba")

PRELOAD = 0x4000  # loaded as the program starts
IN_EXE = 0x2000  # in the .EXE, not the .OVL (inferred: the data is the one overlay with it)
END = 0xFFFF


@dataclass(frozen=True)
class Overlay:
    """An entry in the loader's table."""

    number: int  # from 1, as the thunks have it
    status: int
    relocation_count: int
    load_segment: int
    end_segment: int  # the first paragraph after it in memory
    attributes: int
    file_paragraph: int  # where its relocations start in its file, in paragraphs
    size_paragraphs: int  # of what the file holds; memory to end_segment beyond that is zero

    @property
    def preloaded(self) -> bool:
        return bool(self.status & PRELOAD)

    @property
    def in_exe(self) -> bool:
        return bool(self.status & IN_EXE)

    @property
    def contents_offset(self) -> int:
        """Where its contents start in its file: after the relocations, on a paragraph."""
        return self.file_paragraph * 16 + (self.relocation_count * 4 + 15) // 16 * 16


@dataclass(frozen=True)
class Thunk:
    """A way into an overlay: the root calls here, and the loader goes on to the routine."""

    segment: int
    offset: int
    overlay: int
    target_segment: int
    target_offset: int


class Plink86Program:
    """The root, the loader's table, and the overlays read from the two files."""

    def __init__(self, exe: bytes, ovl: bytes | None = None):
        self.mz = MzExecutable(exe)
        self._ovl = ovl
        root = self.mz.image
        entry = self.mz.cs * 16 + self.mz.ip
        if root[entry : entry + len(_ENTRY)] != _ENTRY:
            raise ValueError("not a PLINK86 program: it doesn't start in PLINK86's loader")
        self.loader_segment = self.mz.cs
        self.table_segment = struct.unpack_from("<H", root, entry + len(_ENTRY))[0]
        table = self.table_segment * 16

        # the table's first words: the loader's segment, and where the program's memory ends
        self.memory_end_segment = struct.unpack_from("<H", root, table + 4)[0]

        self.overlays: list[Overlay] = []
        at = table
        while True:
            status = struct.unpack_from("<H", root, at + 6)[0]
            if status == END:
                break
            count, load, end, attributes, paragraph, size = struct.unpack_from("<HHHHIH", root, at + 8)
            self.overlays.append(
                Overlay(len(self.overlays) + 1, status, count, load, end, attributes, paragraph, size)
            )
            at += 16

        # after the table, the two files' names, then the thunks
        at += 8
        names = []
        for _ in range(2):
            end = root.index(b"\0", at)
            names.append(root[at:end].decode("ascii"))
            at = end + 1
        self.exe_name, self.ovl_name = names

        self.thunks: list[Thunk] = []
        while root[at] == 0x9A and root[at + 7] == 0xEA:
            number, _, target_offset, target_segment = struct.unpack_from("<HBHH", root, at + 5)
            self.thunks.append(Thunk(self.table_segment, at - table, number, target_segment, target_offset))
            at += 12
        self._thunks = {(thunk.segment, thunk.offset): thunk for thunk in self.thunks}

    def overlay(self, number: int) -> Overlay:
        if not 1 <= number <= len(self.overlays):
            raise ValueError(f"there is no overlay {number}: they go from 1 to {len(self.overlays)}")
        return self.overlays[number - 1]

    def thunk_at(self, segment: int, offset: int) -> Thunk | None:
        return self._thunks.get((segment, offset))

    def _file(self, overlay: Overlay) -> bytes:
        if overlay.in_exe:
            return self.mz.data
        if self._ovl is None:
            raise ValueError(f"overlay {overlay.number} is in {self.ovl_name}, which wasn't given")
        return self._ovl

    def relocations(self, overlay: Overlay) -> list[Relocation]:
        """The places in an overlay that hold segments: offset and segment, as the root's are."""
        data = self._file(overlay)
        start = overlay.file_paragraph * 16
        return [
            Relocation(segment, offset)
            for offset, segment in struct.iter_unpack(
                "<HH", data[start : start + 4 * overlay.relocation_count]
            )
        ]

    def contents(self, overlay: Overlay) -> bytes:
        """An overlay's bytes as its file has them, to go at its load segment."""
        data = self._file(overlay)
        start = overlay.contents_offset
        return data[start : start + overlay.size_paragraphs * 16]

    def areas(self) -> dict[int, list[Overlay]]:
        """The overlays by where they load: those that share a place take each other's."""
        areas: dict[int, list[Overlay]] = {}
        for overlay in self.overlays:
            areas.setdefault(overlay.load_segment, []).append(overlay)
        return areas

    def image(self, *numbers: int) -> Image:
        """
        The program's memory with the root, the overlays loaded as it starts, and the ones
        numbered, each at its place. Segments stay relative to the load segment: nothing is
        added at the relocations.
        """
        memory = bytearray(self.memory_end_segment * 16)
        memory[: len(self.mz.image)] = self.mz.image
        relocations = {relocation.linear for relocation in self.mz.relocations}
        chosen = [overlay for overlay in self.overlays if overlay.preloaded]
        chosen += [self.overlay(number) for number in numbers]
        loaded: dict[int, Overlay] = {}
        for overlay in chosen:
            for other in loaded.values():
                if (
                    other is not overlay
                    and overlay.load_segment < other.end_segment
                    and other.load_segment < overlay.end_segment
                ):
                    raise ValueError(f"overlays {other.number} and {overlay.number} load over each other")
            loaded[overlay.number] = overlay
            contents = self.contents(overlay)
            start = overlay.load_segment * 16
            memory[start : start + len(contents)] = contents
            relocations.update(relocation.linear for relocation in self.relocations(overlay))
        return Image(self, bytes(memory), relocations, loaded)


class Image:
    """The program's memory with some overlays loaded."""

    def __init__(
        self,
        program: Plink86Program,
        memory: bytes,
        relocations: set[int],
        loaded: dict[int, Overlay],
    ):
        self.program = program
        self.memory = memory
        self.relocations = relocations
        self.loaded = loaded

    def word(self, linear: int) -> int:
        return struct.unpack_from("<H", self.memory, linear)[0]

    def segment_references(self) -> dict[int, list[int]]:
        """Each segment the relocations name, with the places that name it."""
        found: dict[int, list[int]] = {}
        for linear in sorted(self.relocations):
            found.setdefault(self.word(linear), []).append(linear)
        return found

    def where(self, linear: int) -> str:
        """What a place in memory belongs to: the root, or an overlay."""
        segment = linear // 16
        for overlay in self.loaded.values():
            if overlay.load_segment <= segment < overlay.end_segment:
                return f"overlay {overlay.number}"
        if linear < len(self.program.mz.image):
            return "root"
        return "not loaded"

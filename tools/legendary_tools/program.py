"""A program with overlays, whichever linker made it: what the tools ask of one.

The games were linked by two linkers. Spellcasting 101's was PLINK86, and from Spellcasting
201 it was .RTLink/Plus. Both keep a root that is always in memory, overlays that take each
other's places, and thunks the root calls to reach an overlay's routines. They differ in how
the files hold them, which is each one's own module's business.

Every segment here is relative to where the program loads, as the file has them.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

from .mz import MzExecutable, Relocation


@dataclass(frozen=True)
class Overlay:
    """An entry in the loader's table of overlays."""

    number: int  # from 1
    status: int  # the loader's own word about it
    relocation_count: int
    load_segment: int
    end_segment: int  # the first paragraph after it in memory
    attributes: int
    file_paragraph: int  # where its relocations start in its file, in paragraphs
    size_paragraphs: int  # of what the file holds; memory to end_segment beyond that is zero
    preloaded: bool = False  # loaded as the program starts
    in_exe: bool = False  # in the .EXE, not the .OVL

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


class Program:
    """The root, the loader's table, and the overlays read from the two files."""

    linker = ""
    mz: MzExecutable
    overlays: list[Overlay]
    thunks: list[Thunk]
    loader_segment: int  # where the code that loads overlays is
    table_segment: int  # where its table of them is
    memory_end_segment: int
    exe_name: str
    ovl_name: str
    _ovl: bytes | None
    _thunks: dict[tuple[int, int], Thunk]

    @property
    def root_size(self) -> int:
        """How far the root goes in memory: what is there before any overlay is loaded."""
        raise NotImplementedError

    def root(self) -> tuple[bytearray, set[int]]:
        """The program's memory with only the root in it, and the places that hold segments."""
        raise NotImplementedError

    def in_root(self, linear: int) -> bool:
        """Whether a place in memory is the root's."""
        return 0 <= linear < self.root_size

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
        memory, relocations = self.root()
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
        program: Program,
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

    def overlay_at(self, linear: int) -> int | None:
        """The number of the overlay a place in memory is in; None for the root and the rest."""
        segment = linear // 16
        for overlay in self.loaded.values():
            if overlay.load_segment <= segment < overlay.end_segment:
                return overlay.number
        return None

    def where(self, linear: int) -> str:
        """What a place in memory belongs to: the root, or an overlay."""
        number = self.overlay_at(linear)
        if number is not None:
            return f"overlay {number}"
        if self.program.in_root(linear):
            return "root"
        return "not loaded"

"""Where the program loads its string blocks, and so how its code names a string.

The program has one routine that loads a block, which C would declare as

    int load_strings(char far * far *array, int block);

It unpacks the block into memory it allocates and fills the array with a far pointer to each
string, in order. From then on the code names a string by its place in the array: string 25 of
the block is the pointer at the array's address plus 100. So the calls to the routine say
which block is behind an array, and each call is found here by the bytes that push its two
arguments.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Optional

from .program import Image, Program

# the routine is the one that says this, when an array it's given already has a block
_MESSAGE = b"[WARNING: string block loading over existing block]"

# the block: mov ax,<block> / push ax, or sub ax,ax / push ax for block 0
_BLOCK = rb"(?:\xb8(?P<block>..)|\x2b\xc0)\x50"
# an array in the program's data: mov ax,<offset> / mov dx,<segment> / push dx / push ax,
# or, in the segment DS has, mov ax,<offset> / push ds / push ax
_STATIC = rb"\xb8(?P<offset>..)(?:\xba(?P<segment>..)\x52|\x1e)\x50"
# an array among the caller's own variables: lea ax,[bp-n] / push ss / push ax
_LOCAL = rb"\x8d(?:\x46.|\x86..)\x16\x50"

_KNOWN = re.compile(_BLOCK + rb"(?:" + _STATIC + rb"|(?P<local>" + _LOCAL + rb"))\Z", re.DOTALL)
_ARRAY_ONLY = re.compile(rb"(?:" + _STATIC + rb"|(?P<local>" + _LOCAL + rb"))\Z", re.DOTALL)

# an array's address: its segment, or None for the segment DS has, and its offset
Array = tuple[Optional[int], int]


@dataclass(frozen=True)
class Load:
    """A call to the routine that loads a block."""

    linear: int  # where the call is, in bytes from the program's start
    overlay: int | None  # the overlay the call is in, or None for the root
    block: int | None  # None when the block isn't a number in the code
    array: Array | None  # None for an array that isn't in the data
    local: bool  # the array is among the caller's own variables, there for as long as the call

    @property
    def where(self) -> str:
        return "root" if self.overlay is None else f"overlay {self.overlay}"


def _word(code: bytes | None) -> int | None:
    return None if code is None else int.from_bytes(code, "little")


def find_loader(program: Program) -> tuple[int, int]:
    """The address of the routine that loads a block."""
    image = program.image()
    memory = image.memory
    message = memory.find(_MESSAGE)
    if message < 0:
        raise ValueError("the program doesn't have the message its string loader gives")

    # the routine names the message as mov ax,<offset> / mov dx,<segment>
    use = -1
    for linear in sorted(image.relocations):
        if memory[linear - 4] == 0xB8 and memory[linear - 1] == 0xBA:
            offset, segment = image.word(linear - 3), image.word(linear)
            if segment * 16 + offset == message:
                use = linear
                break
    if use < 0:
        raise ValueError("nothing in the root uses the string loader's message")

    # it starts at the last push bp / mov bp,sp before that which follows a far return
    start = memory.rfind(b"\xcb\x55\x8b\xec", 0, use) + 1

    # and its segment is the one the calls to it give
    for segment in sorted(image.segment_references(), reverse=True):
        offset = start - segment * 16
        if 0 <= offset < 0x10000 and _calls(image, segment, offset, 0, len(memory)):
            return segment, offset
    raise ValueError("nothing calls the string loader")


def _calls(image: Image, segment: int, offset: int, start: int, end: int) -> list[int]:
    """The far calls to an address in a part of memory."""
    call = b"\x9a" + offset.to_bytes(2, "little") + segment.to_bytes(2, "little")
    found = []
    at = image.memory.find(call, start, end)
    while at >= 0:
        if at + 3 in image.relocations:
            found.append(at)
        at = image.memory.find(call, at + 1, end)
    return found


def find_loads(program: Program) -> list[Load]:
    """Every call to the routine that loads a block, in the root and in each overlay."""
    segment, offset = find_loader(program)
    loads: list[Load] = []

    def scan(image: Image, start: int, end: int, overlay: int | None) -> None:
        for call in _calls(image, segment, offset, start, end):
            before = image.memory[call - 16 : call]
            match = _KNOWN.search(before)
            block: int | None = None
            array: Array | None = None
            if match:
                block = _word(match["block"]) or 0
            else:
                match = _ARRAY_ONLY.search(before)
            if match and match["offset"]:
                array = (_word(match["segment"]), _word(match["offset"]) or 0)
            loads.append(Load(call, overlay, block, array, bool(match and match["local"])))

    scan(program.image(), 0, program.root_size, None)
    for overlay in program.overlays:
        if not overlay.preloaded:
            image = program.image(overlay.number)
            scan(image, overlay.load_segment * 16, overlay.end_segment * 16, overlay.number)
    return loads


class StringNames:
    """Which blocks are behind which arrays, for the code of the root or of an overlay."""

    def __init__(self, loads: list[Load]):
        self._blocks: dict[tuple[int | None, Array], set[int]] = {}
        for load in loads:
            if load.array is not None and load.block is not None:
                self._blocks.setdefault((load.overlay, load.array), set()).add(load.block)
        self.arrays = sorted({array for _, array in self._blocks}, key=lambda array: array[1])

    def blocks(self, overlay: int | None, array: Array) -> list[int]:
        """The blocks the code of an overlay, or of the root for None, loads into an array."""
        return sorted(self._blocks.get((overlay, array), ()))

    def array_at(self, in_ds: bool, offset: int) -> tuple[Array, int] | None:
        """
        The array a far pointer at an offset would be in, and the pointer's number in it: the
        nearest array at or below the offset, with the offset a whole number of pointers on
        from it. The arrays looked among are those in DS's segment, or those in another.
        """
        below = [array for array in self.arrays if (array[0] is None) == in_ds and array[1] <= offset]
        if not below:
            return None
        array = below[-1]
        index, rest = divmod(offset - array[1], 4)
        return (array, index) if rest == 0 else None

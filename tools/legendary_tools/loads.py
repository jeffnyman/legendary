"""Where the program loads its string blocks, and so how its code names a string.

The program has one routine that loads a block, which C would declare as

    int load_strings(char far * far *array, int block);

It unpacks the block into memory it allocates and fills the array with a far pointer to each
string, in order. From then on the code names a string by its place in the array: string 25 of
the block is the pointer at the array's address plus 100. So the calls to the routine say
which block is behind an array, and each call is found here by the bytes that push its two
arguments.

*Spellcasting 201* has no arrays of its own for this. Its code names a string by one word,
the block in the high byte and the string's place in the block in the low, and hands the
word to one of two routines: one gives the string's far pointer, loading the block if it
isn't in memory, and one prints the string.
"""

from __future__ import annotations

import re
import struct
from collections import Counter
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


# mov ax,[bp+6] / mov cl,8 / shr ax,cl / mov [bp-6],ax / mov al,[bp+6] / sub ah,ah /
# mov [bp-2],ax: a string's word taken apart, into its block and its place there
_TAKEN_APART = bytes.fromhex("8b4606 b108 d3e8 8946fa 8a4606 2ae4 8946fe")
_PROLOGUE = b"\x55\x8b\xec"


@dataclass(frozen=True)
class Named:
    """A call that names a string by its word."""

    linear: int
    overlay: int | None
    prints: bool  # the call prints the string; otherwise it takes its pointer
    block: int
    string: int
    in_data: bool = False  # the string is named by a pointer kept as data, not made by code

    @property
    def where(self) -> str:
        return "root" if self.overlay is None else f"overlay {self.overlay}"


# mov ax,<number> / mov dx,F000h / push dx / push ax: a string named as a far pointer whose
# segment is one no text is in, and whose offset is the string's number
_POINTED = re.compile(rb"\xb8(..)\xba\x00\xf0\x52\x50", re.DOTALL)
POINTER_SEGMENT = 0xF000
# mov dx,F000h: what gives a number in AX its segment
_SEGMENT_GIVEN = b"\xba\x00\xf0"
# mov ax,<number> and then that at once, or a jump to it: where several strings are handed
# to one call, the compiler loads each number and jumps to the one place that does the rest
_LOADED = re.compile(rb"\xb8(..)(?:(?=\xba\x00\xf0)|\xeb(.)|\xe9(..))", re.DOTALL)
# the number and the segment put by as a far pointer, among a routine's own variables, in
# the data, or through ES: mov word [where],<number> / mov word [where+2],F000h
_STORED = re.compile(
    rb"\xc7\x46(.)(..)\xc7\x46(.)\x00\xf0"
    rb"|\xc7\x06(..)(..)\xc7\x06(..)\x00\xf0"
    rb"|\x26\xc7\x06(..)(..)\x26\xc7\x06(..)\x00\xf0",
    re.DOTALL,
)


def pointed_string(number: int) -> tuple[int, int]:
    """The block and the string that such a pointer's offset names: the block is above ten bits."""
    return number >> 10, number & 0x3FF


def _loaded(image: Image, start: int, end: int) -> list[tuple[int, int, tuple[int, int] | None]]:
    """
    Each place in a stretch of code that makes a string's pointer: where, the number, and the
    routine it is handed to at once when it is.
    """
    memory = image.memory
    found = []
    for match in _LOADED.finditer(memory, start, end):
        if match.start() + 1 in image.relocations:
            continue
        # where the segment is given: here, or where the jump goes
        at = match.end()
        if match[2] is not None:
            at += int.from_bytes(match[2], "little", signed=True)
        elif match[3] is not None:
            at += int.from_bytes(match[3], "little", signed=True)
        if not start <= at < end or memory[at : at + 3] != _SEGMENT_GIVEN or at + 1 in image.relocations:
            continue
        called = None
        # push dx / push ax / call far
        if memory[at + 3 : at + 6] == b"\x52\x50\x9a" and at + 8 in image.relocations:
            offset, segment = struct.unpack_from("<HH", memory, at + 6)
            called = (segment, offset)
        found.append((match.start(), int.from_bytes(match[1], "little"), called))
    for match in _STORED.finditer(memory, start, end):
        where, number, above = (group for group in match.groups() if group is not None)
        if (int.from_bytes(above, "little") - int.from_bytes(where, "little")) & 0xFFFF == 2:
            found.append((match.start(), int.from_bytes(number, "little"), None))
    return found


def find_pointed(
    program: Program, strings: list[int] | None = None
) -> tuple[list[Named], tuple[int, int] | None]:
    """
    Every place that names a string as a far pointer of that kind, in the root and each
    overlay, and the routine most of them hand it to at once, which prints it. Given how many
    strings each block has, the pointers kept as data are found too: without that, any two
    bytes before the segment's would pass for one.
    """
    found: list[tuple[int, int | None, int, tuple[int, int] | None, bool]] = []

    def scan(image: Image, start: int, end: int, overlay: int | None) -> None:
        memory = image.memory
        made = _loaded(image, start, end)
        found.extend((linear, overlay, number, called, False) for linear, number, called in made)
        if strings is None:
            return
        at = memory.find(b"\x00\xf0", start, end)
        while at >= 0:
            # a pointer as data: not the one mov dx gives, nor the second half of one stored
            before = memory[at - 5 : at]
            in_code = before[-1:] == b"\xba" or before[-3:-2] == b"\x46" or before[:2] == b"\xc7\x06"
            if at - start >= 5 and not in_code and at not in image.relocations:
                number = int.from_bytes(memory[at - 2 : at], "little")
                block, string = pointed_string(number)
                if block < len(strings) and string < strings[block]:
                    found.append((at - 2, overlay, number, None, True))
            at = memory.find(b"\x00\xf0", at + 1, end)

    scan(program.image(), 0, program.root_size, None)
    for overlay in program.overlays:
        if not overlay.preloaded:
            image = program.image(overlay.number)
            scan(image, overlay.load_segment * 16, overlay.end_segment * 16, overlay.number)
    calls = Counter(called for _, _, _, called, _ in found if called)
    printer = calls.most_common(1)[0][0] if calls else None
    named = [
        Named(linear, overlay, called == printer and printer is not None, *pointed_string(number), in_data)
        for linear, overlay, number, called, in_data in found
    ]
    return sorted(named, key=lambda call: (call.overlay or 0, call.linear)), printer


def find_namers(program: Program) -> dict[tuple[int, int], bool]:
    """
    The two routines a string's word is handed to, each with whether it prints: the one that
    takes the word apart, and the one after it that calls that and prints what it gets.
    """
    image = program.image()
    memory = image.memory
    apart = memory.find(_TAKEN_APART, 0, program.root_size)
    if apart < 0:
        raise ValueError("the routine that finds a string by its number wasn't found")
    gives = memory.rindex(_PROLOGUE, 0, apart)
    segment = max(found for found in image.segment_references() if found * 16 <= gives)
    found = {(segment, gives - segment * 16): False}
    # push [bp+6] / push cs / call <the first>, in the routine after it
    at = memory.find(b"\xff\x76\x06\x0e\xe8", gives + 1)
    if at >= 0 and at + 7 + struct.unpack_from("<h", memory, at + 5)[0] == gives:
        found[segment, memory.rindex(_PROLOGUE, 0, at) - segment * 16] = True
    return found


def find_named(program: Program) -> list[Named]:
    """Every call that names a string by a word that is a constant, in the root and each overlay."""
    namers = find_namers(program)
    named: list[Named] = []

    def scan(image: Image, start: int, end: int, overlay: int | None) -> None:
        for (segment, offset), prints in namers.items():
            for call in _calls(image, segment, offset, start, end):
                # mov ax,<word> / push ax
                if image.memory[call - 4] == 0xB8 and image.memory[call - 1] == 0x50:
                    string, block = image.memory[call - 3], image.memory[call - 2]
                    named.append(Named(call, overlay, prints, block, string))

    scan(program.image(), 0, program.root_size, None)
    for overlay in program.overlays:
        if not overlay.preloaded:
            image = program.image(overlay.number)
            scan(image, overlay.load_segment * 16, overlay.end_segment * 16, overlay.number)
    return sorted(named, key=lambda call: (call.overlay or 0, call.linear))

"""The routines the game runs for an action: each object's own, and each action's.

The game does a thing by performing an action, a number from the grammar, on up to two
objects. The routine that performs one gives a turn to the routines of the room, the actor and
the objects, each with a reason for the call, then to the action's own. A routine that
answers with something other than zero has dealt with it, and the rest aren't asked.

Which routine is an object's, or an action's, isn't in any table of data. The game's source
had a C switch for each: one on the action's number, and for each type of object one on the
object's number, every case a call to a routine. The compiler made most of them a table of
jumps, and the cases are read here from the code.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass

from .program import Image, Program

# mov ax,[bp+6] / (sub ax,<first>) / cmp ax,<count less one> / jbe / jmp <default>, or
# ja <default> / add ax,ax / xchg bx,ax / jmp cs:[bx+<table>]: a switch made a table of jumps
_JUMP_TABLE = re.compile(
    rb"\x8b\x46\x06(?:\x2d(..))?\x3d(..)(?:\x76\x03\xe9..|\x77.)\x03\xc0\x93\x2e\xff\xa7(..)",
    re.DOTALL,
)
_PROLOGUE = b"\x55\x8b\xec"
_TAKES_ARGUMENT = b"\x8b\x46\x06"
# how far into a switch's routine its argument is taken: at once in Spellcasting 101, and in
# Spellcasting 201 after the stack is checked and, for an action's, the call is noted
_PREAMBLE = 48
# cmp ax,<value> / je <case>, with the case near or, by a jump around a jump, far
_COMPARE = re.compile(rb"\x3d(..)(?:\x74(.)|\x75\x03\xe9(..))", re.DOTALL)
# an object's type, less one, made the index of a table of jumps
_BY_TYPE = re.compile(
    rb"\x26\x8a\x87..\x2a\xe4\x2d\x01\x00\x3d(..)\x77.\x03\xc0\x93\x2e\xff\xa7(..)", re.DOTALL
)
# cmp ax,<how many> / ja <none> / dec [bp-2] / mov bx,[bp-2] / shl bx,1 / shl bx,1 /
# mov es,[segment variable] / mov ax,es:[bx] / mov dx,es:[bx+2]: an object's routine, taken
# from a table of them by the number its record has
_BY_NUMBER = re.compile(
    rb"\x3d(..)\x77.\xff\x4e\xfe\x8b\x5e\xfe\xd1\xe3\xd1\xe3\x8e\x06(..)\x26\x8b\x87(..)\x26\x8b\x97",
    re.DOTALL,
)
# cmp byte [bp+6],<how many> / ja <none> / mov al,6 / mul byte [bp+6] / mov bx,ax /
# mov es,[segment variable] / call far es:[bx+table]: an action's routine, from a table of
# six bytes an action
_ACTION_TABLE = re.compile(
    rb"\x80\x7e\x06(.)\x77.\xb0\x06\xf6\x66\x06\x8b\xd8\x8e\x06(..)\x26\xff\x9f(..)", re.DOTALL
)
_NOTICE = b"MS Run-Time Library"
# mov al,es:[<the action>] / sub ah,ah / push ax / call far: the action handed to a routine
_BY_ACTION = re.compile(rb"\x26\xa0..\x2a\xe4\x50\x9a(..)(..)", re.DOTALL)


@dataclass(frozen=True)
class Routine:
    """A routine a case calls."""

    overlay: int | None  # the overlay it's in, or None for the root
    segment: int
    offset: int

    def __str__(self) -> str:
        where = "the root" if self.overlay is None else f"overlay {self.overlay}"
        return f"{self.segment:04x}:{self.offset:04x} in {where}"


class _Code:
    """The root's code, with what's needed to read a switch from it."""

    def __init__(self, program: Program):
        self.program = program
        self.image: Image = program.image()
        self.memory = self.image.memory
        self.root = self.memory[: program.root_size]
        self.segments = sorted(self.image.segment_references())

    def segment_of(self, linear: int) -> int:
        """The code segment a place is in: the nearest one named at or below it."""
        return max(segment for segment in self.segments if segment * 16 <= linear)

    def word(self, linear: int) -> int:
        return struct.unpack_from("<H", self.memory, linear)[0]

    def called(self, at: int) -> Routine | int | None:
        """
        What a case does: after pushing its arguments, a far call, which is to a routine in
        the root or by a thunk to one in an overlay, or a near call, which is given as where it
        goes. None when it does neither.
        """
        while self.memory[at : at + 2] == b"\xff\x76":
            at += 3
        if self.memory[at] == 0x9A and at + 3 in self.image.relocations:
            offset, segment = struct.unpack_from("<HH", self.memory, at + 1)
            thunk = self.program.thunk_at(segment, offset)
            if thunk:
                return Routine(thunk.overlay, thunk.target_segment, thunk.target_offset)
            return Routine(None, segment, offset)
        if self.memory[at : at + 2] == b"\x0e\xe8":
            # push cs / call near: the offset wraps within the segment
            segment = self.segment_of(at) * 16
            return segment + ((at + 4 + self.word(at + 2) - segment) & 0xFFFF)
        return None

    def switch(self, start: int) -> int | None:
        """Where a routine takes its argument to switch on, when it starts as a switch's does."""
        if not self.memory.startswith(_PROLOGUE, start):
            return None
        at = self.memory.find(_TAKES_ARGUMENT, start, start + _PREAMBLE)
        return at if at >= 0 else None

    def jump_table(self, start: int) -> re.Match[bytes] | None:
        at = self.switch(start)
        return _JUMP_TABLE.match(self.memory, at) if at is not None else None

    def cases(self, start: int) -> dict[int, int]:
        """A switch's cases, from the routine that starts at a place: each value, and where it goes."""
        segment = self.segment_of(start)
        match = self.jump_table(start)
        if match:
            first = int.from_bytes(match[1], "little") if match[1] else 0
            count = int.from_bytes(match[2], "little") + 1
            table = segment * 16 + int.from_bytes(match[3], "little")
            return {first + index: segment * 16 + self.word(table + 2 * index) for index in range(count)}

        # a switch of few cases is compares, to the routine's far return
        end = self.memory.index(b"\x5d\xcb", start)
        found = {}
        for compare in _COMPARE.finditer(self.memory, start, end):
            if compare[2]:
                target = compare.end() + int.from_bytes(compare[2], "little", signed=True)
            else:
                target = compare.end() + int.from_bytes(compare[3], "little", signed=True)
            found[int.from_bytes(compare[1], "little")] = target
        return found

    def routines(self, start: int) -> dict[int, Routine]:
        """A switch's cases that call a routine."""
        found = {}
        for value, target in self.cases(start).items():
            called = self.called(target)
            if isinstance(called, Routine):
                found[value] = called
        return found


def object_routines(program: Program) -> dict[int, Routine]:
    """Each object's routine, by the object's number, for the objects that have one."""
    code = _Code(program)
    for match in _BY_TYPE.finditer(code.root):
        segment = code.segment_of(match.start())
        table = segment * 16 + int.from_bytes(match[2], "little")
        count = int.from_bytes(match[1], "little") + 1
        # the one that's wanted hands the object and the reason on to a switch for its type
        switches = [code.called(segment * 16 + code.word(table + 2 * index)) for index in range(count)]
        if all(isinstance(switch, int) for switch in switches):
            found: dict[int, Routine] = {}
            for switch in switches:
                if isinstance(switch, int):
                    found.update(code.routines(switch))
            return found
    numbered = _numbered_routines(code)
    if numbered is not None:
        return numbered
    raise ValueError("the code that calls an object's routine wasn't found")


def _numbered_routines(code: _Code) -> dict[int, Routine] | None:
    """The same, where a record has the number of its object's routine and a table has the routines."""
    match = _BY_NUMBER.search(code.root)
    notice = code.memory.find(_NOTICE)
    if not match or notice < 0:
        return None
    count = int.from_bytes(match[1], "little")
    table = code.word(notice - 8 + int.from_bytes(match[2], "little")) * 16
    table += int.from_bytes(match[3], "little")
    # the objects are read for the numbers, by a module that needs this one's routines
    from .objects import ObjectTable  # noqa: PLC0415

    found = {}
    for thing in ObjectTable(code.program).objects:
        if 1 <= thing.routine <= count:
            offset, segment = struct.unpack_from("<HH", code.memory, table + 4 * (thing.routine - 1))
            thunk = code.program.thunk_at(segment, offset)
            found[thing.number] = (
                Routine(thunk.overlay, thunk.target_segment, thunk.target_offset)
                if thunk
                else Routine(None, segment, offset)
            )
    return found


def action_routines(program: Program) -> tuple[dict[int, Routine], dict[int, Routine]]:
    """Each action's routine, by the action's number, and the routines some have before it."""
    code = _Code(program)
    before: dict[int, Routine] = {}
    for match in _BY_ACTION.finditer(code.root):
        start = int.from_bytes(match[2], "little") * 16 + int.from_bytes(match[1], "little")
        if code.switch(start) is None:
            continue
        if code.jump_table(start):
            return code.routines(start), before
        # the switch called first, of few cases, is the routines that come before
        before = code.routines(start)
    tabled = _tabled_actions(code)
    if tabled is not None:
        return tabled
    raise ValueError("the code that calls an action's routine wasn't found")


def tabled_action_flags(program: Program) -> bytes | None:
    """A byte for each action, where the actions' routines are in a table; None where they aren't."""
    code = _Code(program)
    match = _ACTION_TABLE.search(code.root)
    notice = code.memory.find(_NOTICE)
    if not match or notice < 0:
        return None
    table = code.word(notice - 8 + int.from_bytes(match[2], "little")) * 16
    # the far pointer the call takes is two bytes into an entry
    table += int.from_bytes(match[3], "little") - 2
    return bytes(code.memory[table + 6 * number] for number in range(1, match[1][0] + 1))


def _tabled_actions(code: _Code) -> tuple[dict[int, Routine], dict[int, Routine]] | None:
    """
    The same, where the routines are in a table: six bytes an action, which are its byte of
    flags, a byte that is the number of the routine that comes before it, or 0, and a far
    pointer to its own. The routines that come before are far pointers from the start of the
    table's segment.
    """
    match = _ACTION_TABLE.search(code.root)
    notice = code.memory.find(_NOTICE)
    if not match or notice < 0:
        return None
    segment = code.word(notice - 8 + int.from_bytes(match[2], "little")) * 16
    table = segment + int.from_bytes(match[3], "little")

    def routine(at: int) -> Routine:
        offset, target = struct.unpack_from("<HH", code.memory, at)
        thunk = code.program.thunk_at(target, offset)
        if thunk:
            return Routine(thunk.overlay, thunk.target_segment, thunk.target_offset)
        return Routine(None, target, offset)

    actions, before = {}, {}
    for number in range(1, match[1][0] + 1):
        entry = table + 6 * number
        actions[number] = routine(entry)
        if earlier := code.memory[entry - 1]:
            before[number] = routine(segment + 4 * (earlier - 1))
    return actions, before

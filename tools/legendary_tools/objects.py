"""The game's objects: its rooms, things and people, as the program has them when it starts.

The program numbers its objects from 1. A table has six bytes for each: its type, a byte
that's zero, and a far pointer to its record. A second table has three bytes for each type:
how many places an object of the type can be in, how many lists of other objects it can hold,
and its record's size.

Every record starts the same way: a far pointer to the object's name, one to a second name,
and its flags. What follows depends on the type. An object that can be somewhere has the
number of the object it's in, which of that object's lists it's on, and the number of the next
object on the list; an object that can hold others has the number of the first on each of its
lists. That is the tree Infocom's games keep their objects in, with more than one list to a
parent.

The tables are found by the code that reads them, so nothing here is an address written down
from one game. The layouts of the records are *Spellcasting 101*'s, and are used only for types
of the sizes it has.
"""

from __future__ import annotations

import collections
import re
import struct
from dataclasses import dataclass, field

from .plink86 import Image, Plink86Program

# mov es,[segment variable] / mov al,es:[si-6]: an object's type, with SI six times its number
_TABLE = re.compile(rb"\x8e\x06(..)\x26\x8a\x84\xfa\xff", re.DOTALL)
# mov es,[segment variable] / mov ax,es:[count] / cmp [bp+6],ax: is the number an object's?
_COUNT = re.compile(rb"\x8e\x06(..)\x26\xa1(..)\x39\x46\x06", re.DOTALL)
# mov es,[segment variable] / mov al,es:[bx+entry] / sub ah,ah / cmp ax,[bp...]: a type's entry
_TYPES = re.compile(rb"\x8e\x06(..)\x26\x8a\x87(..)\x2a\xe4\x3b\x46", re.DOTALL)

# Microsoft C's notice is 8 bytes into the segment DS has
_NOTICE = b"MS Run-Time Library"


@dataclass(frozen=True)
class Layout:
    """Where a type's record has what, after the 14 bytes every record starts with."""

    size: int
    places: int | None = None  # the objects it's in, a word each
    relation: int | None = None  # a byte: which of its parent's lists it's on
    next: int | None = None  # the next object on that list
    lists: int | None = None  # the first object on each of its own lists, a word each
    exit_count: int | None = None  # a room's exits: a byte, and a far pointer to them
    exits: int | None = None


# Spellcasting 101's, from its routines that read and write each field for each type
LAYOUTS = {
    1: Layout(28, lists=0x0E, exit_count=0x16, exits=0x18),
    2: Layout(36, places=0x12, relation=0x14, next=0x16, lists=0x18),
    3: Layout(14),
    4: Layout(22, lists=0x0E),
    5: Layout(22, lists=0x0E),
    6: Layout(26, places=0x0E, lists=0x12),
    7: Layout(32, places=0x0E, relation=0x10, next=0x12, lists=0x14),
    8: Layout(22, places=0x0E, relation=0x10, next=0x12),
}


@dataclass(frozen=True)
class Exit:
    """A row of a room's exits: a direction, a kind, and a far pointer."""

    direction: int
    kind: int
    segment: int
    offset: int


@dataclass
class GameObject:
    """An object as the program starts with it."""

    number: int
    type: int
    segment: int  # where its record is
    offset: int
    record: bytes
    name: str
    second_name: str | None
    flags: int  # the 16 bits after the names: flag 1 is the lowest
    places: list[int] = field(default_factory=list)
    relation: int | None = None
    next: int | None = None
    lists: list[int] = field(default_factory=list)
    exits: list[Exit] = field(default_factory=list)

    def word(self, at: int) -> int:
        return struct.unpack_from("<H", self.record, at)[0]

    def has_flag(self, flag: int) -> bool:
        """Flags are numbered from 1, eight to a byte from each byte's low bit."""
        return bool(self.record[8 + (flag - 1) // 8] >> ((flag - 1) % 8) & 1)


@dataclass(frozen=True)
class TypeEntry:
    """A type's entry in the table of types."""

    type: int
    places: int
    lists: int
    size: int


class ObjectTable:
    """The objects, read from the program's memory as it is before the game changes it."""

    def __init__(self, program: Plink86Program):
        self.image = program.image()
        memory = self.image.memory
        root = memory[: len(program.mz.image)]
        notice = memory.find(_NOTICE)
        if notice < 0:
            raise ValueError("the program's data segment wasn't found")
        self.data_segment = (notice - 8) // 16

        table_variable = self._commonest(_TABLE, root)[0]
        count_variable, count_offset = self._commonest(_COUNT, root)
        # the entries are read a byte at a time, so the lowest offset read is the table's start
        entries = collections.Counter(match.groups() for match in _TYPES.finditer(root))
        types_variable = self._commonest(_TYPES, root)[0]
        types_offset = min(
            self._signed(int.from_bytes(offset, "little"))
            for variable, offset in entries
            if int.from_bytes(variable, "little") == types_variable
        )

        self.table_segment = self._data_word(table_variable)
        self.count = self._word(self._data_word(count_variable), count_offset)
        types_segment = self._data_word(types_variable)

        self.types: dict[int, TypeEntry] = {}
        self.objects: list[GameObject] = []
        for number in range(1, self.count + 1):
            entry = self.table_segment * 16 + 6 * (number - 1)
            kind = memory[entry]
            offset, segment = struct.unpack_from("<HH", memory, entry + 2)
            if kind not in self.types:
                at = types_segment * 16 + ((types_offset + 3 * kind) & 0xFFFF)
                self.types[kind] = TypeEntry(kind, *memory[at : at + 3])
            self.objects.append(self._read(number, kind, segment, offset))

    @staticmethod
    def _signed(word: int) -> int:
        return word - 0x10000 if word >= 0x8000 else word

    @staticmethod
    def _commonest(pattern: re.Pattern[bytes], root: bytes) -> tuple[int, ...]:
        found = collections.Counter(match.groups() for match in pattern.finditer(root))
        if not found:
            raise ValueError("the code that reads the table of objects wasn't found")
        return tuple(int.from_bytes(group, "little") for group in found.most_common(1)[0][0])

    def _word(self, segment: int, offset: int) -> int:
        return self.image.word(segment * 16 + offset)

    def _data_word(self, offset: int) -> int:
        return self._word(self.data_segment, offset)

    def _text(self, segment: int, offset: int) -> str | None:
        if segment == 0 and offset == 0:
            return None
        memory = self.image.memory
        start = segment * 16 + offset
        return memory[start : memory.index(b"\0", start)].decode("latin-1")

    def _read(self, number: int, kind: int, segment: int, offset: int) -> GameObject:
        memory = self.image.memory
        start = segment * 16 + offset
        size = self.types[kind].size
        record = memory[start : start + size]
        name_offset, name_segment, second_offset, second_segment, flags = struct.unpack_from("<HHHHH", record)
        found = GameObject(
            number,
            kind,
            segment,
            offset,
            record,
            self._text(name_segment, name_offset) or "",
            self._text(second_segment, second_offset),
            flags,
        )
        layout = LAYOUTS.get(kind)
        if layout is None or layout.size != size:
            return found
        entry = self.types[kind]
        if layout.places is not None:
            found.places = [found.word(layout.places + 2 * index) for index in range(entry.places)]
        if layout.relation is not None:
            found.relation = record[layout.relation]
        if layout.next is not None:
            found.next = found.word(layout.next)
        if layout.lists is not None:
            found.lists = [found.word(layout.lists + 2 * index) for index in range(entry.lists)]
        if layout.exits is not None and layout.exit_count is not None:
            exits_offset, exits_segment = struct.unpack_from("<HH", record, layout.exits)
            at = exits_segment * 16 + exits_offset
            for row in range(record[layout.exit_count]):
                found.exits.append(
                    Exit(
                        *struct.unpack_from("<BBHH", memory, at + 6 * row)[:2],
                        *reversed(struct.unpack_from("<HH", memory, at + 6 * row + 2)),
                    )
                )
        return found

    def get(self, number: int) -> GameObject:
        if not 1 <= number <= self.count:
            raise ValueError(f"there is no object {number}: they go from 1 to {self.count}")
        return self.objects[number - 1]

    def on_list(self, first: int) -> list[GameObject]:
        """The objects on a list, from its first, each going on to its next."""
        found = []
        seen = set()
        while first and first not in seen and first <= self.count:
            seen.add(first)
            found.append(self.get(first))
            first = found[-1].next or 0
        return found


def is_pointer(image: Image, obj: GameObject, at: int) -> bool:
    """Whether the four bytes at a place in an object's record are a far pointer."""
    return obj.segment * 16 + obj.offset + at + 2 in image.relocations


# the save routine's loops: mov es,[variable] / push es:[si+size] / push es:[si+segment] /
# push es:[si+offset], for each saved variable, and mov es,[variable] / mov ax,es:[count] /
# cmp [bp-n],ax for each loop's end
_SAVED = re.compile(rb"\x8e\x06(..)\x26\xff\xb4(..)\x26\xff\xb4(..)\x26\xff\xb4(..)", re.DOTALL)
_LOOP_END = re.compile(rb"\x8e\x06(..)\x26\xa1(..)\x39\x86", re.DOTALL)
# mov ax,2 / push ax / mov ax,<offset> / mov dx,<segment> / push dx / push ax / push cs / call:
# the count of timers, written as a word
_TIMERS = re.compile(rb"\xb8\x02\x00\x50\xb8(..)\xba(..)\x52\x50\x0e\xe8", re.DOTALL)


@dataclass(frozen=True)
class SavedVariable:
    """A piece of the program's data that is saved with the objects."""

    segment: int
    offset: int
    size: int


class SavedState:
    """
    What the program saves, which is what RESTART.DAT has: four bytes, every object's record
    in the table's order, each saved variable, a count, and six bytes for each it counts.
    What it counts is taken here to be timers, things due to happen later; that is a guess.
    """

    def __init__(self, program: Plink86Program, objects: ObjectTable):
        memory = objects.image.memory
        root = memory[: len(program.mz.image)]

        def word(code: bytes) -> int:
            return int.from_bytes(code, "little")

        saved = _SAVED.search(root)
        timers = _TIMERS.search(root)
        if not saved or not timers:
            raise ValueError("the code that saves the game wasn't found")
        table_segment = objects.image.word(objects.data_segment * 16 + word(saved[1]))
        table = table_segment * 16 + word(saved[4])
        self.timer_count_offset = word(timers[1])

        # the loop that isn't the objects' or the timers' is the saved variables'
        counts = {
            (word(match[1]), word(match[2]))
            for match in _LOOP_END.finditer(root)
            if word(match[2]) != self.timer_count_offset
        }
        variables = None
        for variable, offset in counts:
            count = objects.image.word(objects.image.word(objects.data_segment * 16 + variable) * 16 + offset)
            if count != objects.count:
                variables = count
        if variables is None:
            raise ValueError("the count of saved variables wasn't found")

        self.variables = [
            SavedVariable(segment, offset, size)
            for offset, segment, size in struct.iter_unpack("<HHH", memory[table : table + 6 * variables])
        ]
        self.objects_size = sum(len(found.record) for found in objects.objects)
        self.variables_size = sum(variable.size for variable in self.variables)

    def size(self, timers: int) -> int:
        """The size of a saved state with that many timers."""
        return 4 + self.objects_size + self.variables_size + 2 + 6 * timers

    def timers_in(self, file: bytes) -> int:
        """The count of timers in a saved state, which is the word after the variables."""
        return struct.unpack_from("<H", file, 4 + self.objects_size + self.variables_size)[0]

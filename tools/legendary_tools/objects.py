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
from one game. The layouts of the records are *Spellcasting 101*'s and *Spellcasting 201*'s,
and are used only for types of the sizes those have.

*Spellcasting 201* keeps the same tables and changes what a record holds. The names are out
of the program, in OBJECT.DAT, and a record has a word for each: where the name starts
there, counted from 1. A flag's number no longer says which bit it is: a table has four bytes
for each flag, the bit and the types that have it, so that two types can use one bit for two
flags. And a room's exits are rows of one table for all the rooms, four bytes each, with the
room holding its first row's number.
"""

from __future__ import annotations

import collections
import re
import struct
from dataclasses import dataclass, field

from .data import StringFile, Word
from .program import Image, Program

# mov es,[segment variable] / mov al,es:[si+n]: an object's type, with SI six times its number,
# so that n is six less than where the table starts
_TABLE = re.compile(rb"\x8e\x06(..)\x26\x8a\x84(..)", re.DOTALL)
# Gateway II's, from a later compiler: the entry is found with BX, and into AL, BL or CL
_TABLE_BX = re.compile(rb"\x8e\x06(..)\x26\x8a[\x87\x8f\x9f](..)", re.DOTALL)
# and its count is in the data segment, read with no segment loaded: mov ax,[count] /
# cmp [bp+6],ax
_COUNT_NEAR = re.compile(rb"\xa1(..)\x39\x46\x06", re.DOTALL)
# and a type's entry is a byte or two before BX: mov es,[variable] / mov al|cl,es:[bx-3]
_TYPES_BX = re.compile(rb"\x8e\x06(..)\x26\x8a[\x87\x8f]([\xf0-\xff]\xff)", re.DOTALL)
# mov es,[segment variable] / mov ax,es:[count] / cmp [bp+6],ax: is the number an object's?
_COUNT = re.compile(rb"\x8e\x06(..)\x26\xa1(..)\x39\x46\x06", re.DOTALL)
# mov es,[segment variable] / mov al,es:[bx+entry] / sub ah,ah / cmp ax,[bp...]: a type's entry
_TYPES = re.compile(rb"\x8e\x06(..)\x26\x8a\x87(..)\x2a\xe4\x3b\x46", re.DOTALL)

# mov es,[segment variable] / mov si,es:[bx+list] / or si,si: a word from an object's list
_WORD_LIST = re.compile(rb"\xd1\xe3\x8e\x06(..)\x26\x8b\xb7(..)(?:\x0b\xf6|\x8b\xc6)", re.DOTALL)

# shl si,cl / mov es,[segment variable] / mov ax,es:[si+list]: an entry of one of a room's rows
_ROOM_ROW = re.compile(rb"\xd3\xe6\x8e\x06(..)\x26\x8b\x84(..)", re.DOTALL)

# the same, where the row is searched: cmp word ptr es:[si+list],0
_ROOM_SEARCH = re.compile(rb"\xd3\xe6\x8e\x06(..)\x26\x83\xbc(..)\x00", re.DOTALL)

# shl si,cl / mov es,[segment variable] / mov ax,es:[si+entry] / mov [bp-8],ax / mov ax,1: the
# types that have a flag, with SI four times the flag's number; the flag's bit is two before
_FLAG_TABLE = re.compile(rb"\xd3\xe6\x8e\x06(..)\x26\x8b\x84(..)\x89\x46.\xb8[\x01\x06]\x00", re.DOTALL)

# mov cl,2 / shl si,cl / mov al,[bp+6] / mov es,[segment variable] / cmp es:[si+rows],al: is
# this row of the exits the direction asked for?
_EXIT_ROWS = re.compile(rb"\xb1\x02\xd3\xe6\x8a\x46\x06\x8e\x06(..)\x26\x38\x84(..)", re.DOTALL)

# shl bx,1 / shl bx,1 / mov es,[segment variable] / mov ax,es:[bx+doors] / mov [bp-n],ax /
# mov ax,flag / push ax: a door's own object, to be asked if it's open, which the room it
# leads to is two before
_DOORS = re.compile(rb"\xd1\xe3\xd1\xe3\x8e\x06(..)\x26\x8b\x87(..)\x89\x46.\xb8..\x50", re.DOTALL)

# the same, with mov dx,es:[bx+texts+2] and both kept, and a jump: the text an exit says
_EXIT_TEXTS = re.compile(
    rb"\xd1\xe3\xd1\xe3\x8e\x06(..)\x26\x8b\x87(..)\x26\x8b\x97..\x89\x46.\x89\x56.\xeb", re.DOTALL
)

# mov cl,3 / shl si,cl / mov es,[segment variable] / mov bx,es:[si+rows+2] / cmp byte [bx],0:
# the variable that says if an exit is open, which the room it leads to is two before
# and then je <shut> / mov ax,es:[si+rows]: the room, which is where a row starts
_EXIT_VARIABLES = re.compile(
    rb"\xb1\x03\xd3\xe6\x8e\x06(..)\x26\x8b\x9c(..)\x80\x3f\x00\x74.\x26\x8b\x84(..)", re.DOTALL
)

# mov cl,2 / shl si,cl / mov es,[segment variable] / mov ax,es:[si+rows] / and ah,7Fh: a row
# of a room's list in Spellcasting 301, where the last of a room's rows has its top bit set
_ROOM_ROW_MARKED = re.compile(rb"\xb1\x02\xd3\xe6\x8e\x06(..)\x26\x8b\x84(..)\x80\xe4\x7f", re.DOTALL)

# Microsoft C's notice is 8 bytes into the segment DS has
_NOTICE = b"MS Run-Time Library"
# the segment of a far pointer that is a string's number and not a place in memory
_STRING_SEGMENT = 0xF000


@dataclass(frozen=True)
class Layout:
    """Where a type's record has what, after the 14 bytes every record starts with."""

    size: int
    flag_bytes: int = 2  # the flags, which come after the names
    names: int = 8  # the names' size: two far pointers, or two words
    places: int | None = None  # the objects it's in, a word each
    relation: int | None = None  # a byte: which of its parent's lists it's on
    next: int | None = None  # the next object on that list
    lists: int | None = None  # the first object on each of its own lists, a word each
    exit_count: int | None = None  # a room's exits: a byte, and a far pointer to them
    exits: int | None = None
    exit_start: int | None = None  # or the number of its first row in the table of exits
    also_here: int | None = None  # a room's: where its row starts in the list of what's also there
    seen_from: int | None = None  # and in the list of the places that can be seen into from it
    routine: int | None = None  # the number of its routine, in a game that numbers them


# Spellcasting 101's, from its routines that read and write each field for each type
LAYOUTS = {
    1: Layout(28, lists=0x0E, exit_count=0x16, exits=0x18, also_here=0x12, seen_from=0x14),
    2: Layout(36, flag_bytes=6, places=0x12, relation=0x14, next=0x16, lists=0x18),
    3: Layout(14),
    4: Layout(22, lists=0x0E),
    5: Layout(22, lists=0x0E),
    6: Layout(26, places=0x0E, lists=0x12),
    7: Layout(32, places=0x0E, relation=0x10, next=0x12, lists=0x14),
    8: Layout(22, places=0x0E, relation=0x10, next=0x12),
}


# Spellcasting 201's, from the same routines there: each field four bytes sooner, with the
# names two words, a thing with a third list, and a room's exits a row's number
LAYOUTS_201 = {
    1: Layout(24, names=4, lists=0x0A, also_here=0x10, seen_from=0x12, exit_count=0x14, exit_start=0x16),
    2: Layout(34, flag_bytes=6, names=4, places=0x0E, relation=0x10, next=0x12, lists=0x14),
    3: Layout(10, names=4),
    4: Layout(18, names=4, lists=0x0A),
    5: Layout(18, names=4, lists=0x0A),
    6: Layout(22, names=4, places=0x0A, lists=0x0E),
    7: Layout(28, names=4, places=0x0A, relation=0x0C, next=0x0E, lists=0x10),
    8: Layout(18, names=4, places=0x0A, relation=0x0C, next=0x0E),
}


# Timequest's, from the same routines there, between the two: the names still far pointers,
# the flags four bytes for all but a thing and the types that are only words, and a room's
# exits already a row's number
LAYOUTS_TIMEQUEST = {
    1: Layout(28, flag_bytes=4, lists=0x10, also_here=0x14, seen_from=0x16, exit_count=0x18, exit_start=0x1A),
    2: Layout(36, flag_bytes=6, places=0x12, relation=0x14, next=0x16, lists=0x18),
    3: Layout(14),
    4: Layout(22, lists=0x0E),
    5: Layout(24, flag_bytes=4, lists=0x10),
    6: Layout(28, flag_bytes=4, places=0x10, lists=0x14),
    7: Layout(34, flag_bytes=4, places=0x10, relation=0x12, next=0x14, lists=0x16),
    8: Layout(24, flag_bytes=4, places=0x10, relation=0x12, next=0x14),
}


# Spellcasting 301's: Spellcasting 201's, with a word more at each record's end but a
# room's, which is the number of the object's routine, and four bytes more in a thing's
LAYOUTS_301 = {
    1: Layout(
        24,
        names=4,
        lists=0x0A,
        also_here=0x10,
        seen_from=0x12,
        exit_count=0x14,
        exit_start=0x16,
        routine=0x0E,
    ),
    2: Layout(40, flag_bytes=6, names=4, places=0x0E, relation=0x10, next=0x12, lists=0x14, routine=0x26),
    3: Layout(12, names=4, routine=0x0A),
    4: Layout(20, names=4, lists=0x0A, routine=0x12),
    5: Layout(20, names=4, lists=0x0A, routine=0x12),
    6: Layout(24, names=4, places=0x0A, lists=0x0E, routine=0x16),
    7: Layout(30, names=4, places=0x0A, relation=0x0C, next=0x0E, lists=0x10, routine=0x1C),
    8: Layout(20, names=4, places=0x0A, relation=0x0C, next=0x0E, routine=0x12),
}


# Gateway's: Spellcasting 301's with four bytes of flags, as Timequest has, for a room, a
# person and the two kinds of scenery that had two; and a thing without the four bytes that
# game added
LAYOUTS_GATEWAY = {
    1: Layout(
        26,
        flag_bytes=4,
        names=4,
        lists=0x0C,
        also_here=0x12,
        seen_from=0x14,
        exit_count=0x16,
        exit_start=0x18,
        routine=0x10,
    ),
    2: Layout(36, flag_bytes=6, names=4, places=0x0E, relation=0x10, next=0x12, lists=0x14, routine=0x22),
    3: Layout(12, names=4, routine=0x0A),
    4: Layout(20, names=4, lists=0x0A, routine=0x12),
    5: Layout(22, flag_bytes=4, names=4, lists=0x0C, routine=0x14),
    6: Layout(24, names=4, places=0x0A, lists=0x0E, routine=0x16),
    7: Layout(32, flag_bytes=4, names=4, places=0x0C, relation=0x0E, next=0x10, lists=0x12, routine=0x1E),
    8: Layout(24, flag_bytes=4, names=4, places=0x0C, relation=0x0E, next=0x10, routine=0x16),
}


# Eric the Unready's: Gateway's, with a room as Spellcasting 301 has it, two bytes of flags
# and no more; a door two bytes longer, for four bytes of flags; and something fixed in a
# room two bytes shorter
LAYOUTS_ERIC = {
    **LAYOUTS_GATEWAY,
    1: LAYOUTS_301[1],
    6: Layout(26, flag_bytes=4, names=4, places=0x0C, lists=0x10, routine=0x18),
    8: Layout(22, flag_bytes=4, names=4, places=0x0C, relation=0x0E, next=0x10, routine=0x14),
}


# the directions an exit can be in, by number, from the main loop's own tests of the word typed
DIRECTIONS = (
    "",
    "north",
    "northeast",
    "east",
    "southeast",
    "south",
    "southwest",
    "west",
    "northwest",
    "up",
    "down",
    "in",
    "out",
)


@dataclass(frozen=True)
class Exit:
    """A row of a room's exits: a direction, a kind, and a far pointer or, later, a word."""

    direction: int
    kind: int
    segment: int
    offset: int
    value: int | None = None  # Spellcasting 201's word: a room, a door's row, or a routine's number


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
    flags: int  # the first 16 bits after the names: flag 1 is the lowest
    flag_count: int = 16
    flags_at: int = 8  # where the flags start in the record
    # the bit each flag is, for the flags its type has, when the program keeps a table of them
    flag_bits: dict[int, int] | None = None
    nouns: list[int] = field(default_factory=list)  # the words that name it, by number
    adjectives: list[int] = field(default_factory=list)  # the words that tell it from others
    places: list[int] = field(default_factory=list)
    relation: int | None = None
    next: int | None = None
    lists: list[int] = field(default_factory=list)
    exits: list[Exit] = field(default_factory=list)
    # a room's: objects that are there without being in it, each with whether it is for now,
    # and the places whose contents can be seen from it, the same
    also_here: list[tuple[int, bool]] = field(default_factory=list)
    seen_from: list[tuple[int, bool]] = field(default_factory=list)
    routine: int = 0  # the number of its routine, from 1, in a game that numbers them; 0 for none

    def word(self, at: int) -> int:
        return struct.unpack_from("<H", self.record, at)[0]

    def has_flag(self, flag: int) -> bool:
        """Flags are numbered from 1, eight to a byte from each byte's low bit, or by the table."""
        bit = flag - 1
        if self.flag_bits is not None:
            if flag not in self.flag_bits:
                return False
            bit = self.flag_bits[flag]
        at = self.flags_at + bit // 8
        return at < len(self.record) and bool(self.record[at] >> (bit % 8) & 1)


@dataclass(frozen=True)
class TypeEntry:
    """A type's entry in the table of types."""

    type: int
    places: int
    lists: int
    size: int


class ObjectTable:
    """The objects, read from the program's memory as it is before the game changes it."""

    def __init__(self, program: Program, words: list[Word] | None = None, names: bytes | None = None):
        self.words = words
        # the game's file of text, for the strings its tables name by number: given after
        self.strings: StringFile | None = None
        # OBJECT.DAT's text: its size in a word, then each name and a zero
        self._names = names[2 : 2 + struct.unpack_from("<H", names)[0]] if names else b""
        self.image = program.image()
        memory = self.image.memory
        root = memory[: program.root_size]
        notice = memory.find(_NOTICE)
        if notice < 0:
            raise ValueError("the program's data segment wasn't found")
        self.data_segment = (notice - 8) // 16

        # a later compiler's code, as Gateway II has, keeps the count where DS reaches it and
        # finds the tables' entries by other registers
        self.near = not _COUNT.search(root) and bool(_COUNT_NEAR.search(root))
        table_variable, table_offset = self._commonest(_TABLE_BX if self.near else _TABLE, root)
        table_offset = self._signed(table_offset) + 6
        count_variable, count_offset = (
            (0, self._commonest(_COUNT_NEAR, root)[0]) if self.near else self._commonest(_COUNT, root)
        )
        # the entries are read a byte at a time, so the lowest offset read is the table's start
        types = _TYPES_BX if self.near else _TYPES
        entries = collections.Counter(match.groups() for match in types.finditer(root))
        types_variable = self._commonest(types, root)[0]
        types_offset = min(
            self._signed(int.from_bytes(offset, "little"))
            for variable, offset in entries
            if int.from_bytes(variable, "little") == types_variable
        )

        # the two lists of words: mov es,[variable] / mov si,es:[bx+list] / or si,si, for the
        # nouns and then for the adjectives. The code is the parser's, which can be in an overlay
        lists = self._pairs(_WORD_LIST, root)
        marked: list[tuple[int, int]] = []
        for overlay in program.overlays:
            start, end = overlay.load_segment * 16, overlay.end_segment * 16
            code = program.image(overlay.number).memory[start:end]
            if len(lists) < 2:
                lists = self._pairs(_WORD_LIST, code)
            if len(marked) < 2:
                marked = self._pairs(_ROOM_ROW_MARKED, code)
        self._word_lists = [self._data_word(variable) * 16 + offset for variable, offset in lists[:2]]

        # the two lists a room has a row in, in the order the code that finds objects reads
        # them, or, where a routine searches each, in the routines' order
        rows = self._pairs(_ROOM_SEARCH, root)
        if len(rows) < 2:
            rows = self._pairs(_ROOM_ROW, root)
        # or, in Spellcasting 301, by the code in an overlay that goes down each, whose rows
        # end at one with its top bit set and not at a zero
        self._rows_marked = len(marked) >= 2
        if self._rows_marked:
            rows = marked
        self._room_rows = [self._data_word(variable) * 16 + offset for variable, offset in rows][:2]

        self.table_segment = self._data_word(table_variable)
        self.count = (
            self._data_word(count_offset)
            if self.near
            else self._word(self._data_word(count_variable), count_offset)
        )
        types_segment = self._data_word(types_variable)
        table = self.table_segment * 16 + table_offset
        self._find_flags(root, table)
        self._find_exits(root)

        self.types: dict[int, TypeEntry] = {}
        entries = [table + 6 * (number - 1) for number in range(1, self.count + 1)]
        for kind in sorted({memory[entry] for entry in entries}):
            at = types_segment * 16 + ((types_offset + 3 * kind) & 0xFFFF)
            self.types[kind] = TypeEntry(kind, *memory[at : at + 3])
        # one game's layouts or the other's, and only when every type is the size it has there:
        # a game with a type of another size keeps its records another way
        self._layouts: dict[int, Layout] = next(
            (
                layouts
                for layouts in (
                    LAYOUTS,
                    LAYOUTS_TIMEQUEST,
                    LAYOUTS_201,
                    LAYOUTS_301,
                    LAYOUTS_GATEWAY,
                    LAYOUTS_ERIC,
                )
                if all(
                    kind in layouts and layouts[kind].size == entry.size for kind, entry in self.types.items()
                )
            ),
            {},
        )
        # whose layouts they are, which says whose names its flags have
        self.game = (
            "Spellcasting 101"
            if self._layouts is LAYOUTS
            else "Timequest"
            if self._layouts is LAYOUTS_TIMEQUEST
            else "Spellcasting 201"
            if self._layouts is LAYOUTS_201
            else "Spellcasting 301"
            if self._layouts is LAYOUTS_301
            else "Gateway"
            if self._layouts is LAYOUTS_GATEWAY
            else "Eric the Unready"
            if self._layouts is LAYOUTS_ERIC
            else None
        )
        self.objects: list[GameObject] = []
        for number, entry in enumerate(entries, 1):
            offset, segment = struct.unpack_from("<HH", memory, entry + 2)
            self.objects.append(self._read(number, memory[entry], segment, offset))

    def _find_exits(self, root: bytes) -> None:
        """The one table of exits, where a game has one, with the tables its kinds of exit use."""
        self._exit_rows = self._doors = None
        doors = _DOORS.search(root)
        if doors:
            self._doors = self._data_word(int.from_bytes(doors[1], "little")) * 16
            self._doors += int.from_bytes(doors[2], "little") - 2
        # the timers' table is read the same way: the exits' is the one read nearest the doors'
        exits = [
            (abs(match.start() - (doors.start() if doors else 0)), match)
            for match in _EXIT_ROWS.finditer(root)
        ]
        if exits:
            _, nearest = min(exits, key=lambda found: found[0])
            self._exit_rows = self._data_word(int.from_bytes(nearest[1], "little")) * 16
            self._exit_rows += int.from_bytes(nearest[2], "little")
        self._exit_texts = self._exit_variables = None
        self._exit_variable_at = 2
        if self._exit_rows is not None:
            if texts := self._pairs(_EXIT_TEXTS, root):
                self._exit_texts = self._data_word(texts[0][0]) * 16 + texts[0][1]
            if opened := _EXIT_VARIABLES.search(root):
                segment = self._data_word(int.from_bytes(opened[1], "little")) * 16
                self._exit_variables = segment + int.from_bytes(opened[3], "little")
                # how far into a row the variable is: 2 in Timequest, with the text's far
                # pointer after it, and 4 in Gateway, with the number of a text after it
                rows, variable = int.from_bytes(opened[3], "little"), int.from_bytes(opened[2], "little")
                self._exit_variable_at = variable - rows

    @staticmethod
    def _pairs(pattern: re.Pattern[bytes], code: bytes) -> list[tuple[int, int]]:
        """Each different variable and offset a pattern finds, in the order they're found."""
        found = dict.fromkeys(
            (int.from_bytes(match[1], "little"), int.from_bytes(match[2], "little"))
            for match in pattern.finditer(code)
        )
        return list(found)

    def _find_flags(self, root: bytes, table: int) -> None:
        """The table of flags, where there is one: the bit each is, and the types that have it."""
        self.flag_table: dict[int, tuple[int, int]] | None = None
        found = _FLAG_TABLE.search(root)
        if not found:
            return
        segment = self._data_word(int.from_bytes(found[1], "little"))
        start = segment * 16 + int.from_bytes(found[2], "little") - 2
        self.flag_table = {}
        # the table of objects comes after it, and nothing says where the flags end but that
        for flag in range(1, 65):
            at = start + 4 * flag
            if start < table <= at:
                break
            self.flag_table[flag] = (self.image.memory[at], self.image.word(at + 2))

    def door(self, row: int) -> tuple[int, int] | None:
        """A door's row: the room it leads to, and the door's own object."""
        if self._doors is None:
            return None
        return self.image.word(self._doors + 4 * row), self.image.word(self._doors + 4 * row + 2)

    def string_named(self, block: int, string: int) -> str:
        """A string of the game's file of text, when it has been given to look in."""
        if self.strings and block < len(self.strings.blocks) and string < self.strings.blocks[block].strings:
            return self.strings.strings(self.strings.blocks[block])[string]
        return f"(string {block}.{string})"

    def exit_text(self, row: int) -> str | None:
        """What an exit that only says something says."""
        if self._exit_texts is None:
            return None
        offset, segment = struct.unpack_from("<HH", self.image.memory, self._exit_texts + 4 * row)
        return self._text(segment, offset)

    def exit_variable(self, row: int) -> tuple[int, int, str | None] | None:
        """An exit a variable opens: the room it leads to, the variable, and what it says shut."""
        if self._exit_variables is None:
            return None
        row_at = self._exit_variables + 8 * row
        if self._exit_variable_at == 2:
            room, variable, offset, segment = struct.unpack_from("<HHHH", self.image.memory, row_at)
            return room, variable, self._text(segment, offset)
        # the room, a word not understood, the variable, and which of the exits' texts, from 1
        room, _, variable, text = struct.unpack_from("<HHHH", self.image.memory, row_at)
        return room, variable, self.exit_text(text - 1) if text else None

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
        if segment == _STRING_SEGMENT:
            # not a pointer but a string's number, as Spellcasting 301 and after name them:
            # the block above ten bits, and the string's place in it
            return self.string_named(offset >> 10, offset & 0x3FF)
        memory = self.image.memory
        start = segment * 16 + offset
        return memory[start : memory.index(b"\0", start)].decode("latin-1")

    def _name(self, start: int) -> str | None:
        """A name from OBJECT.DAT's text, by where it starts there, counted from 1."""
        if not 0 < start <= len(self._names):
            return None
        return self._names[start - 1 : self._names.index(b"\0", start - 1)].decode("latin-1")

    def _names_of(self, record: bytes, layout: Layout | None) -> tuple[str, str | None]:
        """A record's two names: by far pointers into the program, or by words into OBJECT.DAT."""
        if layout is not None and layout.names == 4:
            first, second = struct.unpack_from("<HH", record)
            return self._name(first) or "", self._name(second)
        name_offset, name_segment, second_offset, second_segment = struct.unpack_from("<HHHH", record)
        return self._text(name_segment, name_offset) or "", self._text(second_segment, second_offset)

    def _read(self, number: int, kind: int, segment: int, offset: int) -> GameObject:
        memory = self.image.memory
        start = segment * 16 + offset
        size = self.types[kind].size
        record = memory[start : start + size]
        layout = self._layouts.get(kind)
        name, second_name = self._names_of(record, layout)
        found = GameObject(number, kind, segment, offset, record, name, second_name, 0)
        if layout is None:
            found.flags = found.word(8)
            return found
        found.flags_at = layout.names
        found.flags = found.word(layout.names)
        found.flag_count = 8 * layout.flag_bytes
        if self.flag_table is not None:
            found.flag_count = len(self.flag_table)
            found.flag_bits = {
                flag: bit for flag, (bit, kinds) in self.flag_table.items() if kinds >> (kind - 1) & 1
            }
        if len(self._word_lists) == 2:
            # after the flags, where its nouns start in the list of all nouns, counted from 1,
            # and the same for its adjectives; each object's words end at a zero
            at = layout.names + layout.flag_bytes
            found.nouns = self._list(self._word_lists[0], found.word(at))
            found.adjectives = self._list(self._word_lists[1], found.word(at + 2))
        entry = self.types[kind]
        found.routine = found.word(layout.routine) if layout.routine else 0
        if layout.places is not None:
            found.places = [found.word(layout.places + 2 * index) for index in range(entry.places)]
        if layout.relation is not None:
            found.relation = record[layout.relation]
        if layout.next is not None:
            found.next = found.word(layout.next)
        if layout.lists is not None:
            found.lists = [found.word(layout.lists + 2 * index) for index in range(entry.lists)]
        if layout.also_here is not None and layout.seen_from is not None and len(self._room_rows) == 2:
            found.also_here = self._row(self._room_rows[0], found.word(layout.also_here))
            found.seen_from = self._row(self._room_rows[1], found.word(layout.seen_from))
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
        if layout.exit_start is not None and layout.exit_count is not None and self._exit_rows is not None:
            at = self._exit_rows + 4 * (found.word(layout.exit_start) - 1)
            for row in range(record[layout.exit_count] if found.word(layout.exit_start) else 0):
                direction, way, value = struct.unpack_from("<BBH", memory, at + 4 * row)
                found.exits.append(Exit(direction, way, 0, 0, value))
        return found

    def _row(self, table: int, start: int) -> list[tuple[int, bool]]:
        """A room's row of a list: four bytes each, an object and whether it counts, to a zero."""
        found = []
        at = table + 4 * (start - 1)
        while start and (number := self.image.word(at)):
            found.append((number & 0x7FFF if self._rows_marked else number, self.image.memory[at + 2] != 0))
            at += 4
            if self._rows_marked and number & 0x8000:
                break
        return found

    def _list(self, table: int, start: int) -> list[int]:
        found = []
        at = table + 2 * (start - 1)
        while start and (word := self.image.word(at)):
            found.append(word & 0x7FFF if self._rows_marked else word)
            at += 2
            if self._rows_marked and word & 0x8000:
                break
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
# the same with the loop's own variable near: cmp [bp-n],ax with n in a byte
_LOOP_END_NEAR = re.compile(rb"\x8e\x06(..)\x26\xa1(..)\x39\x46", re.DOTALL)
# mov ax,1 / push ax / mov ax,<size> / push ax / mov ax,<offset> / mov dx,<segment> / push dx /
# push ax / push cs / call: one thing of a size written, which before the objects is what
# starts the file
_HEADER = re.compile(rb"\xb8\x01\x00\x50\xb8(..)\x50\xb8..(?:\xba..\x52|\x1e)\x50\x0e\xe8", re.DOTALL)
# push es:[count] / mov ax,<size> / push ax / mov ax,<offset> / mov dx,<segment> / push dx /
# push ax / push cs / call: the timers written, a count of them and the size of one
_TIMER_ROWS = re.compile(rb"\x26\xff\x36(..)\xb8(..)\x50\xb8..\xba..\x52\x50\x0e\xe8", re.DOTALL)
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
    What the program saves, which is what RESTART.DAT has: some bytes that start it, every
    object's record in the table's order, each saved variable, a count, and some bytes for
    each it counts, which are the timers. Spellcasting 101 starts with four bytes and has six
    for a timer; Spellcasting 201 starts with 34 and has four. Spellcasting 301 is as that
    is, with four words more before the count, and the code that saves it is in an overlay.
    """

    def __init__(self, program: Program, objects: ObjectTable):
        memory = objects.image.memory

        def word(code: bytes) -> int:
            return int.from_bytes(code, "little")

        # the code that saves is in the root, or in an overlay
        root = memory[: program.root_size]
        saved = _SAVED.search(root)
        for overlay in program.overlays:
            if saved:
                break
            start, end = overlay.load_segment * 16, overlay.end_segment * 16
            root = program.image(overlay.number).memory[start:end]
            saved = _SAVED.search(root)
        timers = _TIMERS.search(root)
        if not saved or not timers:
            raise ValueError("the code that saves the game wasn't found")
        table_segment = objects.image.word(objects.data_segment * 16 + word(saved[1]))
        table = table_segment * 16 + word(saved[4])
        self.timer_count_offset = word(timers[1])
        # what starts the file is written just before the objects, where a size for it is found
        header = list(_HEADER.finditer(root, max(0, saved.start() - 256), saved.start()))
        self.header_size = word(header[-1][1]) if header else 4
        rows = _TIMER_ROWS.search(root, saved.end())
        # words written between the variables and the timers: the last is the timers' count,
        # and any before it are more of the game's state
        self.extra_size = 0
        if rows:
            words = list(_TIMERS.finditer(root, saved.end(), rows.start()))
            if words and word(rows[1]) == word(words[-1][1]):
                self.timer_count_offset = word(rows[1])
                self.extra_size = 2 * (len(words) - 1)
        self.timer_size = word(rows[2]) if rows and word(rows[1]) == self.timer_count_offset else 6

        # the loop that isn't the objects' or the timers' is the saved variables': the one
        # that ends just after the variables are written, or the only other one there is
        near = _LOOP_END_NEAR.search(root, saved.end(), saved.end() + 64)
        counts = {
            (word(match[1]), word(match[2]))
            for match in ([near] if near else _LOOP_END.finditer(root))
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
        fixed = self.header_size + self.objects_size + self.variables_size + self.extra_size
        return fixed + 2 + self.timer_size * timers

    def timers_in(self, file: bytes) -> int:
        """The count of timers in a saved state, which is the word after the variables."""
        at = self.header_size + self.objects_size + self.variables_size + self.extra_size
        return struct.unpack_from("<H", file, at)[0]

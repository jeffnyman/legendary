"""Mission Critical's objects and verbs, from its 32-bit program as it starts.

The world is the earlier games' world of objects, kept as Companions of Xanth keeps it, in
tables that are in the program's code object, with the records they point to in its data.

Three tables stand one after another. The first has three bytes for each of six types: how
many places an object of the type can be in, how many lists of other objects it can hold, and
its record's size. The second has three bytes for each of twenty flags: which bit it is in
a record's two bytes of flags, and a word with a bit for each type that has it. The third has
five bytes for each object, numbered from 1: its type, and the address of its record. A word
after it is how many objects there are.

A record of types 1 to 5 starts with two words, where the object's name and its second name
start in OBJECT.DAT, counted from 1, and its two bytes of flags. What follows is the type's
own, and is in `LAYOUTS`. A type 6 is two bytes, the number of a routine, and nothing else.

A room has up to four views, four bytes each: the direction faced, and a picture as a
file's number and the picture's number in it. Its exits are rows of a table of ten bytes
each: the direction, a kind, 1 for a room and 4 for a routine, the room or the routine's
number, and for some the direction faced on arriving.

The verbs are a table of eleven bytes each: the address of the verb's name, of the word that
goes between its two objects or nothing, how many objects it takes, and the number of the
action it makes. An object names actions, not verbs: the one a click on it does, and a list
of those it offers, which is words up to a zero in one table for every object, the object
holding where its own starts. After the verbs are the actions' routines, six bytes each: an
action's number and the address of its routine.

The tables are found by the code that reads them. The layouts are from the routines that
get and set each field for each type, at `00013890` to `00014430`.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass, field

from .le import LeExecutable

# mov al,[esi+table] / test eax,eax / jle / dec eax / mov ebx,[ebp-8] / lea eax,[eax+eax*2] /
# xor edx,edx / mov ecx,edi / mov dl,[eax+sizes] / mov eax,[esi+table+1]: saving each record
_SAVE = re.compile(
    rb"\x8a\x86(....)\x85\xc0\x7e.\x48\x8b\x5d.\x8d\x04\x40\x31\xd2\x89\xf9\x8a\x90(....)\x8b\x86....",
    re.DOTALL,
)
# mov ax,[count]: after it, how many objects the loop goes to
_COUNT = re.compile(rb"\x66\xa1(....)", re.DOTALL)
# mov al,[ebx+exits] / mov [esp+n],al / xor eax,eax / mov dl,[ebx+exits+1] / mov ax,[ebx+exits+2]
_EXITS = re.compile(rb"\x8a\x83(....)\x88\x44\x24.\x31\xc0\x8a\x93....\x66\x8b\x83....", re.DOTALL)
# add eax,eax / sub eax,2 / add eax,<lists>: where an object's list of actions starts
_ACTION_LISTS = re.compile(rb"\x01\xc0\x83\xe8\x02\x05(....)", re.DOTALL)

# what each flag is, where the program's use of it says. 1 to 4 are how a name is written and
# 19 picks `herself`; Open asks 12 and sets 16 and 5, and Close clears 16; Look in asks 13 and
# 16; 17 is every room and 20 both people. 10 is tested, set and cleared
# by the objects' own routines and nothing else, as the game before's flag 10 is
FLAG_NAMES = {
    1: "takes some",
    2: "its name takes no article",
    3: "plural",
    4: "its name takes an",
    5: "touched",
    6: "seen",
    10: "its routine's own mark",
    12: "can be opened and closed",
    13: "can be seen through",
    16: "open",
    17: "a room",
    18: "things can be on it",
    19: "female",
    20: "a person",
}

# call <test>, <set> or <clear> with the flag's number just put in EDX
_FLAG_USE = re.compile(
    rb"\xba(.)\x00\x00\x00(?:\x31\xc0)?(?:\x66\xa1....|\x66\x8b.{1,6}?|\x89.|\xb8....)?\xe8(....)", re.DOTALL
)

# mov ebx,[count] / test ebx,ebx / jle / lea eax,[ebx*8] / ... / mov edx,[esi+table+4] /
# mov eax,[esi+table]: saving each variable, by a table of eight bytes each, where and how big
_VARIABLES = re.compile(
    rb"\x8b\x1d(....)\x85\xdb\x7e.\x8d\x04\xdd\x00\x00\x00\x00.{0,12}?\x8b\x96....\x8b\x86(....)", re.DOTALL
)
# mov edx,[ebx] / mov eax,[ecx] / mov edx,[edx*8+names]: the screen of `Global Variables`, which
# has a table of eight bytes each, a variable's address and its name's
_NAMED = re.compile(rb"\x8b\x13\x8b\x01\x8b\x14\xd5(....)", re.DOTALL)

# add eax,<sets> / pop / pop / ret / xor eax,eax / mov ax,[edx+1] / add eax,ebx / add eax,<things> /
# pop / pop / ret / ... / add eax,<replies> / pop / pop / ret: a byte of state for each set of
# things to say, each thing in a set, and each thing said back
_TALK = re.compile(
    rb"\x05(....)\x59\x5b\xc3\x31\xc0\x66\x8b\x42\x01\x01\xd8\x05(....)\x59\x5b\xc3.{0,24}?\x05(....)\x59\x5b\xc3",
    re.DOTALL,
)
# xor eax,eax / mov ax,[room] / call / mov ebx,eax / mov ebp,eax / xor eax,eax / mov ax,[room]:
# the routine that goes through the exits of the room the player is in
_ROOM = re.compile(rb"\x31\xc0\x66\xa1(....)\xe8....\x89\xc3\x89\xc5\x31\xc0\x66\xa1\1", re.DOTALL)

TYPE_NAMES = {1: "room", 2: "thing", 3: "holder", 4: "fixture", 5: "person", 6: "routine alone"}


@dataclass(frozen=True)
class Layout:
    """Where a type's record has what."""

    place: int | None = (
        None  # the object it's in; then a byte, which of that one's lists; then the next on it
    )
    lists: int | None = None  # the first object on each of its own lists, a word each
    action: int | None = None  # the action a click does
    actions: int | None = None  # where its list of actions starts
    routine: int | None = None  # the number of its routine
    views: int | None = None  # a room's four views
    exit_count: int | None = None  # a room's exits: how many, a byte, and the first's row
    exit_start: int | None = None


LAYOUTS = {
    1: Layout(lists=0x06, views=0x08, routine=0x18, exit_count=0x1C, exit_start=0x1D),
    2: Layout(place=0x06, lists=0x0B, action=0x13, actions=0x15, routine=0x17),
    3: Layout(place=0x06, lists=0x0B, action=0x17, actions=0x19, routine=0x1B),
    4: Layout(lists=0x06, action=0x08, actions=0x0A, routine=0x0C),
    5: Layout(place=0x06, lists=0x0B, action=0x15, actions=0x17, routine=0x19),
    6: Layout(routine=0x00),
}


@dataclass(frozen=True)
class Exit:
    direction: int
    kind: int  # 1 a room, 4 a routine
    target: int
    facing: int


@dataclass
class Thing:
    """An object, as the program starts with it."""

    number: int
    type: int
    address: int
    record: bytes
    name: str = ""
    second_name: str = ""
    flags: int = 0
    place: int = 0
    relation: int = 0
    next: int = 0
    lists: list[int] = field(default_factory=list)
    action: int = 0
    actions: list[int] = field(default_factory=list)
    routine: int = 0
    views: list[tuple[int, int, int]] = field(default_factory=list)  # direction, file, picture
    exits: list[Exit] = field(default_factory=list)


@dataclass(frozen=True)
class Verb:
    number: int
    name: str
    between: str  # the word between its two objects
    objects: int
    action: int


class World:
    """The tables of Mission Critical's world, read from its program and OBJECT.DAT."""

    def __init__(self, program: LeExecutable, names: bytes):
        self.program = program
        code = next(each for each in program.objects if each.is_code)
        contents = program.contents(code)
        saved = _SAVE.search(contents)
        exits = _EXITS.search(contents)
        lists = _ACTION_LISTS.search(contents)
        count = _COUNT.search(contents, saved.end(), saved.end() + 80) if saved else None
        if not (saved and exits and lists and count):
            raise ValueError("the program's tables of objects weren't found")
        self.table, sizes = struct.unpack("<II", saved[1] + saved[2])
        self.types_at = sizes - 2
        self.count = self._word(struct.unpack("<I", count[1])[0])
        self.exits_at = struct.unpack("<I", exits[1])[0]
        self.lists_at = struct.unpack("<I", lists[1])[0]
        self._names = names[2:]
        entries = [
            struct.unpack("<BI", program.read(self.table + 5 * index, 5)) for index in range(self.count)
        ]
        kinds = max(kind for kind, _ in entries)
        # places, lists and size, for each type
        self.types = {
            kind: tuple(program.read(self.types_at + 3 * (kind - 1), 3)) for kind in range(1, kinds + 1)
        }
        # the flags are between the types and the objects: a bit, and the types that have it
        self.flags_at = self.types_at + 3 * kinds
        self.flags = [
            struct.unpack("<BH", program.read(self.flags_at + 3 * index, 3))
            for index in range((self.table - self.flags_at) // 3)
        ]
        self.objects = [
            self._thing(index + 1, kind, address) for index, (kind, address) in enumerate(entries)
        ]
        self.verbs, self.routines = self._verbs(contents, code.base)

    def _word(self, address: int) -> int:
        return struct.unpack("<H", self.program.read(address, 2))[0]

    def _name(self, start: int) -> str:
        return self._names[start - 1 :].split(b"\0")[0].decode("latin-1") if start else ""

    def _thing(self, number: int, kind: int, address: int) -> Thing:
        record = self.program.read(address, self.types[kind][2])
        found = Thing(number, kind, address, record)
        layout = LAYOUTS.get(kind)
        if layout is None:
            return found

        def word(at: int) -> int:
            return struct.unpack_from("<H", record, at)[0]

        if kind != 6:
            found.name, found.second_name, found.flags = self._name(word(0)), self._name(word(2)), word(4)
        if layout.place is not None:
            found.place, found.relation, found.next = (
                word(layout.place),
                record[layout.place + 2],
                word(layout.place + 3),
            )
        if layout.lists is not None:
            found.lists = [word(layout.lists + 2 * index) for index in range(self.types[kind][1])]
        if layout.action is not None:
            found.action = word(layout.action)
        if layout.actions is not None and word(layout.actions):
            at = self.lists_at + 2 * (word(layout.actions) - 1)
            while action := self._word(at):
                found.actions.append(action)
                at += 2
        if layout.routine is not None:
            found.routine = word(layout.routine)
        self._room(found, layout)
        return found

    def _room(self, found: Thing, layout: Layout) -> None:
        """A room's views and exits."""
        record = found.record

        def word(at: int) -> int:
            return struct.unpack_from("<H", record, at)[0]

        if layout.views is not None:
            for index in range(4):
                direction, picture = struct.unpack_from("<HH", record, layout.views + 4 * index)
                if direction:
                    found.views.append((direction, picture >> 8, picture & 0xFF))
        if layout.exit_start is not None and layout.exit_count is not None and word(layout.exit_start):
            for index in range(record[layout.exit_count]):
                row = self.program.read(self.exits_at + 10 * (word(layout.exit_start) - 1 + index), 10)
                found.exits.append(Exit(row[0], row[1], *struct.unpack_from("<HH", row, 2)))

    def get(self, number: int) -> Thing:
        if not 1 <= number <= self.count:
            raise ValueError(f"there is no object {number}: they go from 1 to {self.count}")
        return self.objects[number - 1]

    def has_flag(self, thing: Thing, flag: int) -> bool:
        """Whether an object has a flag; they are numbered from 1, as the program numbers them."""
        bit, kinds = self.flags[flag - 1]
        return bool(kinds >> (thing.type - 1) & 1 and thing.flags >> bit & 1)

    def saved_variables(self) -> list[tuple[int, int, str | None]]:
        """
        The variables the game saves, in the order it saves them: each one's address, its size,
        and its name, where the program has one for it or what reads it here says what it is.
        """
        code = next(each for each in self.program.objects if each.is_code)
        contents = self.program.contents(code)
        found, named = _VARIABLES.search(contents), _NAMED.search(contents)
        if not found:
            raise ValueError("the program's table of saved variables wasn't found")
        count_at, table = struct.unpack("<II", found[1] + found[2])
        (count,) = struct.unpack("<I", self.program.read(count_at, 4))
        names: dict[int, str] = {}
        if named:
            # the program's own names, for a screen its writers had to set and clear them
            at = struct.unpack("<I", named[1])[0] - 4
            while True:
                address, name = struct.unpack("<II", self.program.read(at, 8))
                text = self._text(name)
                if not text or self.program.object_at(address) is None:
                    break
                names[address] = text
                at += 8
        names.setdefault(self.lists_at, "the objects' lists of actions")
        if talk := _TALK.search(contents):
            sets, things, replies = struct.unpack("<III", talk[1] + talk[2] + talk[3])
            names.setdefault(sets, "the state of each set of things to say")
            names.setdefault(things, "the state of each thing to say")
            names.setdefault(replies, "the state of each thing said back")
        if room := _ROOM.search(contents):
            names.setdefault(struct.unpack("<I", room[1])[0], "the room the player is in")
        variables = []
        for index in range(count):
            address, size = struct.unpack("<II", self.program.read(table + 8 * index, 8))
            variables.append((address, size, names.get(address)))
        return variables

    def flag_uses(self) -> dict[int, list[int]]:
        """For each flag, how often the program tests, sets and clears it by its number."""
        code = next(each for each in self.program.objects if each.is_code)
        contents = self.program.contents(code)
        # the three routines: each asks for the flag's place, masks by its bit, and differs after
        routines = [
            code.base + found.start()
            for found in re.finditer(rb"\x53\x51\x83\xec\x04\x81\xe2\xff\xff\x00\x00", contents)
        ][:3]
        uses = {flag: [0, 0, 0] for flag in range(1, len(self.flags) + 1)}
        for found in _FLAG_USE.finditer(contents):
            target = code.base + found.end() + struct.unpack("<i", found[2])[0]
            if target in routines and found[1][0] in uses:
                uses[found[1][0]][routines.index(target)] += 1
        return uses

    def on_list(self, first: int) -> list[Thing]:
        """The objects on a list, from its first."""
        found = []
        while first and len(found) < self.count:
            found.append(self.get(first))
            first = found[-1].next
        return found

    def _text(self, address: int) -> str | None:
        """Text at an address in the data: None where there's none a zero ends."""
        if address == 0:
            return ""
        where = self.program.object_at(address)
        if where is None or where.is_code:
            return None
        text = self.program.read(address, 40).split(b"\0")[0]
        return text.decode("ascii") if text and all(0x20 <= byte < 0x7F for byte in text) else None

    def _verbs(self, contents: bytes, base: int) -> tuple[list[Verb], dict[int, int]]:
        """The verbs, found as the longest run of their records, and the actions' routines after them."""
        best: list[Verb] = []
        start = end = 0
        for fixup in self.program.fixups():
            at = first_at = fixup.at - base
            if not 0 <= at < len(contents) - 11 or start <= at < end:
                continue
            found = []
            while at + 11 <= len(contents):
                name, between, objects, action = struct.unpack_from("<IIBH", contents, at)
                first, second = self._text(name), self._text(between)
                if not first or second is None or objects > 2:
                    break
                found.append(Verb(len(found) + 1, first, second, objects, action))
                at += 11
            if len(found) > len(best):
                best, start, end = found, first_at, at
        routines = {}
        # the table after starts on a multiple of four
        end += -(base + end) % 4
        while end + 6 <= len(contents):
            action, routine = struct.unpack_from("<HI", contents, end)
            where = self.program.object_at(routine)
            if not 0 < action < 0x200 or where is None or not where.is_code:
                break
            routines[action] = routine
            end += 6
        return best, routines

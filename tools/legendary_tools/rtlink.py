"""A program linked by .RTLink/Plus (Pocket Soft), with its run-time library and overlays.

The MZ header describes almost nothing: a small stub that reads RTLINKST.COM, the linker's
startup code, and jumps into it. That code does what DOS would have done, from a format of
the linker's own. The image starts with a header:

    +00  the program's first instruction, offset then segment
    +04  its stack, offset then segment
    +08  a table of segments, and at +0A how many: each is where one starts, offset then
         segment
    +0C  a table of groups, and how many, in the same form
    +10  a table of symbols, and how many: eight bytes each, `R`, then a group, a segment
         and an offset
    +14  where the records start, and at +16 how many
    +1C  a run-time library: 1, its name, and at +26 a stamp its file has to have too
    +34  the linker's version: 019A for 4.10, 01F6 for 5.02, 0262 for 6.10
    +4A  in 6.10, a second table of symbols, and at +4C how many: it comes after the first
    +4E  the library's extension, RTL

The program isn't kept as it sits in memory. It is kept as records, one for each stretch of
a segment, and the segments are put where the table says. A record is numbers, each one to
four bytes long (the first byte's top bits say how many more follow, most significant first):

    the segment's number, from 1
    how many bytes
    how far into the segment
    1 when the bytes follow, 0 when the stretch is only cleared
    the bytes
    how many lists of fixups, and the lists

A list of fixups starts with a word of flags. For the plain kind, flags 1000, a count and
that many places follow, and each place is a word that holds a segment: what a relocation is
in an MZ header. Every fixup in the program itself is that kind. The library's are the other
kind, flags with bit 0, which name a frame and a target by number and say what to put at each
place: the library was linked apart from the program, so its fixups do the linking.

The library, S201.RTL, holds the C run-time and the code the games share, as records of the
same kind that go by the program's own table of segments.

Overlays work as PLINK86's do. The segment the program starts in is the overlay manager's.
It has a table of the overlays, 18 bytes each, and after it the thunks, to the segment's end:

    call <the manager>         E8 oo oo
    jmp far <the routine>      EA oo oo ss ss
    dw <the overlay's number>  from 0

Each overlay is in the .OVL as PLINK86's are: its relocations, then its contents on a
paragraph.

Version 5.02, 01F6 at +34, keeps a program the same way: the header, the records and the
fixups' flags are 4.10's. Its overlay manager is another: the entries of its table start
six or eight bytes after the count, not four, and its thunks come after its segment, in segments of
its group, where 4.10's end it.

Every segment here is relative to where the program loads, as the file has them.
"""

from __future__ import annotations

import struct
from collections import Counter
from dataclasses import dataclass

from .mz import MzExecutable
from .program import Overlay, Program, Thunk

# how the stub begins: call <find the startup code> / jmp far cs:[4]
_ENTRY = bytes.fromhex("e805002eff2e0400")
# the versions read here, 4.10, 5.02 and 6.10: the first two's files are the same, and their
# managers differ; the third's has a second table of symbols
_VERSIONS = (0x19A, 0x1F6, 0x262)
_SECOND_SYMBOLS = 0x262  # the version that first has a second table of symbols
_LIBRARY_MAGIC = 0x37BA
_ENTRY_SIZE = 18  # of an overlay's entry in the manager's table
_THUNK_SIZE = 10

# a list of fixups' flags
_NAMED = 0x0001  # names a frame and a target, and isn't a plain relocation
_FROM_FRAME = 0x0002  # the value is counted from the frame, not from the place itself
_KIND = 0x001C  # what is put there: 0 a byte, 1 an offset, 2 a segment, 3 both, 4 a high byte
_ADDEND = 0x0020  # a number to add comes before the places
_TARGET = 0x01C0  # what the target's number is of
_FRAME = 0x0E00  # and the frame's: 0 a segment, 1 a group, 2 a symbol, 3 a segment's value, 4 a
# symbol of the second table
_MANY = 0x1000  # a count of places follows, and not one place
_SHIFTED = 0x4000  # the word there is shifted up twelve bits before the segment is added
_ABSOLUTE = 3
_SECOND_SYMBOL = 4  # a number in the second table of symbols


class _Reader:
    """The linker's numbers, read one after another."""

    def __init__(self, data: bytes, at: int):
        self.data = data
        self.at = at

    def number(self) -> int:
        first = self.data[self.at]
        self.at += 1
        if not first & 0x80:
            return first
        # 10xxxxxx has one byte after it, 110xxxxx two and 1110xxxx three
        more = 1 if not first & 0x40 else 2 if not first & 0x20 else 3
        if more == 3 and first & 0x10:
            raise ValueError(f"a number at {self.at - 1:#x} isn't in a form .RTLink writes")
        value = first & (0xFF >> (more + 1))
        for byte in self.take(more):
            value = value << 8 | byte
        return value

    def word(self) -> int:
        return int.from_bytes(self.take(2), "little")

    def take(self, count: int) -> bytes:
        if self.at + count > len(self.data):
            raise ValueError("a record goes past the end of its file")
        self.at += count
        return self.data[self.at - count : self.at]


@dataclass(frozen=True)
class Fixups:
    """One list of fixups: what they are, and the places, counted from the record's start."""

    flags: int
    frame: int  # the frame's number and the target's, when the flags say they are named
    target: int
    addend: int
    places: tuple[int, ...]

    @property
    def named(self) -> bool:
        return bool(self.flags & _NAMED)


@dataclass(frozen=True)
class Record:
    """A stretch of a segment: its bytes, or None when it is only cleared, and its fixups."""

    segment: int  # the segment's number, from 1
    offset: int
    length: int
    data: bytes | None
    fixups: tuple[Fixups, ...]


def _fixups(reader: _Reader) -> list[Fixups]:
    """One list's fixups: one with an addend can be several, each with its own places."""
    flags = reader.word()
    frame = target = 0
    if flags & _NAMED:
        frame, target = reader.number(), reader.number()
    found = []
    for _ in range(reader.number() if flags & _ADDEND and flags & _MANY else 1):
        addend = 0
        many = flags & _MANY
        if flags & _ADDEND:
            number = reader.number()
            many, addend = number & 1, number >> 1
        places = tuple(reader.number() for _ in range(reader.number() if many else 1))
        found.append(Fixups(flags, frame, target, addend, places))
    return found


def read_records(data: bytes, at: int, count: int) -> tuple[list[Record], int]:
    """The records from a place in a file, and where they end."""
    reader = _Reader(data, at)
    records = []
    for _ in range(count):
        segment, length, offset, kept = (reader.number() for _ in range(4))
        contents = reader.take(length) if kept else None
        fixups: list[Fixups] = []
        for _ in range(reader.number()):
            fixups += _fixups(reader)
        records.append(Record(segment, offset, length, contents, tuple(fixups)))
    return records, reader.at


def is_rtlink(exe: bytes) -> bool:
    """Whether a program starts in .RTLink's stub."""
    try:
        mz = MzExecutable(exe)
    except ValueError:
        return False
    entry = mz.cs * 16 + mz.ip
    return mz.image[entry : entry + len(_ENTRY)] == _ENTRY


def library_name(exe: bytes) -> str | None:
    """The run-time library a .RTLink program names, or None when it names none."""
    image = MzExecutable(exe).image
    if image[0x1C:0x1D] != b"\x01":
        return None
    name = image[0x1D:0x25].split(b"\0")[0].decode("ascii")
    return f"{name}.{image[0x4E:0x51].decode('ascii')}"


class RtlinkProgram(Program):
    """The root with its library linked in, the manager's table, and the overlays."""

    linker = ".RTLink/Plus"

    def __init__(self, exe: bytes, ovl: bytes | None = None, library: bytes | None = None):
        self.mz = MzExecutable(exe)
        self._ovl = ovl
        image = self.mz.image
        entry = self.mz.cs * 16 + self.mz.ip
        if image[entry : entry + len(_ENTRY)] != _ENTRY:
            raise ValueError("not a .RTLink program: it doesn't start in .RTLink's stub")
        self.version = struct.unpack_from("<H", image, 0x34)[0]
        if self.version not in _VERSIONS:
            raise ValueError(f"linked by a version of .RTLink that isn't known here: {self.version:04x}")
        (
            self.start_offset,
            self.start_segment,
            self.stack_offset,
            self.stack_segment,
            segments,
            segment_count,
            groups,
            group_count,
            symbols,
            symbol_count,
            records,
            record_count,
        ) = struct.unpack_from("<12H", image, 0)
        self.segments = [self._address(image, segments + 4 * index) for index in range(segment_count)]
        self.groups = [self._address(image, groups + 4 * index) for index in range(group_count)]
        self._symbols = [
            image[symbols + 8 * index : symbols + 8 * index + 8] for index in range(symbol_count)
        ]
        # 6.10 has a second table of symbols, after the first
        more, more_count = (
            struct.unpack_from("<HH", image, 0x4A) if self.version >= _SECOND_SYMBOLS else (0, 0)
        )
        self._more_symbols = [image[more + 8 * index : more + 8 * index + 8] for index in range(more_count)]
        self.memory_end_segment = self.stack_segment + (self.stack_offset + 15) // 16
        self.exe_name = ""

        self._memory = bytearray(self.memory_end_segment * 16)
        self._relocations: set[int] = set()
        self.records, _ = read_records(image, records, record_count)
        self.library_name = library_name(exe)
        self.library_records: list[Record] = []
        if self.library_name is not None:
            if library is None:
                raise ValueError(f"its run-time library, {self.library_name}, wasn't given")
            self.library_records = self._library(library, struct.unpack_from("<H", image, 0x26)[0])
        for record in self.records + self.library_records:
            self._place(record)

        self.loader_segment = self.table_segment = self.start_segment
        self._manager()

    @staticmethod
    def _address(image: bytes, at: int) -> int:
        offset, segment = struct.unpack_from("<HH", image, at)
        return segment * 16 + offset

    def _library(self, library: bytes, stamp: int) -> list[Record]:
        magic, _, _, its_stamp, count, version = struct.unpack_from("<HBBHHH", library, 0)
        if magic != _LIBRARY_MAGIC:
            raise ValueError(f"{self.library_name} isn't a .RTLink run-time library")
        if its_stamp != stamp or version != self.version:
            raise ValueError(f"{self.library_name} isn't the library the program was linked with")
        records, end = read_records(library, struct.unpack_from("<H", library, 0x10)[0] * 16, count)
        if end != len(library):
            raise ValueError(f"{self.library_name}'s records don't end where its file does")
        return records

    def _segment(self, number: int) -> int:
        if not 1 <= number <= len(self.segments):
            raise ValueError(f"a record names segment {number}, and there are {len(self.segments)}")
        return self.segments[number - 1]

    def _resolve(self, kind: int, number: int) -> tuple[int, int]:
        """
        Where a frame or a target is, from what its number is of: a base, and how far past it.
        A frame is the base alone. A symbol's base is its group, or its segment when it has none.
        """
        if kind == 0:
            return self._segment(number), 0
        if kind == 1:
            return self.groups[number - 1], 0
        if kind == _ABSOLUTE:
            return number * 16, 0
        symbols = self._symbols if kind == 2 else self._more_symbols if kind == _SECOND_SYMBOL else []
        if 1 <= number <= len(symbols) and symbols[number - 1][:1] == b"R":
            group, segment, offset = struct.unpack_from("<HHH", symbols[number - 1], 1)
            where = self._segment(segment) + offset
            base = self.groups[group - 1] if group else self._segment(segment)
            return base, where - base
        raise ValueError(f"a fixup names something that isn't known here: kind {kind}, number {number}")

    def _add(self, at: int, value: int) -> None:
        struct.pack_into("<H", self._memory, at, (self.word(at) + value) & 0xFFFF)

    def word(self, at: int) -> int:
        return struct.unpack_from("<H", self._memory, at)[0]

    def _fix(self, fixups: Fixups, at: int) -> None:
        """One named fixup, done as the startup code does it."""
        flags = fixups.flags
        kind = (flags & _KIND) >> 2
        frame_kind = (flags & _FRAME) >> 9
        frame, _ = self._resolve(frame_kind, fixups.frame)
        value = sum(self._resolve((flags & _TARGET) >> 6, fixups.target)) + fixups.addend
        if flags & _FROM_FRAME:
            value -= frame & ~15
        else:
            # from the place itself, as a jump's or a call's operand is counted
            value -= at + (1 if kind == 0 else 2)
        if kind in (0, 4):
            self._memory[at] = (self._memory[at] + (value >> 8 if kind else value)) & 0xFF
            return
        if kind in (1, 3):
            self._add(at, value)
        if kind in (2, 3):
            where = at + 2 if kind == 3 else at
            if flags & _SHIFTED:
                struct.pack_into("<H", self._memory, where, (self.word(where) << 12) & 0xFFFF)
            self._add(where, frame >> 4)
            if frame_kind != _ABSOLUTE:
                self._relocations.add(where)

    def _place(self, record: Record) -> None:
        start = self._segment(record.segment) + record.offset
        if start + record.length > len(self._memory):
            raise ValueError(f"segment {record.segment} goes past the end of the program's memory")
        if record.data is not None:
            self._memory[start : start + record.length] = record.data
        for fixups in record.fixups:
            for place in fixups.places:
                if fixups.named:
                    self._fix(fixups, start + place)
                else:
                    self._relocations.add(start + place)

    def _manager(self) -> None:
        """The overlay manager's table and its thunks, found in the segment the program starts in."""
        base = self.start_segment * 16
        length = max(
            (
                record.offset + record.length
                for record in self.records
                if self._segment(record.segment) == base
            ),
            default=0,
        )
        segment = bytes(self._memory[base : base + length])
        # the manager says where its table is: where the count is, where the entries start,
        # and an entry's size, one after another. The entries are 4 bytes past the count in
        # 4.10, and 6 or 8 in 5.02, with what the manager keeps between them
        table = next(
            (
                (count_at, entries)
                for at in range(length - 6)
                for count_at, entries, size in [struct.unpack_from("<HHH", segment, at)]
                if size == _ENTRY_SIZE
                and entries - count_at in (4, 6, 8)
                and entries + _ENTRY_SIZE <= length
                and 0 < struct.unpack_from("<H", segment, count_at)[0] <= (length - entries) // _ENTRY_SIZE
            ),
            None,
        )
        self.overlays: list[Overlay] = []
        self.thunks: list[Thunk] = []
        self.ovl_name = ""
        if table is not None:
            # the count is one more than there are
            count = struct.unpack_from("<H", segment, table[0])[0] - 1
            for number in range(1, count + 1):
                load, attributes, paragraph, status, size, relocations = struct.unpack_from(
                    "<6H", segment, table[1] + _ENTRY_SIZE * (number - 1)
                )
                self.overlays.append(
                    Overlay(number, status, relocations, load, load + size, attributes, paragraph, size)
                )
            end = segment.upper().find(b".OVL\0", table[1])
            if end >= 0:
                self.ovl_name = segment[segment.rindex(b"\0", 0, end) + 1 : end + 4].decode("ascii")
            # the thunks end the manager's segment in 4.10; in 5.02 they come after it, in
            # segments of its group, and are reached through its own
            self._find_thunks(bytes(self._memory[base : base + 0x10000]))
        self._thunks = {(thunk.segment, thunk.offset): thunk for thunk in self.thunks}

    def _find_thunks(self, segment: bytes) -> None:
        """The thunks: ten bytes each, one after another, every one calling the same place."""
        found = []
        at = 0
        while at + _THUNK_SIZE <= len(segment):
            if segment[at] != 0xE8 or segment[at + 3] != 0xEA:
                at += 1
                continue
            called = (at + 3 + struct.unpack_from("<h", segment, at + 1)[0]) & 0xFFFF
            offset, target, index = struct.unpack_from("<HHH", segment, at + 4)
            if index >= len(self.overlays) or called >= at:
                at += 1
                continue
            found.append((called, Thunk(self.start_segment, at, index + 1, target, offset)))
            at += _THUNK_SIZE
        calls = Counter(called for called, _ in found)
        if calls:
            manager = calls.most_common(1)[0][0]
            self.thunks = sorted(
                (thunk for called, thunk in found if called == manager), key=lambda thunk: thunk.offset
            )

    @property
    def root_size(self) -> int:
        return len(self._memory)

    def in_root(self, linear: int) -> bool:
        segment = linear // 16
        if any(overlay.load_segment <= segment < overlay.end_segment for overlay in self.overlays):
            return False
        return 0 <= linear < len(self._memory)

    def root(self) -> tuple[bytearray, set[int]]:
        return bytearray(self._memory), set(self._relocations)

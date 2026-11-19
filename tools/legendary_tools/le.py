"""
A 32-bit program for DOS/4GW, as Mission Critical is: the linear executable bound into its .EXE.

The file starts with DOS/4GW itself, which is a 16-bit program and two more after it, each headed
`BW` and saying where the next is. Last in the chain is a small MZ stub, and its header's long at
3Ch says how far on the linear executable is, headed `LE`.

The LE header gives the program as objects, each a run of 4,096-byte pages that loads at an address
of its own, and a table of the places in each page that hold an address, to be put right should an
object load elsewhere. The pages' offsets are from the stub's start; everything else in the header
is from the header's own.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass


@dataclass(frozen=True)
class LeObject:
    """A run of the program's pages, and where in memory it expects to be."""

    number: int
    size: int  # in memory: what's past its pages is zeros
    base: int
    flags: int
    first_page: int  # from 1
    pages: int

    @property
    def is_code(self) -> bool:
        return bool(self.flags & 0x0004)

    @property
    def end(self) -> int:
        return self.base + self.size


@dataclass(frozen=True)
class Fixup:
    """A place that holds an address in the program: where it is, and what it points to."""

    at: int
    kind: int  # 7 a 32-bit offset, 8 one relative to the place, 2 a selector, 6 a far pointer
    object: int
    target: int


def _stub(data: bytes) -> int:
    """Where the MZ stub before the linear executable is: last in the chain DOS/4GW is bound as."""
    if data[:2] != b"MZ":
        raise ValueError("not an MZ program")
    last_page, pages = struct.unpack_from("<HH", data, 2)
    at = pages * 512 - ((512 - last_page) % 512)
    while data[at : at + 2] == b"BW":
        (at,) = struct.unpack_from("<I", data, at + 0x1C)
    if data[at : at + 2] != b"MZ":
        # a program that isn't bound to its extender has the stub first
        at = 0
    return at


def is_linear(data: bytes) -> bool:
    """Whether the file has a linear executable in it."""
    try:
        stub = _stub(data)
    except (ValueError, struct.error):
        return False
    if len(data) < stub + 0x40:
        return False
    (header,) = struct.unpack_from("<I", data, stub + 0x3C)
    return data[stub + header : stub + header + 4] == b"LE\0\0"


class LeExecutable:
    """The 32-bit program in a file, with its objects where they expect to load."""

    def __init__(self, data: bytes):
        self.stub = _stub(data)
        (offset,) = struct.unpack_from("<I", data, self.stub + 0x3C)
        self.header = self.stub + offset
        if data[self.header : self.header + 4] != b"LE\0\0":
            raise ValueError("no linear executable in it")
        self.data = data
        (
            self.page_count,
            entry_object,
            entry,
            stack_object,
            stack,
            self.page_size,
            self.last_page_size,
        ) = struct.unpack_from("<7I", data, self.header + 0x14)
        object_table, object_count, self._page_map = struct.unpack_from("<3I", data, self.header + 0x40)
        self._fixup_pages, self._fixup_records = struct.unpack_from("<2I", data, self.header + 0x68)
        (self._pages,) = struct.unpack_from("<I", data, self.header + 0x80)
        self.objects = [
            LeObject(number + 1, size, base, flags, first, pages)
            for number, (size, base, flags, first, pages, _) in enumerate(
                struct.iter_unpack(
                    "<6I", data[self.header + object_table : self.header + object_table + 24 * object_count]
                )
            )
        ]
        self.entry = self.objects[entry_object - 1].base + entry
        self.stack = self.objects[stack_object - 1].base + stack
        self._contents: dict[int, bytes] = {}
        self._fixups: list[Fixup] | None = None

    def object_at(self, address: int) -> LeObject | None:
        return next((each for each in self.objects if each.base <= address < each.end), None)

    def _page(self, number: int) -> bytes:
        """A page's bytes, by its number from 1: the last is short in the file."""
        # each entry of the map is the page's number, big end first in three bytes, and a flag
        entry = self.header + self._page_map + 4 * (number - 1)
        physical = int.from_bytes(self.data[entry : entry + 3], "big")
        start = self.stub + self._pages + (physical - 1) * self.page_size
        size = self.last_page_size if physical == self.page_count else self.page_size
        return self.data[start : start + size]

    def contents(self, each: LeObject) -> bytes:
        """An object as it is in memory, loaded where it expects to be, with its addresses put in."""
        if each.number not in self._contents:
            pages = b"".join(
                self._page(each.first_page + index).ljust(self.page_size, b"\0")
                for index in range(each.pages)
            )
            memory = bytearray(pages[: each.size].ljust(each.size, b"\0"))
            # the file leaves each address's place as it likes: the table alone says what goes there
            for fixup in self.fixups():
                if fixup.kind == 7 and each.base <= fixup.at <= each.end - 4:
                    struct.pack_into("<I", memory, fixup.at - each.base, fixup.target)
            self._contents[each.number] = bytes(memory)
        return self._contents[each.number]

    def read(self, address: int, size: int) -> bytes:
        """Memory at an address, as far as the object it's in goes."""
        each = self.object_at(address)
        if each is None:
            raise ValueError(f"{address:08x} is in none of the program's objects")
        return self.contents(each)[address - each.base : address - each.base + size]

    def fixups(self) -> list[Fixup]:
        """Every place that holds an address, in the order of the pages."""
        if self._fixups is not None:
            return self._fixups
        found = self._fixups = []
        table = self.header + self._fixup_pages
        for each in self.objects:
            for index in range(each.pages):
                page = each.first_page + index
                start, end = struct.unpack_from("<2I", self.data, table + 4 * (page - 1))
                found += self._page_fixups(
                    each.base + index * self.page_size,
                    self.header + self._fixup_records + start,
                    self.header + self._fixup_records + end,
                )
        return found

    def _page_fixups(self, base: int, at: int, end: int) -> list[Fixup]:
        found = []
        while at < end:
            kind, flags = self.data[at], self.data[at + 1]
            if kind & 0x20 or flags & 0x03:
                raise ValueError(f"a fixup of a kind not read: {kind:02x} {flags:02x}")
            (source,) = struct.unpack_from("<h", self.data, at + 2)
            at += 4
            if flags & 0x40:
                (target_object,) = struct.unpack_from("<H", self.data, at)
                at += 2
            else:
                target_object = self.data[at]
                at += 1
            target = 0
            # a selector alone has no offset after it
            if kind & 0x0F != 2:
                size = 4 if flags & 0x10 else 2
                target = int.from_bytes(self.data[at : at + size], "little")
                at += size
            # one that points before its object's start, as an array counted from 1 does, wraps round
            address = (self.objects[target_object - 1].base + target) & 0xFFFFFFFF
            found.append(Fixup(base + source, kind & 0x0F, target_object, address))
        return found

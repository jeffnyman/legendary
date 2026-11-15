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

from .mz import MzExecutable
from .program import Overlay, Program, Thunk

# how the program begins: cli / mov di,es / mov si,ss / mov cx,sp / mov dx,<the table's segment>
_ENTRY = bytes.fromhex("fa8cc78cd68bccba")

PRELOAD = 0x4000  # loaded as the program starts
IN_EXE = 0x2000  # in the .EXE, not the .OVL (inferred: the data is the one overlay with it)
END = 0xFFFF


class Plink86Program(Program):
    """The root, the loader's table, and the overlays read from the two files."""

    linker = "PLINK86"

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
                Overlay(
                    len(self.overlays) + 1,
                    status,
                    count,
                    load,
                    end,
                    attributes,
                    paragraph,
                    size,
                    preloaded=bool(status & PRELOAD),
                    in_exe=bool(status & IN_EXE),
                )
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

    @property
    def root_size(self) -> int:
        return len(self.mz.image)

    def root(self) -> tuple[bytearray, set[int]]:
        memory = bytearray(self.memory_end_segment * 16)
        memory[: len(self.mz.image)] = self.mz.image
        return memory, {relocation.linear for relocation in self.mz.relocations}

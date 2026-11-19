"""What each line of a game's speech says: the text a line's number goes with.

Companions of Xanth plays a line of its .VOC file by the line's number, and the number is
never beside the sound: it is in the program, with the text, in three ways.

A line of talk has the number of its sound. The game keeps what can be said to its people
as a table of lines, four bytes each, a number for the line's text and a word that leads on
to what can be said next. The text's number is a place in a table of far pointers to the
texts, or from 8000h a routine that makes the text. The lines of talk come first in the
file: there are as many as the count the game keeps of them.

A line of a scene, where the game tells its story with pictures and voices, is a record of
sixteen bytes in the scene's overlay, which starts with the number of its sound and a far
pointer to its text.

The rest are named in the code where they are said, a constant put in the word the game
keeps for the line to speak, and those aren't followed here.
"""

from __future__ import annotations

import re
import struct

from .data import StringFile
from .program import Program

# cmp [bp+6],8000h / jb <a text> / ... / mov es,[variable] / mov bx,[bp+6] / add bx,bx /
# add bx,bx / mov ax,es:[bx]: a text by its number, from the table of far pointers
_TEXTS = re.compile(
    rb"\x81\x7e\x06\x00\x80\x72.{0,24}?\x8e\x06(..)\x8b\x5e\x06\x03\xdb\x03\xdb\x26\x8b\x87\x00\x00",
    re.DOTALL,
)
# mov ax,[count] / cmp [bp+6],ax / jae <none> / mov ax,[bp+6] / add ax,<states>: is this a
# line of talk? The count is how many there are
_COUNT = re.compile(rb"\xa1(..)\x39\x46\x06\x73.\x8b\x46\x06\x05..\xba", re.DOTALL)
# add ax,ax / add ax,ax / add ax,<lines> / mov [bp-n],ax / mov word [bp-n],<segment>: a line
# of talk's four bytes. What the player can say is a table read the same way, before it: the
# lines spoken back are the last read
_LINES = re.compile(rb"\x03\xc0\x03\xc0\x05(..)\x89\x86..\xc7\x86..(..)", re.DOTALL)
_STRINGS = 0xF0  # the high byte of a far pointer's segment that is a string's number


def speech_texts(program: Program, strings: StringFile) -> dict[int, str]:
    """The text of each line of speech that has one that can be read, by the line's number."""
    blocks: dict[int, list[str]] = {}

    def text(memory: bytes, at: int) -> str | None:
        """The text a far pointer is to: a string of the file's by number, or one in memory."""
        offset, segment = struct.unpack_from("<HH", memory, at)
        block = segment & 0xFF
        if segment >> 8 == _STRINGS:
            if block >= len(strings.blocks) or offset >= strings.blocks[block].strings:
                return None
            if block not in blocks:
                blocks[block] = strings.strings(strings.blocks[block])
            return blocks[block][offset]
        start = segment * 16 + offset
        if not segment or start >= len(memory):
            return None
        return memory[start : memory.index(b"\0", start)].decode("latin-1")

    found: dict[int, str] = {}
    root = program.image().memory
    count = 0
    for overlay in program.overlays:
        memory = program.image(overlay.number).memory
        code = bytes(memory[overlay.load_segment * 16 : overlay.end_segment * 16])
        texts, counted, tables = _TEXTS.search(code), _COUNT.search(code), list(_LINES.finditer(code))
        if not (texts and counted and tables):
            continue
        data = program.image().word
        notice = bytes(root).find(b"MS Run-Time Library")
        segment = notice - 8  # the data's segment, which the notice is eight bytes into
        table = data(segment + int.from_bytes(texts[1], "little")) * 16
        count = data(segment + int.from_bytes(counted[1], "little"))
        lines = tables[-1]
        rows = int.from_bytes(lines[2], "little") * 16 + int.from_bytes(lines[1], "little")
        for line in range(count):
            number = data(rows + 4 * line)
            if number < 0x8000 and (said := text(bytes(root), table + 4 * number)) is not None:
                found[line] = said
        break

    # the scenes' records: a line's number, past the lines of talk, and a string's number
    for overlay in program.overlays:
        memory = bytes(program.image(overlay.number).memory)
        for at in range(overlay.load_segment * 16, overlay.end_segment * 16 - 6):
            line, _, segment = struct.unpack_from("<HHH", memory, at)
            if line >= count and segment >> 8 == _STRINGS and line not in found:
                said = text(memory, at + 2)
                if said is not None and len(said) > 1:
                    found[line] = said
    return found

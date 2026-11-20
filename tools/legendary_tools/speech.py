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

Mission Critical talks the same way, and speaks both sides. What is said back to the player
is a table of records of eight bytes, the second word the text's number and the third what
can be said next; a record's own place in the table is the number of its sound, as it is in
Companions of Xanth. What the player can say is in sets: a set is five bytes, how many
things, where the first is in a table of four bytes a thing, a text's number and where it
leads, and a routine's number. The player's own lines are spoken too, and a table of five
bytes says which sound each is: the set, as 8000h and its number, which thing of the set,
and the line. A text's number is a place in a table of four bytes: F0h in the top byte with
a block and a string of the text file below it, or the address of text in the program.

And where the program shows a text and speaks a line there and then, the two are side by
side in the code: the text's number pushed for the routine that shows it, and the line's put
in EAX for the one that speaks it.

Not every line is speech. The same files hold the game's sounds, an alarm, a door, a hum,
and the program starts those by other routines of the same part of it, which take a line's
number as the one that speaks does. And the ship's briefing has a table of its own, eight
bytes a part: the line that tells it, a picture, and the address of the part's title.
`speech_notes_32` says of each line it can which of these it is.
"""

from __future__ import annotations

import collections
import re
import struct

from .data import StringFile
from .le import LeExecutable
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


# cmp bx,[eax+table] / jne / cmp dl,[eax+table+2] / jne / mov ax,[eax+table+3] / pop ebx / ret /
# add eax,5 / cmp eax,<size>: the line the player speaks, by its set and which thing of it
_SPOKEN = re.compile(
    rb"\x66\x3b\x98(....)\x75.\x3a\x90....\x75.\x66\x8b\x80....\x5b\xc3\x83\xc0\x05\x3d(....)", re.DOTALL
)
# cmp eax,8000h / jb / call [edx+routines] / pop edx / ret / mov eax,[edx+texts]: a text by its number
_TEXTS_32 = re.compile(rb"\x3d\x00\x80\x00\x00\x72\x08\xff\x92....\x5a\xc3\x8b\x82(....)", re.DOTALL)
# mov edx,<sets> / lea eax,[eax+eax*4] / ... / mov ax,[edx+1] / mov edx,<things>: a set, and its things
_SETS = re.compile(rb"\xba(....)\x8d\x04\x80.{0,24}?\x66\x8b\x42\x01\xba(....)\xc1\xe0\x02", re.DOTALL)
# mov ebp,<replies> / shl edx,3: what is said back, eight bytes each
_REPLIES = re.compile(rb"\xbd(....)\xc1\xe2\x03", re.DOTALL)
# xor edx,edx / mov dx,[count] / cmp eax,edx / jae / add eax,<states>: how many of those there are
_REPLY_COUNT = re.compile(rb"\x31\xd2\x66\x8b\x15(....)\x39\xd0\x73.\x05....\x59\x5b\xc3", re.DOTALL)
# push <text> / call <show> / add esp,4 / xor edx,edx / mov eax,<line> / call <speak>
_SHOWN_AND_SPOKEN = re.compile(
    rb"\x68(...\xf0)\xe8....\x83\xc4\x04\x31\xd2\xb8(..)\x00\x00\xe8(....)", re.DOTALL
)


def speech_texts_32(program: LeExecutable, strings: StringFile) -> dict[int, str]:
    """The text each line of Mission Critical's speech says, by the line's number, where it's known."""
    code_object = next(each for each in program.objects if each.is_code)
    code = program.contents(code_object)
    blocks: dict[int, list[str]] = {}

    def long_at(address: int) -> int:
        return struct.unpack("<I", program.read(address, 4))[0]

    def word_at(address: int) -> int:
        return struct.unpack("<H", program.read(address, 2))[0]

    def named(number: int) -> str | None:
        """The text a number in the file's own form names: a block and a string."""
        block, string = number >> 16 & 0xFF, number & 0xFFFF
        if number >> 24 != 0xF0 or block >= len(strings.blocks):
            return None
        if block not in blocks:
            blocks[block] = strings.strings(strings.block(block))
        return blocks[block][string] if string < len(blocks[block]) else None

    def text(number: int) -> str | None:
        """A text by its number in the program's table; None for one a routine makes."""
        if number >= 0x8000:
            return None
        found = long_at(texts + 4 * number)
        if found >> 24 == 0xF0:
            return named(found)
        where = program.object_at(found)
        if where is None or where.is_code:
            return None
        plain = program.read(found, 400).split(b"\0")[0]
        return plain.decode("latin-1") if plain else None

    found: dict[int, str] = {}
    spoken, sets, replies, count, table = (
        _SPOKEN.search(code),
        _SETS.search(code),
        _REPLIES.search(code),
        _REPLY_COUNT.search(code),
        _TEXTS_32.search(code),
    )
    if not (spoken and sets and replies and count and table):
        return found
    (texts,) = struct.unpack("<I", table[1])
    # what is said back: a record's place is its line
    (reply_at,) = struct.unpack("<I", replies[1])
    for line in range(word_at(struct.unpack("<I", count[1])[0])):
        said = text(word_at(reply_at + 8 * line + 2))
        if said:
            found[line] = said
    # what the player says: the set, which thing of it, and the line
    spoken_at, size = struct.unpack("<I", spoken[1])[0], struct.unpack("<I", spoken[2])[0]
    set_at, thing_at = struct.unpack("<I", sets[1])[0], struct.unpack("<I", sets[2])[0]
    for at in range(spoken_at, spoken_at + size, 5):
        which, thing, line = struct.unpack("<HBH", program.read(at, 5))
        if which < 0x8000 or not line:
            continue
        first = word_at(set_at + 5 * (which - 0x8000) + 1)
        said = text(word_at(thing_at + 4 * (first + thing)))
        if said:
            found.setdefault(line, said)
    # and the lines spoken where their text is shown
    speakers: dict[int, int] = {}
    pairs = []
    for match in _SHOWN_AND_SPOKEN.finditer(code):
        target = code_object.base + match.end() + struct.unpack("<i", match[3])[0]
        speakers[target] = speakers.get(target, 0) + 1
        pairs.append((target, struct.unpack("<I", match[1])[0], struct.unpack("<H", match[2])[0]))
    # the routine that speaks is the one most of them call: another plays sounds by number
    speak = max(speakers, key=lambda target: speakers[target]) if speakers else None
    for target, number, line in pairs:
        said = named(number)
        if target == speak and said:
            found.setdefault(line, said)
    return found


# mov eax,<line> / a register or two set / call: a line's number handed to a routine
_HANDED = re.compile(
    rb"\xb8(..)\x00\x00(?:\x31[\xd2\xdb]|[\xba\xbb\xbe]....|\x89[\xda\xfa]){0,2}\xe8(....)", re.DOTALL
)
# a text's number put in EAX or pushed, some way before
_TEXT_NEAR = re.compile(rb"[\x68\xb8](...\xf0)", re.DOTALL)
# mov edx,<first> / movsx eax,dx / inc edx / call / cmp edx,<last> / jle: a run of sounds readied
# one after another, for one of them to be played at random
_RUN = re.compile(rb"\xba(..)\x00\x00\x0f\xbf\xc2\x42\xe8(....)\x81\xfa(..)\x00\x00\x7e", re.DOTALL)
# mov eax,[esi+table-2] / mov edx,edi / sar eax,16 / call: the briefing's parts, the line of each
_BRIEFING = re.compile(rb"\x8b\x86(....)\x89\xfa\xc1\xf8\x10\xe8", re.DOTALL)


def speech_notes_32(program: LeExecutable, strings: StringFile, lines: int) -> dict[int, str]:
    """
    What the program does with a line of Mission Critical's, for the lines its talk doesn't
    name: speaks it, with the text it shows as it does where there is one, starts it as a
    sound, or tells a part of the ship's briefing with it.
    """
    code_object = next(each for each in program.objects if each.is_code)
    code = program.contents(code_object)
    blocks: dict[int, list[str]] = {}

    def named(number: int) -> str | None:
        block, string = number >> 16 & 0xFF, number & 0xFFFF
        if number >> 24 != 0xF0 or block >= len(strings.blocks):
            return None
        if block not in blocks:
            blocks[block] = strings.strings(strings.block(block))
        return blocks[block][string] if string < len(blocks[block]) else None

    # the routine that speaks, by the pairs of a text shown and a line spoken
    pairs = collections.Counter(
        code_object.base + found.end() + struct.unpack("<i", found[3])[0]
        for found in _SHOWN_AND_SPOKEN.finditer(code)
    )
    if not pairs:
        return {}
    speak = pairs.most_common(1)[0][0]
    spoken: dict[int, set[str | None]] = {}
    sounds: dict[int, int] = {}
    for found in _HANDED.finditer(code):
        (line,) = struct.unpack("<H", found[1])
        target = code_object.base + found.end() + struct.unpack("<i", found[2])[0]
        if not 0 < line < lines:
            continue
        if target == speak:
            # the text shown just before, if one was
            near = _TEXT_NEAR.findall(code, max(found.start() - 48, 0), found.start())
            spoken.setdefault(line, set()).add(named(struct.unpack("<I", near[-1])[0]) if near else None)
        elif speak < target < speak + 0x800:
            # the routines after it in the same part of the program start, stop and ask after sounds
            sounds.setdefault(line, code_object.base + found.start())
    notes = {line: f"a sound, not speech: the program starts it at {at:08x}" for line, at in sounds.items()}
    for found in _RUN.finditer(code):
        first, last = struct.unpack("<H", found[1])[0], struct.unpack("<H", found[3])[0]
        target = code_object.base + found.start() + 13 + struct.unpack("<i", found[2])[0]
        if speak < target < speak + 0x800 and 0 < first <= last < lines:
            at = code_object.base + found.start()
            for line in range(first, last + 1):
                notes.setdefault(line, f"a sound, not speech: one of {first} to {last}, readied at {at:08x}")
    for line, texts in spoken.items():
        only = next(iter(texts)) if len(texts) == 1 else None
        notes[line] = f"spoken as the game says: {only}" if only else "spoken, by its number in the code"
    if briefing := _BRIEFING.search(code):
        at = struct.unpack("<I", briefing[1])[0] + 2
        while True:
            line, _, title = struct.unpack("<HHI", program.read(at, 8))
            where = program.object_at(title)
            if not 0 < line < lines or where is None or where.is_code:
                break
            name = program.read(title, 60).split(b"\0")[0].decode("latin-1")
            notes[line] = f"the ship's briefing: {name}" if name else "the ship's briefing"
            at += 8
    return notes

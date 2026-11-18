"""The commands: python -m legendary_tools <command> <the game's folder> ..."""

from __future__ import annotations

import argparse
import collections
import contextlib
import io
import os
import re
import struct
import sys

from . import data
from .flags import GAME_201, GAME_301, GAME_GATEWAY, GAME_TIMEQUEST, describe_flag, flag_name, flag_uses
from .gallery import write_index
from .game import GameFolder
from .grammar import NO_TURN, Form, action_flags, describe_flags, read_grammar
from .handlers import action_routines, keyed_routines, object_routines
from .loads import (
    POINTER_SEGMENT,
    StringNames,
    find_loader,
    find_loads,
    find_named,
    find_namers,
    find_pointed,
    pointed_string,
)
from .mz import MzExecutable
from .objects import DIRECTIONS, Exit, GameObject, ObjectTable, SavedState, is_pointer
from .pictures import (
    HUFFMAN,
    IMPLODED,
    LZW,
    RAW_PLANE,
    RAW_PLANES,
    PictureFile,
    is_imploded,
    is_known,
    is_later,
)
from .plink86 import Plink86Program
from .program import Image, Program
from .regions import read_regions
from .rtlink import RtlinkProgram, is_rtlink, library_name
from .sentence import read_sentence_grammar
from .sound import read_songs, read_sound
from .timers import (
    EVERY_TURN,
    TIMER_NAMES,
    TIMER_NAMES_201,
    TIMER_NAMES_301,
    TIMER_NAMES_GATEWAY,
    TIMER_NAMES_TIMEQUEST,
    timer_calls,
    timer_code,
)


def _program(folder: GameFolder) -> Program:
    """The game's program, read as the linker that made it left it."""
    exe = folder.read(folder.main_program())
    found = folder.ending("main.ovl")
    ovl = folder.read(found[0]) if found else None
    try:
        if is_rtlink(exe):
            name = library_name(exe)
            path = folder.find(name) if name else None
            return RtlinkProgram(exe, ovl, folder.read(path) if path else None)
        return Plink86Program(exe, ovl)
    except ValueError as error:
        raise ValueError(f"{folder.main_program()}: {error}") from error


def _address(text: str) -> tuple[int, int]:
    try:
        segment, offset = (int(part, 16) for part in text.split(":"))
    except ValueError:
        raise ValueError(f"{text}: an address is segment:offset, in hex, as 2f1c:0192") from None
    return segment, offset


def exe(args: argparse.Namespace) -> None:
    """Each program's MZ header."""
    folder = GameFolder(args.folder)
    for path in folder.ending(".exe"):
        try:
            mz = MzExecutable(folder.read(path))
        except ValueError:
            continue
        print(os.path.basename(path))
        print(f"  file                {len(mz.data):7} bytes")
        print(f"  header              {mz.header_size:7} bytes, {len(mz.relocations)} relocations")
        print(f"  image               {len(mz.image):7} bytes, {mz.image_paragraphs:04x} paragraphs")
        print(f"  after the image     {mz.trailing:7} bytes")
        print(f"  memory at the least    {mz.memory_paragraphs:04x} paragraphs")
        print(f"  starts at           {mz.cs:04x}:{mz.ip:04x}, stack {mz.ss:04x}:{mz.sp:04x}")


def overlays(args: argparse.Namespace) -> None:
    """The loader's table of overlays, by where they load."""
    program = _program(GameFolder(args.folder))
    mz = program.mz
    print(f"linker    {program.linker}")
    if isinstance(program, RtlinkProgram):
        _, relocations = program.root()
        print(f"root      {len(program.records)} records, {len(relocations)} relocations")
        print(f"library   {program.library_name}, {len(program.library_records)} records")
        print(f"segments  {len(program.segments)}")
        print(f"starts at {program.start_segment:04x}:{program.start_offset:04x}")
    else:
        print(
            f"root      0000-{mz.image_paragraphs:04x}  {len(mz.image)} bytes,"
            f" {len(mz.relocations)} relocations"
        )
    print(f"loader    {program.loader_segment:04x}")
    print(f"table     {program.table_segment:04x}, with {len(program.thunks)} thunks after it")
    print(f"memory    ends at {program.memory_end_segment:04x}")
    for load, group in program.areas().items():
        print(f"\nat {load:04x}:")
        for overlay in group:
            file = program.exe_name if overlay.in_exe else program.ovl_name
            notes = ", loaded at the start" if overlay.preloaded else ""
            print(
                f"  {overlay.number:2}  {overlay.load_segment:04x}-{overlay.end_segment:04x}"
                f"  {overlay.size_paragraphs * 16:6} bytes, {overlay.relocation_count:4} relocations,"
                f" {file} at {overlay.file_paragraph * 16:#07x}{notes}"
            )


def thunks(args: argparse.Namespace) -> None:
    """The ways into the overlays: each thunk, its overlay, and the routine it goes to."""
    program = _program(GameFolder(args.folder))
    for thunk in program.thunks:
        if args.overlay in (None, thunk.overlay):
            print(
                f"{thunk.segment:04x}:{thunk.offset:04x}  overlay {thunk.overlay:2}"
                f"  {thunk.target_segment:04x}:{thunk.target_offset:04x}"
            )


def segments(args: argparse.Namespace) -> None:
    """Each segment the relocations name, and how often, with the overlays given loaded."""
    program = _program(GameFolder(args.folder))
    image = program.image(*args.overlay)
    for segment, places in sorted(image.segment_references().items()):
        print(f"{segment:04x}  {len(places):5}  {image.where(segment * 16)}")


def refs(args: argparse.Namespace) -> None:
    """The places that name a segment, in the root and every overlay."""
    program = _program(GameFolder(args.folder))
    wanted = int(args.segment, 16)

    def show(image: Image, only: str | None) -> None:
        for linear in image.segment_references().get(wanted, []):
            where = image.where(linear)
            if only in (None, where):
                # a far pointer's segment follows its offset
                offset = image.word(linear - 2)
                place = f"{linear // 16:04x}:{linear % 16:x}  ({linear:#07x})"
                print(f"{place}  {where:11}  {wanted:04x}:{offset:04x}?")

    show(program.image(), None)
    for overlay in program.overlays:
        if not overlay.preloaded:
            show(program.image(overlay.number), f"overlay {overlay.number}")


def dump(args: argparse.Namespace) -> None:
    """Memory at an address, in hex, with the overlays given loaded."""
    program = _program(GameFolder(args.folder))
    image = program.image(*args.overlay)
    segment, offset = _address(args.address)
    for row in range(offset, offset + args.count, 16):
        chunk = image.memory[segment * 16 + row : segment * 16 + min(row + 16, offset + args.count)]
        text = "".join(chr(byte) if 0x20 <= byte < 0x7F else "." for byte in chunk)
        print(f"{segment:04x}:{row:04x}  {chunk.hex(' '):47}  {text}")


def dis(args: argparse.Namespace) -> None:
    """Instructions from an address, with the overlays given loaded."""
    try:
        # here and not at the top: only this command needs it, and the others run without it
        import capstone  # noqa: PLC0415
    except ImportError:
        raise ValueError("dis needs capstone: pip install capstone") from None
    program = _program(GameFolder(args.folder))
    segment, offset = _address(args.address)
    numbers = list(args.overlay)
    # an address in a thunk's overlay, when no overlay is named, is taken to be in that one
    if not numbers:
        numbers = sorted(
            {
                thunk.overlay
                for thunk in program.thunks
                if (thunk.target_segment, thunk.target_offset) == (segment, offset)
            }
        )[:1]
    image = program.image(*numbers)
    print(f"; {image.where(segment * 16 + offset)}")
    describe = _string_notes(GameFolder(args.folder), program, image.overlay_at(segment * 16 + offset))
    literal = _literal_notes(GameFolder(args.folder), program, image)
    start = segment * 16 + offset
    disassembler = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_16)
    instructions = disassembler.disasm(image.memory[start : start + 16 * args.count], offset)
    for _, instruction in zip(range(args.count), instructions):
        linear = segment * 16 + instruction.address
        note = ""
        code = instruction.bytes
        if code[0] in (0x9A, 0xEA) and len(code) == 5:
            target = (int.from_bytes(code[3:5], "little"), int.from_bytes(code[1:3], "little"))
            thunk = program.thunk_at(*target)
            if thunk:
                note = f"  ; overlay {thunk.overlay}, {thunk.target_segment:04x}:{thunk.target_offset:04x}"
            elif target[0] == program.loader_segment:
                note = f"  ; {program.linker}'s loader"
        elif code[0] == 0xB8 and (said := literal(linear)):
            note = said
        elif any(linear + index in image.relocations for index in range(instruction.size)):
            note = "  ; a segment"
        elif pointer := _POINTER_OPERAND.search(instruction.op_str):
            note = describe(pointer[1] is None, int(pointer[2], 16))
        print(
            f"{segment:04x}:{instruction.address:04x}  {code.hex():<18}"
            f" {instruction.mnemonic} {instruction.op_str}{note}"
        )


# a word at an offset alone, in ES's segment or, with no segment named, in DS's
_POINTER_OPERAND = re.compile(r"word ptr (es:)?\[0x([0-9a-f]+)\]")


def _shown(string: str, limit: int | None = None) -> str:
    """A string on one line, with its line ends and tabs as C writes them."""
    shown = string.replace("\\", "\\\\").replace("\n", "\\n").replace("\t", "\\t")
    return shown if limit is None or len(shown) <= limit else shown[: limit - 3] + "..."


def _literal_notes(folder: GameFolder, program: Program, image: Image):
    """
    What to say beside mov ax,<a word> when the word is a string's: its number, handed to one
    of the routines that take one, or where it is, as the offset of a far pointer to text.
    """
    memory = image.memory
    notice = memory.find(b"MS Run-Time Library")
    try:
        file = data.StringFile(folder.read(folder.one("str.dat")))
    except ValueError:
        file = None
    try:
        namers = find_namers(program)
    except ValueError:
        namers = {}

    def text_at(start: int) -> str:
        end = memory.find(b"\0", start, start + 400)
        text = memory[start:end] if end > start else b""
        if len(text) < 4 or any(byte < 9 or byte > 126 for byte in text):
            return ""
        return f'  ; "{_shown(text.decode("ascii"), 70)}"'

    def describe(linear: int) -> str:
        word = image.word(linear + 1)
        after = memory[linear + 3 : linear + 9]
        # push ax / call far <a routine that takes a string's number>
        if file and after[:2] == b"\x50\x9a" and (image.word(linear + 7), image.word(linear + 5)) in namers:
            block, string = word >> 8, word & 255
            if block < len(file.blocks) and string < file.blocks[block].strings:
                return f"  ; string {block}.{string}: {_shown(file.strings(file.blocks[block])[string], 70)}"
        # mov dx,F000h, here or where a jump from here goes: a string's number, as the offset
        # of a far pointer no text is at
        given = linear + 3
        if after[0] == 0xEB:
            given = linear + 5 + int.from_bytes(after[1:2], "little", signed=True)
        elif after[0] == 0xE9:
            given = linear + 6 + int.from_bytes(after[1:3], "little", signed=True)
        pointed = memory[given : given + 3] == b"\xba\x00\xf0" and given + 1 not in image.relocations
        if file and pointed:
            block, string = pointed_string(word)
            if block < len(file.blocks) and string < file.blocks[block].strings:
                return f"  ; string {block}.{string}: {_shown(file.strings(file.blocks[block])[string], 70)}"
        # mov dx,<segment> / push dx / push ax: a far pointer
        if after[0] == 0xBA and linear + 4 in image.relocations:
            return text_at(image.word(linear + 4) * 16 + word)
        # push ds / push ax: a far pointer into the data
        if after[:2] == b"\x1e\x50" and notice >= 8:
            return text_at(notice - 8 + word)
        return ""

    return describe


def _string_notes(folder: GameFolder, program: Program, overlay: int | None):
    """
    What to say beside an instruction that uses a far pointer in the data at an offset: the
    string it is, when the offset is in an array that the code there loads a block into.
    """
    try:
        file = data.StringFile(folder.read(folder.one("str.dat")))
        names = StringNames(find_loads(program))
    except ValueError:
        return lambda in_ds, offset: ""
    unpacked: dict[int, list[str]] = {}

    def describe(in_ds: bool, offset: int) -> str:
        found = names.array_at(in_ds, offset)
        if found is None:
            return ""
        array, index = found
        blocks = [
            block
            for block in names.blocks(overlay, array)
            if block < len(file.blocks) and index < file.blocks[block].strings
        ]
        if len(blocks) == 1:
            strings = unpacked.setdefault(blocks[0], file.strings(file.blocks[blocks[0]]))
            return f"  ; string {blocks[0]}.{index}: {_shown(strings[index], 70)}"
        if blocks:
            return f"  ; string {index} of block " + " or ".join(str(block) for block in blocks)
        return ""

    return describe


def _named_strings(program: Program, file: data.StringFile) -> None:
    """Each call that names a string by its number, with the string."""
    try:
        namers = find_namers(program)
    except ValueError:
        # no routine takes a string's number: the number is given as a pointer's offset
        found, printer = find_pointed(program, [block.strings for block in file.blocks])
        if not found:
            raise
        print(
            f"a string is named by a far pointer whose segment is {POINTER_SEGMENT:04X}:"
            " its offset is the block times 1024, and the string's place in it"
        )
        if printer:
            print(f"the routine most are handed to, which prints, is at {printer[0]:04x}:{printer[1]:04x}")
        print(f"{len(found)} places name a string that way\n")
        print("place     in          string  kind    text")
    else:
        found = find_named(program)
        for (segment, offset), prints in sorted(namers.items()):
            what = "prints a string by its number" if prints else "gives a string's pointer by its number"
            print(f"the routine that {what} is at {segment:04x}:{offset:04x}")
        print(f"{len(found)} calls name a string by a number that is a constant\n")
        print("call      in          string  kind    text")
    unpacked: dict[int, list[str]] = {}
    for call in found:
        text = "not in the file"
        if call.block < len(file.blocks) and call.string < file.blocks[call.block].strings:
            strings = unpacked.setdefault(call.block, file.strings(file.blocks[call.block]))
            text = _shown(strings[call.string], 60)
        name = f"{call.block}.{call.string}"
        kind = "prints" if call.prints else "data" if call.in_data else "takes"
        print(f"{call.linear:#07x}   {call.where:10}  {name:>6}  {kind:6}  {text}")
    named = {call.block for call in found}
    missing = [str(block.number) for block in file.blocks if block.number not in named]
    print(f"\nblocks no call names: {', '.join(missing) or 'none'}")
    strings = {(call.block, call.string) for call in found}
    total = sum(block.strings for block in file.blocks)
    print(f"{len(strings)} of the file's {total} strings are named")


def loads(args: argparse.Namespace) -> None:
    """Where the program loads each string block, and the array of pointers it loads it into."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    file = data.StringFile(folder.read(folder.one("str.dat")))
    try:
        segment, offset = find_loader(program)
    except ValueError:
        # a program without that routine may name its strings by number
        _named_strings(program, file)
        return
    found = find_loads(program)
    print(
        f"the routine that loads a block is at {segment:04x}:{offset:04x}, and {len(found)} calls go to it\n"
    )
    print("call      in          block  strings  array")
    for load in sorted(found, key=lambda load: (load.overlay or 0, load.linear)):
        block = "?" if load.block is None else str(load.block)
        count = (
            ""
            if load.block is None or load.block >= len(file.blocks)
            else str(file.blocks[load.block].strings)
        )
        if load.array is None:
            array = "the caller's own" if load.local else "?"
        else:
            segment = "ds" if load.array[0] is None else f"{load.array[0]:04x}"
            array = f"{segment}:{load.array[1]:04x}"
        print(f"{load.linear:#07x}   {load.where:10}  {block:>5}  {count:>7}  {array}")

    named = {load.block for load in found if load.block is not None}
    missing = [str(block.number) for block in file.blocks if block.number not in named]
    print(f"\nblocks no call names by number: {', '.join(missing) or 'none'}")


def _object_table(folder: GameFolder, program: Program, words: list[data.Word] | None = None) -> ObjectTable:
    """The game's objects, with their names from OBJECT.DAT when the game has one."""
    names = folder.find("object.dat")
    table = ObjectTable(program, words, folder.read(names) if names else None)
    with contextlib.suppress(ValueError):
        table.strings = data.StringFile(folder.read(folder.one("str.dat")))
    return table


def _named(table: ObjectTable, number: int) -> str:
    """An object's number and name, or its number alone when it isn't an object's."""
    return f"{number} {table.get(number).name}" if 1 <= number <= table.count else str(number)


def _exit_target(program: Program, table: ObjectTable, row: Exit) -> str:
    """Where an exit's pointer goes, by its kind."""
    if row.value is not None:
        if row.kind == 1:
            return f"to {_named(table, row.value)}"
        door = table.door(row.value) if row.kind == 2 else None
        if door:
            return f"to {_named(table, door[0])} by {_named(table, door[1])}"
        if row.kind == 3 and (text := table.exit_text(row.value)) is not None:
            return "says: " + _shown(text, 60)
        opened = table.exit_variable(row.value) if row.kind == 5 else None
        if opened:
            room, variable, text = opened
            shut = _shown(text, 60) if text else "that you can't go that way"
            return f"to {_named(table, room)} when the byte at {variable:04x} isn't 0, or says: {shut}"
        return f"a routine, numbered {row.value}" if row.kind == 4 else str(row.value)
    target = f"{row.segment:04x}:{row.offset:04x}"
    thunk = program.thunk_at(row.segment, row.offset)
    if thunk:
        target += f", a routine in overlay {thunk.overlay}"
    elif row.kind in (1, 2):
        at = row.segment * 16 + row.offset
        target += f", to {_named(table, table.image.word(at))}"
        if row.kind == 2:
            target += f" by {_named(table, table.image.word(at + 2))}"
    elif row.kind == 3:
        memory = table.image.memory
        start = row.segment * 16 + row.offset
        target += ": " + _shown(memory[start : memory.index(b"\0", start)].decode("latin-1"), 60)
    return target


def _routine_of(program: Program, number: int) -> str:
    """An object's routine, or that the code that calls the routines wasn't found."""
    try:
        return str(object_routines(program).get(number) or "none")
    except ValueError as error:
        return f"not known ({error})"


def _show_routines(program: Program, number: int) -> None:
    """An object's routine, and the two other kinds it can have where a game keeps them in tables."""
    print(f"  routine: {_routine_of(program, number)}")
    choosing, text = keyed_routines(program)
    if number in text:
        print(f"  its name or its line in a room is given by: {text[number]}")
    for routine in choosing.get(number, []):
        print(f"  whether a phrase means it, when it could mean others, is said by: {routine}")


def _show_object(program: Program, table: ObjectTable, found: GameObject) -> None:
    entry = table.types[found.type]
    print(f"object {found.number}: {found.name}" + (f" ({found.second_name})" if found.second_name else ""))
    print(f"  type {found.type}, {entry.size} bytes at {found.segment:04x}:{found.offset:04x}")
    print(
        "  flags: "
        + (" ".join(str(flag) for flag in range(1, found.flag_count + 1) if found.has_flag(flag)) or "none")
    )
    known = {
        flag: flag_name(flag, found.type, table.game)
        for flag in range(1, found.flag_count + 1)
        if found.has_flag(flag)
    }
    print("".join(f"    {flag}: {name}\n" for flag, name in known.items() if name), end="")
    if table.words:
        for label, numbers in (("nouns", found.nouns), ("adjectives", found.adjectives)):
            if numbers:
                print(f"  {label}: " + ", ".join(table.words[number - 1].text for number in numbers))
    _show_routines(program, found.number)
    for place in found.places:
        print(
            f"  in {_named(table, place)}"
            + (f", on its list {found.relation}" if found.relation is not None else "")
        )
    if found.next:
        print(f"  next on that list: {_named(table, found.next)}")
    for index, first in enumerate(found.lists):
        members = ", ".join(_named(table, member.number) for member in table.on_list(first))
        print(f"  list {index}: {members or 'empty'}")
    for label, row_of in (("also here", found.also_here), ("seen from here", found.seen_from)):
        if row_of:
            print(
                f"  {label}: "
                + ", ".join(
                    _named(table, number) + ("" if counts else " (not for now)") for number, counts in row_of
                )
            )
    for row in found.exits:
        target = _exit_target(program, table, row)
        direction = DIRECTIONS[row.direction] if row.direction < len(DIRECTIONS) else str(row.direction)
        print(f"  exit {direction}, kind {row.kind}: {target}")
    words = []
    at = 0
    while at < len(found.record):
        if is_pointer(table.image, found, at):
            words.append(f"[{found.word(at + 2):04x}:{found.word(at):04x}]")
            at += 4
        else:
            words.append(f"{found.word(at):04x}")
            at += 2
    print("  record: " + " ".join(words))


def objects(args: argparse.Namespace) -> None:
    """The game's objects as it starts: all of them, one in full, or the rooms with what's in them."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    vocabulary = folder.find("vocab.dat")
    table = _object_table(folder, program, data.vocabulary(folder.read(vocabulary)) if vocabulary else None)
    if args.number is not None:
        _show_object(program, table, table.get(args.number))
        return
    if args.tree:
        inside: set[int] = set()

        def show(found: GameObject, depth: int) -> None:
            if found.number in inside:
                return
            inside.add(found.number)
            print(f"{'  ' * depth}{found.number} {found.name}")
            for index, first in enumerate(found.lists):
                members = [member for member in table.on_list(first) if member.number not in inside]
                if members and len(found.lists) > 1:
                    print(f"{'  ' * (depth + 1)}(list {index})")
                for member in members:
                    show(member, depth + 1)

        for found in table.objects:
            if not found.places and found.lists and any(found.lists):
                show(found, 0)
        return
    if args.saved:
        state = SavedState(program, table)
        file = folder.read(folder.one("restart.dat"))
        timers = state.timers_in(file)
        print(f"{state.header_size:6}  bytes that start it")
        print(f"{state.objects_size:6}  in the records of {table.count} objects")
        print(f"{state.variables_size:6}  in {len(state.variables)} saved variables")
        if state.extra_size:
            print(f"{state.extra_size:6}  in {state.extra_size // 2} words more")
        each = {4: "four", 6: "six"}.get(state.timer_size, str(state.timer_size))
        print(f"{2 + state.timer_size * timers:6}  in a count, {timers}, and {each} bytes for each it counts")
        print(f"{state.size(timers):6}  in all; RESTART.DAT is {len(file)}")
        return
    print(
        "types: "
        + "; ".join(
            f"{entry.type}: {entry.size} bytes, {entry.places} places, {entry.lists} lists"
            for entry in sorted(table.types.values(), key=lambda entry: entry.type)
        )
    )
    print()
    for found in table.objects:
        where = ", ".join(_named(table, place) for place in found.places if place)
        name = found.name + (f" ({found.second_name})" if found.second_name else "")
        print(f"{found.number:4}  {found.type}  {found.flags:04x}  {name:44}  {where}")


def _shapes(forms: list[Form], words: list[data.Word]) -> dict[int, list[str]]:
    """The commands that make each action, by the action's number."""

    def word(number: int) -> str:
        return words[number - 1].text if number else ""

    shapes: dict[int, list[str]] = {}
    for form in forms:
        start = " ".join(part for part in (word(form.verb), word(form.preposition)) if part)
        if form.action_alone:
            shapes.setdefault(form.action_alone, []).append(start)
        if form.action_with_object:
            shapes.setdefault(form.action_with_object, []).append(f"{start} OBJECT")
        for second in form.seconds:
            line = " ".join(part for part in (start, "OBJECT", word(second.preposition), "OBJECT") if part)
            shapes.setdefault(second.action, []).append(line)
    return shapes


def actions(args: argparse.Namespace) -> None:
    """Each action: its routine, the routine that comes before it, and the commands that make it."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    words = data.vocabulary(folder.read(folder.one("vocab.dat")))
    shapes = _shapes(read_grammar(program, words), words)
    routines, before = action_routines(program)
    flags = action_flags(program)
    for number in sorted(routines):
        if args.number not in (None, number):
            continue
        turn = ", and takes no turn" if number <= len(flags) and flags[number - 1] & NO_TURN else ""
        print(f"action {number}: {routines[number]}{turn}")
        if number in before:
            print(f"    first {before[number]}")
        made = shapes.get(number, [])
        if args.number is None and len(made) > 4:
            made = [*made[:4], f"and {len(made) - 4} more"]
        for shape in made or ["no command makes it"]:
            print(f"    {shape}")


def _flag_said(table: ObjectTable, flag: int) -> str:
    """What a flag is: its name, and where the game has a table of flags, its bit and its types."""
    name = describe_flag(flag, table.game)
    if table.flag_table is None or flag not in table.flag_table:
        return name
    bit, kinds = table.flag_table[flag]
    have = [str(kind) for kind in sorted(table.types) if kinds >> (kind - 1) & 1]
    types = "every type" if len(have) == len(table.types) else "type " + ", ".join(have)
    return f"{name} (bit {bit}, for {types})"


def flags(args: argparse.Namespace) -> None:
    """The objects' flags: what has each as the game starts, and where the program tests or changes it."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    words = data.vocabulary(folder.read(folder.one("vocab.dat")))
    table = _object_table(folder, program, words)
    names = {found.number: found.name for found in table.objects}
    shapes = {number: made[0] for number, made in _shapes(read_grammar(program, words), words).items()}
    uses, missed = flag_uses(program, names, shapes)
    most = max(found.flag_count for found in table.objects)
    for flag in range(1, most + 1):
        if args.flag not in (None, flag):
            continue
        have = [found for found in table.objects if flag <= found.flag_count and found.has_flag(flag)]
        about = [use for use in uses if use.flag == flag]
        if not have and not about and args.flag is None:
            continue
        counts = collections.Counter(use.kind for use in about)
        types = collections.Counter(found.type for found in have)
        by_type = ", ".join(f"{count} of type {kind}" for kind, count in sorted(types.items()))
        print(f"flag {flag}: {_flag_said(table, flag)}")
        print(f"    at the start, on {len(have)} objects" + (f": {by_type}" if have else ""))
        print(f"    tested {counts['tests']} times, set {counts['sets']}, cleared {counts['clears']}")
        if args.flag is None:
            continue
        for found in have:
            print(f"    has it: {found.number} {found.name}")
        for use in about:
            where = "the root" if use.overlay is None else f"overlay {use.overlay}"
            at = f"{use.segment:04x}:{use.offset:04x} in {where}"
            print(f"    {at}: {use.routine} {use.kind} it of {use.asked_of}")
    if args.flag is None:
        print(f"\n{len(uses)} calls about a flag; {missed} more aren't read", file=sys.stderr)


def timers(args: argparse.Namespace) -> None:
    """The game's timers: each routine it puts by to run on a later turn, and who starts and stops it."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    words = data.vocabulary(folder.read(folder.one("vocab.dat")))
    table = _object_table(folder, program, words)
    names = {found.number: found.name for found in table.objects}
    shapes = {number: made[0] for number, made in _shapes(read_grammar(program, words), words).items()}
    calls = timer_calls(program, names, shapes)
    # the names of the numbers are each game's own
    numbered = {
        GAME_201: TIMER_NAMES_201,
        GAME_TIMEQUEST: TIMER_NAMES_TIMEQUEST,
        GAME_301: TIMER_NAMES_301,
        GAME_GATEWAY: TIMER_NAMES_GATEWAY,
    }.get(table.game or "", {})
    found = sorted({(call.number or 0, call.timer) for call in calls if call.timer})
    for number, timer in found:
        code = timer_code(program, timer)
        if number:
            name = f": {numbered[number]}" if number in numbered else ""
        else:
            name = f": {TIMER_NAMES[timer]}" if timer in TIMER_NAMES else ""
        said = f"{number}, {timer[0]:04x}:{timer[1]:04x}" if number else f"{timer[0]:04x}:{timer[1]:04x}"
        print(f"timer {said}{name}" + (f", which calls {code}" if code else ""))
        for call in calls:
            if call.timer != timer:
                continue
            where = "the root" if call.overlay is None else f"overlay {call.overlay}"
            what = call.kind
            if call.kind == "starts":
                what += (
                    " it with a count worked out"
                    if call.count is None
                    else " it to run every turn"
                    if call.count == EVERY_TURN
                    else f" it with a count of {call.count}"
                )
            else:
                what += " it"
            print(f"    {call.segment:04x}:{call.offset:04x} in {where}: {call.routine} {what}")
    other = sum(1 for call in calls if call.timer is None)
    print(
        f"\n{len(found)} timers in {len(calls) - other} calls; {other} more calls aren't read",
        file=sys.stderr,
    )


def grammar(args: argparse.Namespace) -> None:
    """The forms a command can take: each verb, its prepositions and objects, and the action made."""
    folder = GameFolder(args.folder)
    words = data.vocabulary(folder.read(folder.one("vocab.dat")))
    forms = read_grammar(_program(folder), words)

    def word(number: int) -> str:
        return words[number - 1].text if number else ""

    for form in forms:
        if args.verb and word(form.verb) != args.verb:
            continue
        start = " ".join(part for part in (word(form.verb) or "(no verb)", word(form.preposition)) if part)
        first = f"  (the object {describe_flags(form.flags)})"
        made = []
        if form.action_alone:
            made.append(f"{start}: action {form.action_alone}")
        if form.action_with_object:
            made.append(f"{start} OBJECT: action {form.action_with_object}{first}")
        for second in form.seconds:
            line = " ".join(part for part in (start, "OBJECT", word(second.preposition), "OBJECT") if part)
            made.append(
                f"{line}: action {second.action}{first[:-1]}, the second {describe_flags(second.flags)})"
            )
        for line in made or [f"{start}: no action"]:
            print(line)
    if not args.verb:
        print(f"\n{len(forms)} forms", file=sys.stderr)


def sentence(args: argparse.Namespace) -> None:
    """The grammar the parser matches a command against, rule by rule, as its writers had it."""
    ways = read_sentence_grammar(_program(GameFolder(args.folder)))
    rules: dict[str, list[list[str]]] = {}
    for way in ways:
        rules.setdefault(way.rule, []).append(way.symbols)
    width = max(len(rule) for rule in rules)
    for rule, made in rules.items():
        for index, symbols in enumerate(made):
            start = f"{rule:{width}} ::=" if index == 0 else f"{'':{width}}   |"
            print(f"{start} {' '.join(symbols) or '(nothing)'}")
        print()


_PACKINGS = {
    RAW_PLANE: "as it is",
    RAW_PLANES: "as it is, in planes",
    HUFFMAN: "Huffman",
    LZW: "LZW",
    IMPLODED: "imploded",
}


def pictures(args: argparse.Namespace) -> None:
    """The pictures in the game's .PIC files: listed, or written to a folder as PNG files."""
    folder = GameFolder(args.folder)
    skipped = 0
    # which game's kind the files are, which one file of them can't always say
    files = [folder.read(path) for path in folder.ending(".pic")]
    later = is_later(files)
    imploded = is_imploded(files)
    known = is_known(files)
    for path in folder.ending(".pic"):
        name = os.path.splitext(os.path.basename(path))[0].lower()
        # the letter before the file's number says which display it's for
        file = PictureFile(
            folder.read(path), ega=name.rstrip("0123456789").endswith("e"), later=later, imploded=imploded
        )
        found = file.pictures()
        # the later games' files say the display in each picture, and not in the file's name
        display = "" if imploded else f", for the {'EGA' if file.ega else 'CGA'}"
        print(f"{os.path.basename(path)}: {len(found)} pictures{display}")
        for entry in found:
            if args.out:
                try:
                    if not known:
                        raise ValueError("the game's pictures are of kinds that aren't known")
                    picture = file.read(entry)
                except ValueError:
                    # packed a way that isn't read yet: the rest are still written
                    skipped += 1
                    continue
                if not picture.pixels:
                    # a palette alone, for the pictures after it
                    continue
                os.makedirs(args.out, exist_ok=True)
                with open(os.path.join(args.out, f"{name}_{entry.number:03}.png"), "wb") as png:
                    png.write(picture.png())
            else:
                packing = _PACKINGS.get(entry.packing, f"{entry.kind:#04x}")
                notes = "" if entry.has_palette and file.ega else ", no palette"
                if entry.places:
                    notes += f", placed {entry.places} time{'s' if entry.places > 1 else ''}"
                print(f"  {entry.number:3}  {entry.width:3} x {entry.height:3}  {packing}{notes}")
    if args.out:
        print(f"written to {args.out}")
    if skipped:
        print(f"{skipped} pictures weren't written: they are packed a way that isn't known")


def regions(args: argparse.Namespace) -> None:
    """The regions of the pictures in the game's .RGN files: what object is where in each."""
    folder = GameFolder(args.folder)
    try:
        table: ObjectTable | None = _object_table(folder, _program(folder))
    except ValueError:
        # the objects' names are a help, and the regions can be listed by number without them
        table = None

    def named(number: int) -> str:
        return _named(table, number) if table else str(number)

    for path in folder.ending(".rgn"):
        print(os.path.basename(path))
        for picture in read_regions(folder.read(path)):
            rest = f", the rest of it {named(picture.object)}" if picture.object else ""
            print(f"  picture {picture.picture}{rest}")
            for region in picture.regions:
                print(
                    f"    {region.left * 2:3},{region.top:3} to {region.right * 2 + 1:3},{region.bottom:3}"
                    f"  {named(region.object)}"
                )


def _write(folder: str, name: str, contents: bytes) -> None:
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, name), "wb") as file:
        file.write(contents)


def music(args: argparse.Namespace) -> None:
    """The songs in the game's .MUS files: listed, or written to a folder as .mid, .rol and .mdi files."""
    folder = GameFolder(args.folder)
    for path in folder.ending(".mus"):
        name = os.path.splitext(os.path.basename(path))[0].lower()
        print(os.path.basename(path))
        for song in read_songs(folder.read(path)):
            kind = "MIDI, for the MT-32" if song.midi else f".{song.extension.upper()}, for the AdLib"
            names = ", ".join(dict.fromkeys(song.names()))
            print(f"  {song.number:2}  {len(song.data):5} bytes  {kind}" + (f": {names}" if names else ""))
            if args.out:
                _write(args.out, f"{name}_{song.number:02}.{song.extension}", song.data)
    if args.out:
        print(f"written to {args.out}")


def sounds(args: argparse.Namespace) -> None:
    """The sounds in the game's .RS files: listed, or written to a folder as WAV files."""
    folder = GameFolder(args.folder)
    for path in folder.ending(".rs"):
        sound = read_sound(folder.read(path))
        times = f"{sound.repeat} time{'s' if sound.repeat > 1 else ''}"
        print(
            f"{os.path.basename(path):14} {len(sound.widths):6} bytes, each played {times}:"
            f" {sound.seconds:5.1f} seconds at {sound.rate:5.0f} a second"
        )
        if args.out:
            _write(args.out, os.path.splitext(os.path.basename(path))[0].lower() + ".wav", sound.wav())
    if args.out:
        print(f"written to {args.out}")


def _games_in(path: str) -> list[tuple[str, str]]:
    """
    The games a folder is or holds, each with a name for it: the folder itself when it has
    pictures, songs or sounds, named for itself or, when it's called `game`, for the folder
    it's in; otherwise each folder in it that is one, or has one called `game`.
    """

    def has_media(folder: str) -> bool:
        return os.path.isdir(folder) and any(
            name.lower().endswith((".pic", ".mus", ".rs")) for name in os.listdir(folder)
        )

    path = os.path.abspath(path)
    if has_media(path):
        named = os.path.basename(path)
        return [(os.path.basename(os.path.dirname(path)) if named.lower() == "game" else named, path)]
    found = []
    for name in sorted(os.listdir(path)) if os.path.isdir(path) else []:
        for folder in (os.path.join(path, name), os.path.join(path, name, "game")):
            if has_media(folder):
                # a game's own folder given, with its files in the `game` inside it
                found.append((os.path.basename(path) if name.lower() == "game" else name, folder))
                break
    return found


def extract(args: argparse.Namespace) -> None:
    """A game's pictures, sounds and music, or those of every game in a folder, as PNG, WAV and MIDI files."""
    games = _games_in(args.folder)
    if not games:
        raise ValueError(f"{args.folder}: no game's pictures, songs or sounds are there")
    # beside the tools, where the repository ignores it: what's written is the games' own
    out = args.out or os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "media")
    for name, folder in games:
        counts = []
        for kind, write in (("pictures", pictures), ("sounds", sounds), ("music", music)):
            into = os.path.join(out, name, kind)
            said = io.StringIO()
            try:
                # each lists what it writes, which is its own command's to say
                with contextlib.redirect_stdout(said):
                    write(argparse.Namespace(folder=folder, out=into))
            except (ValueError, IndexError, struct.error) as error:
                counts.append(f"{kind} not read ({error})")
                continue
            written = len(os.listdir(into)) if os.path.isdir(into) else 0
            what = {"pictures": "picture", "sounds": "sound", "music": "song"}[kind]
            missed = re.search(r"^(\d+) pictures weren't written", said.getvalue(), re.MULTILINE)
            counts.append(
                f"{written} {what}{'' if written == 1 else 's'}"
                + (f" and {missed[1]} more not read" if missed else "")
            )
        # a page to look through them, when there is anything to look through
        folders = [os.path.join(out, name, kind) for kind in ("pictures", "sounds", "music")]
        if any(os.path.isdir(kind) and os.listdir(kind) for kind in folders):
            page = write_index(os.path.join(out, name), name)
            print(f"{name}: {', '.join(counts)}; to look through them, open {page}")
        else:
            print(f"{name}: {', '.join(counts)}")


def strings(args: argparse.Namespace) -> None:
    """The table of string blocks that starts ...STR.DAT, or the words its text is packed with."""
    folder = GameFolder(args.folder)
    file = data.StringFile(folder.read(folder.one("str.dat")))
    if args.words:
        for number, word in enumerate(file.words, 128):
            print(f"{number:3}  {word!r}")
        return
    print("block  unpacked  packed  strings  at")
    # a file that doesn't say a block's size unpacked has it counted, a zero to each string
    sizes = [
        block.unpacked_size or sum(len(string) + 1 for string in file.strings(block)) for block in file.blocks
    ]
    for block, size in zip(file.blocks, sizes):
        print(f"{block.number:5}  {size:8}  {block.packed_size:6}  {block.strings:7}  {block.offset:#07x}")
    print(
        f"\n{len(file.blocks)} blocks, {sum(block.strings for block in file.blocks)} strings,"
        f" {sum(sizes)} bytes packed into"
        f" {sum(block.packed_size for block in file.blocks)},"
        f" with a tree of {len(file.tree) // 2} nodes and {len(file.words)} words"
    )


def text(args: argparse.Namespace) -> None:
    """The text in ...STR.DAT: every block's strings, or one block's."""
    folder = GameFolder(args.folder)
    file = data.StringFile(folder.read(folder.one("str.dat")))
    blocks = file.blocks if args.block is None else [file.block(args.block)]
    for block in blocks:
        for number, string in enumerate(file.strings(block)):
            print(f"{block.number}.{number}  {_shown(string)}")


def vocab(args: argparse.Namespace) -> None:
    """The vocabulary in VOCAB.DAT: each word, its parts of speech, and the word it stands for."""
    folder = GameFolder(args.folder)
    words = data.vocabulary(folder.read(folder.one("vocab.dat")))
    for word in words:
        line = f"{word.number:4}  {word.text:18}  {data.parts_of_speech(word.parts)}"
        if word.synonym:
            line += f"; for {words[word.synonym - 1].text} as {data.parts_of_speech(word.synonym_parts)}"
        print(line)


def main() -> None:
    parser = argparse.ArgumentParser(
        prog="legendary-tools",
        description="Take a Legend Entertainment game apart. Every command takes the game's folder.",
    )
    commands = parser.add_subparsers(dest="command", required=True, metavar="<command>")

    def command(function, *, overlay: str | None = None):
        sub = commands.add_parser(function.__name__, help=function.__doc__, description=function.__doc__)
        sub.add_argument("folder", help="the game's folder, the one with its ...MAIN.EXE in it")
        sub.set_defaults(run=function)
        if overlay == "many":
            sub.add_argument(
                "--overlay",
                type=int,
                action="append",
                default=[],
                metavar="N",
                help="an overlay to have loaded; give it again for another",
            )
        elif overlay == "one":
            sub.add_argument("--overlay", type=int, metavar="N", help="only this overlay's")
        return sub

    command(exe)
    command(overlays)
    command(thunks, overlay="one")
    command(segments, overlay="many")
    command(refs).add_argument("segment", help="the segment, in hex")
    sub = command(dump, overlay="many")
    sub.add_argument("address", help="segment:offset, in hex")
    sub.add_argument("count", type=int, nargs="?", default=256, help="how many bytes (256)")
    sub = command(dis, overlay="many")
    sub.add_argument("address", help="segment:offset, in hex")
    sub.add_argument("count", type=int, nargs="?", default=40, help="how many instructions (40)")
    command(actions).add_argument(
        "number", type=int, nargs="?", help="only this action, with every command that makes it"
    )
    command(grammar).add_argument("verb", nargs="?", help="only this verb's forms")
    command(timers)
    command(flags).add_argument(
        "flag", type=int, nargs="?", help="only this flag, with every object that has it and every call"
    )
    command(loads)
    command(music).add_argument(
        "--out", metavar="FOLDER", help="write each song there as a .mid or .rol file"
    )
    command(sounds).add_argument("--out", metavar="FOLDER", help="write each sound there as a WAV file")
    command(pictures).add_argument("--out", metavar="FOLDER", help="write each picture there as a PNG file")
    command(extract).add_argument(
        "--out",
        metavar="FOLDER",
        help="write them there, a folder to a game (media, beside the tools)",
    )
    command(regions)
    command(sentence)
    sub = command(objects)
    sub.add_argument("number", type=int, nargs="?", help="only this object, in full")
    sub.add_argument("--tree", action="store_true", help="the rooms and holders, with what's in each")
    sub.add_argument("--saved", action="store_true", help="what RESTART.DAT has, by its parts")
    command(strings).add_argument(
        "--words", action="store_true", help="list the words the text is packed with, not the blocks"
    )
    command(text).add_argument("block", type=int, nargs="?", help="only this block's")
    command(vocab)

    args = parser.parse_args()
    try:
        args.run(args)
        sys.stdout.flush()
    except (ValueError, OSError) as error:
        if isinstance(error, BrokenPipeError):
            # the reader, head perhaps, had enough: leave quietly, with nothing more to flush
            os.dup2(os.open(os.devnull, os.O_WRONLY), sys.stdout.fileno())
        else:
            raise SystemExit(str(error)) from None

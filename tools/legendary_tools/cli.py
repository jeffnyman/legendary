"""The commands: python -m legendary_tools <command> <the game's folder> ..."""

from __future__ import annotations

import argparse
import collections
import os
import re
import sys

from . import data
from .flags import describe_flag, flag_name, flag_uses
from .game import GameFolder
from .grammar import NO_TURN, Form, action_flags, describe_flags, read_grammar
from .handlers import action_routines, object_routines
from .loads import StringNames, find_loader, find_loads
from .mz import MzExecutable
from .objects import DIRECTIONS, Exit, GameObject, ObjectTable, SavedState, is_pointer
from .pictures import HUFFMAN, LZW, RAW_PLANE, RAW_PLANES, PictureFile
from .plink86 import Plink86Program
from .program import Image, Program
from .regions import read_regions
from .rtlink import RtlinkProgram, is_rtlink, library_name
from .sentence import read_sentence_grammar
from .sound import read_songs, read_sound
from .timers import EVERY_TURN, TIMER_NAMES, timer_calls, timer_code


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


def _string_notes(folder: GameFolder, program: Program, overlay: int | None):
    """
    What to say beside an instruction that uses a far pointer in the data at an offset: the
    string it is, when the offset is in an array that the code there loads a block into.
    """
    try:
        names = StringNames(find_loads(program))
        file = data.StringFile(folder.read(folder.one("str.dat")))
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


def loads(args: argparse.Namespace) -> None:
    """Where the program loads each string block, and the array of pointers it loads it into."""
    folder = GameFolder(args.folder)
    program = _program(folder)
    file = data.StringFile(folder.read(folder.one("str.dat")))
    segment, offset = find_loader(program)
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
    return ObjectTable(program, words, folder.read(names) if names else None)


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


def _show_object(program: Program, table: ObjectTable, found: GameObject) -> None:
    entry = table.types[found.type]
    print(f"object {found.number}: {found.name}" + (f" ({found.second_name})" if found.second_name else ""))
    print(f"  type {found.type}, {entry.size} bytes at {found.segment:04x}:{found.offset:04x}")
    print(
        "  flags: "
        + (" ".join(str(flag) for flag in range(1, found.flag_count + 1) if found.has_flag(flag)) or "none")
    )
    # the names are Spellcasting 101's flags', which the other games number another way
    known = {
        flag: flag_name(flag, found.type)
        for flag in range(1, found.flag_count + 1)
        if found.has_flag(flag) and table.flags_named
    }
    print("".join(f"    {flag}: {name}\n" for flag, name in known.items() if name), end="")
    if table.words:
        for label, numbers in (("nouns", found.nouns), ("adjectives", found.adjectives)):
            if numbers:
                print(f"  {label}: " + ", ".join(table.words[number - 1].text for number in numbers))
    print(f"  routine: {_routine_of(program, found.number)}")
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
        print(f"{4:6}  bytes that start it")
        print(f"{state.objects_size:6}  in the records of {table.count} objects")
        print(f"{state.variables_size:6}  in {len(state.variables)} saved variables")
        print(f"{2 + 6 * timers:6}  in a count, {timers}, and six bytes for each it counts")
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
        print(f"flag {flag}: {describe_flag(flag)}")
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
    names = {found.number: found.name for found in _object_table(folder, program, words).objects}
    shapes = {number: made[0] for number, made in _shapes(read_grammar(program, words), words).items()}
    calls = timer_calls(program, names, shapes)
    found = sorted({call.timer for call in calls if call.timer})
    for timer in found:
        code = timer_code(program, timer)
        name = f": {TIMER_NAMES[timer]}" if timer in TIMER_NAMES else ""
        print(f"timer {timer[0]:04x}:{timer[1]:04x}{name}" + (f", which calls {code}" if code else ""))
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


_PACKINGS = {RAW_PLANE: "as it is", RAW_PLANES: "as it is, in planes", HUFFMAN: "Huffman", LZW: "LZW"}


def pictures(args: argparse.Namespace) -> None:
    """The pictures in the game's .PIC files: listed, or written to a folder as PNG files."""
    folder = GameFolder(args.folder)
    for path in folder.ending(".pic"):
        name = os.path.splitext(os.path.basename(path))[0].lower()
        # the letter before the file's number says which display it's for
        file = PictureFile(folder.read(path), ega=name.rstrip("0123456789").endswith("e"))
        found = file.pictures()
        print(f"{os.path.basename(path)}: {len(found)} pictures, for the {'EGA' if file.ega else 'CGA'}")
        for entry in found:
            if args.out:
                picture = file.read(entry)
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
    """The songs in the game's .MUS files: listed, or written to a folder as .mid and .rol files."""
    folder = GameFolder(args.folder)
    for path in folder.ending(".mus"):
        name = os.path.splitext(os.path.basename(path))[0].lower()
        print(os.path.basename(path))
        for song in read_songs(folder.read(path)):
            kind = "MIDI, for the MT-32" if song.midi else ".ROL, for the AdLib"
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


def strings(args: argparse.Namespace) -> None:
    """The table of string blocks that starts ...STR.DAT, or the words its text is packed with."""
    folder = GameFolder(args.folder)
    file = data.StringFile(folder.read(folder.one("str.dat")))
    if args.words:
        for number, word in enumerate(file.words, 128):
            print(f"{number:3}  {word!r}")
        return
    print("block  unpacked  packed  strings  at")
    for block in file.blocks:
        print(
            f"{block.number:5}  {block.unpacked_size:8}  {block.packed_size:6}  {block.strings:7}"
            f"  {block.offset:#07x}"
        )
    print(
        f"\n{len(file.blocks)} blocks, {sum(block.strings for block in file.blocks)} strings,"
        f" {sum(block.unpacked_size for block in file.blocks)} bytes packed into"
        f" {sum(block.packed_size for block in file.blocks)},"
        f" with a tree of {len(file.tree)} nodes and {len(file.words)} words"
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

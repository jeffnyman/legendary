"""The commands: python -m legendary_tools <command> <the game's folder> ..."""

from __future__ import annotations

import argparse
import os
import re
import sys

from . import data
from .game import GameFolder
from .grammar import read_grammar
from .loads import StringNames, find_loader, find_loads
from .mz import MzExecutable
from .objects import Exit, GameObject, ObjectTable, SavedState, is_pointer
from .plink86 import Image, Plink86Program


def _program(folder: GameFolder) -> Plink86Program:
    exe = folder.read(folder.main_program())
    ovl = folder.ending("main.ovl")
    try:
        return Plink86Program(exe, folder.read(ovl[0]) if ovl else None)
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
    """PLINK86's table of overlays, by where they load."""
    program = _program(GameFolder(args.folder))
    mz = program.mz
    print(
        f"root      0000-{mz.image_paragraphs:04x}  {len(mz.image)} bytes, {len(mz.relocations)} relocations"
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
                note = "  ; PLINK86's loader"
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


def _string_notes(folder: GameFolder, program: Plink86Program, overlay: int | None):
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


def _named(table: ObjectTable, number: int) -> str:
    """An object's number and name, or its number alone when it isn't an object's."""
    return f"{number} {table.get(number).name}" if 1 <= number <= table.count else str(number)


def _exit_target(program: Plink86Program, table: ObjectTable, row: Exit) -> str:
    """Where an exit's pointer goes, by its kind."""
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


def _show_object(program: Plink86Program, table: ObjectTable, found: GameObject) -> None:
    entry = table.types[found.type]
    print(f"object {found.number}: {found.name}" + (f" ({found.second_name})" if found.second_name else ""))
    print(f"  type {found.type}, {entry.size} bytes at {found.segment:04x}:{found.offset:04x}")
    print(
        "  flags: "
        + (" ".join(str(flag) for flag in range(1, found.flag_count + 1) if found.has_flag(flag)) or "none")
    )
    if table.words:
        for label, numbers in (("nouns", found.nouns), ("adjectives", found.adjectives)):
            if numbers:
                print(f"  {label}: " + ", ".join(table.words[number - 1].text for number in numbers))
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
    for row in found.exits:
        target = _exit_target(program, table, row)
        print(f"  exit {row.direction:2}, kind {row.kind}: {target}")
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
    table = ObjectTable(program, data.vocabulary(folder.read(vocabulary)) if vocabulary else None)
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
        made = []
        if form.action_alone:
            made.append(f"{start}: action {form.action_alone}")
        if form.action_with_object:
            made.append(f"{start} OBJECT: action {form.action_with_object}")
        for second in form.seconds:
            line = " ".join(part for part in (start, "OBJECT", word(second.preposition), "OBJECT") if part)
            made.append(f"{line}: action {second.action}  [{second.flags:02x} {second.unknown}]")
        for line in made or [f"{start}: no action"]:
            print(f"{form.flags:02x}  {line}")
    if not args.verb:
        print(f"\n{len(forms)} forms", file=sys.stderr)


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
    command(grammar).add_argument("verb", nargs="?", help="only this verb's forms")
    command(loads)
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

"""The commands: python -m legendary_tools <command> <the game's folder> ..."""

from __future__ import annotations

import argparse
import os
import sys

from . import data
from .game import GameFolder
from .mz import MzExecutable
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
        print(
            f"{segment:04x}:{instruction.address:04x}  {code.hex():<18}"
            f" {instruction.mnemonic} {instruction.op_str}{note}"
        )


def strings(args: argparse.Namespace) -> None:
    """The table of string blocks that starts ...STR.DAT."""
    folder = GameFolder(args.folder)
    contents = folder.read(folder.one("str.dat"))
    blocks = data.string_blocks(contents)
    print("block  unpacked  packed  strings")
    for block in blocks:
        print(f"{block.number:5}  {block.unpacked_size:8}  {block.packed_size:6}  {block.strings:7}")
    print(
        f"\n{len(blocks)} blocks, {sum(block.strings for block in blocks)} strings,"
        f" {sum(block.unpacked_size for block in blocks)} bytes packed into"
        f" {sum(block.packed_size for block in blocks)}; the file is {len(contents)}"
    )


def vocab(args: argparse.Namespace) -> None:
    """The words that start VOCAB.DAT, in the file's order."""
    folder = GameFolder(args.folder)
    contents = folder.read(folder.one("vocab.dat"))
    words, end = data.vocabulary(contents)
    for number, word in enumerate(words):
        print(f"{number:4}  {word}")
    print(f"\n{len(words)} words, ending at {end:#x} of {len(contents):#x}", file=sys.stderr)


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
    command(strings)
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

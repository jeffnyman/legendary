"""The game's timers: the routines it has put by to run on a later turn, or on every turn.

The game keeps a table of up to sixteen timers, six bytes each: a far pointer to a routine,
and a count. After each turn that takes time, one routine goes down the table. A timer whose
count is above zero has it taken down by one. A timer whose count is zero or less has its
routine called, and afterward is taken out of the table, unless its count is -1, which is a
routine that runs every turn, or the routine has put itself back with a new count.

Four routines beside that one keep the table: one empties it, one puts a routine in it with a
count or gives one already there a new count, one takes a routine out, and one says whether a
routine is in it. The game's source called them with the routine as a constant, so every call
says which timer it is about, and whose routine the call is in says who starts and stops it.

A timer's routine is in the root, where the table's far pointer can always find it. Most are
six bytes there, a call to the routine that does the work, in an overlay, and a return.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

from .flags import CodeReader, routine_labels
from .plink86 import Plink86Program

# Spellcasting 101's: the segment the routines are in, and where each starts
SEGMENT = 0x1272
ROUTINES = {0x000E: "empties", 0x0033: "starts", 0x00BF: "stops", 0x013E: "asks about"}
# where the table is, how many it holds, and where in the data its count of timers is
TABLE = (0x3B00, 0x4F30)
MOST = 16
COUNT_OFFSET = 0x7858
EVERY_TURN = -1
# a name for each, from reading its routine
TIMER_NAMES = {
    (0x07EC, 0x04B4): "the simulation ends",
    (0x07EC, 0x06D9): "the stepfather on the stairs",
    (0x07EC, 0x06DF): "the man with the whip",
    (0x07EC, 0x06E5): "the potty door closes",
    (0x07EC, 0x06EB): "the voice at the gate",
    (0x07EC, 0x06F1): "Miss Beancounter's spectacles",
    (0x07EC, 0x06F7): "the amazon leader",
    (0x07EC, 0x06FD): "the amazons' visits",
    (0x07EC, 0x0703): "the chambermaid",
    (0x07EC, 0x0709): "the waiter comes",
    (0x07EC, 0x070F): "the waiter brings it",
    (0x07EC, 0x0715): "the islanders gather",
    (0x07EC, 0x071B): "the islanders' thanks",
    (0x07EC, 0x0721): "Ocarina at home",
    (0x07EC, 0x0727): "Glockenspiel comes back",
    (0x07EC, 0x072D): "the gods quarrel",
    (0x07EC, 0x0733): "the gold gate shuts",
    (0x07EC, 0x0739): "the silver gate shuts",
    (0x07EC, 0x073F): "the bronze gate shuts",
    (0x07EC, 0x0745): "the ending",
    (0x07EC, 0x074B): "to Batguano Court",
    (0x07EC, 0x075A): "the role-players' game",
    (0x07EC, 0x0760): "meals at the Cafeteria",
    (0x07EC, 0x0766): "the Stadium's day",
    (0x07EC, 0x076C): "the professor tires",
    (0x07EC, 0x0772): "the party starts",
    (0x07EC, 0x0778): "the party's chatter",
    (0x07EC, 0x077E): "Gretchen with you",
    (0x07EC, 0x0784): "Gretchen arrives",
    (0x07EC, 0x078A): "the fratman",
    (0x07EC, 0x07D2): "dinner with Hillary",
    (0x07EC, 0x07D8): "the class bell",
    (0x07EC, 0x07DE): "the dinner invitation",
    (0x07EC, 0x07E4): "Tickingclock comes in",
    (0x07EC, 0x07EA): "the lecture",
    (0x07EC, 0x07F0): "the day's first look",
    (0x07EC, 0x089F): "a word forgotten",
    (0x07EC, 0x08AD): "a byte forgotten",
    (0x07EC, 0x08BA): "FRIMP wears off",
    (0x07EC, 0x09F3): "sleepiness",
    (0x07EC, 0x128A): "ZEM wears off",
    (0x07EC, 0x12A3): "the dragon's breath",
    (0x07EC, 0x12A9): "the pendulum",
    (0x09BB, 0x0D1E): "the red button resets",
    (0x09BB, 0x0D26): "the surfboard's trip",
    (0x3670, 0x0899): "the registration line",
}


@dataclass(frozen=True)
class TimerCall:
    """One call about a timer."""

    kind: str  # starts, stops or asks about
    timer: tuple[int, int] | None  # the timer's routine, in the root; None when it isn't a constant
    count: int | None  # for a start, the count it's given, when that is a constant
    overlay: int | None  # the overlay the call is in, or None for the root
    segment: int
    offset: int
    routine: str  # whose routine the call is in


def timer_code(program: Plink86Program, timer: tuple[int, int]) -> str | None:
    """Where a timer's work is done, when its routine in the root is only a call to there and a return."""
    image = program.image()
    at = timer[0] * 16 + timer[1]
    if image.memory[at] != 0x9A or image.memory[at + 5] != 0xCB or at + 3 not in image.relocations:
        return None
    offset, segment = struct.unpack_from("<HH", image.memory, at + 1)
    thunk = program.thunk_at(segment, offset)
    if thunk:
        return f"{thunk.target_segment:04x}:{thunk.target_offset:04x} in overlay {thunk.overlay}"
    return f"{segment:04x}:{offset:04x} in the root"


def timer_calls(program: Plink86Program, names: dict[int, str], shapes: dict[int, str]) -> list[TimerCall]:
    """Every call that starts, stops or asks about a timer, in the root and in each overlay."""
    labels = routine_labels(program, names, shapes)
    root = program.image()
    readers = [CodeReader(root, 0, len(program.mz.image), None, labels.get(None, {}))]
    for overlay in program.overlays:
        if not overlay.preloaded:
            start, end = overlay.load_segment * 16, overlay.end_segment * 16
            image = program.image(overlay.number)
            readers.append(CodeReader(image, start, end, overlay.number, labels.get(overlay.number, {})))
    routines = {SEGMENT * 16 + offset: kind for offset, kind in ROUTINES.items()}
    calls = []
    for reader in readers:
        for at, kind in reader.calls(routines, SEGMENT):
            before = reader.memory[at - 12 : at]
            timer = count = None
            # mov ax,<offset> / mov dx,<segment> / push dx / push ax: the routine, pushed last
            if before[4] == 0xB8 and before[7] == 0xBA and before[10:] == b"\x52\x50":
                offset, segment = struct.unpack_from("<HxH", before, 5)
                timer = (segment, offset)
                # mov ax,<count> / push ax, before it
                if kind == "starts" and before[0] == 0xB8 and before[3] == 0x50:
                    count = struct.unpack_from("<h", before, 1)[0]
            base = reader.segment_of(at)
            calls.append(
                TimerCall(kind, timer, count, reader.overlay, base, at - base * 16, reader.routine(at))
            )
    return calls

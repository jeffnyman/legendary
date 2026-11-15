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

*Spellcasting 201* numbers its timers. Its table has four bytes for each of the sixteen, a
timer's number and its count, and a second table has a far pointer for each number, which
is the routine or a thunk to it. A fifth routine there gives a timer's count.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass

from .flags import CodeReader, routine_labels
from .program import Program

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


# mov es,[segment variable] / call far es:[bx-4], with BX four times a timer's number: its
# routine, called from the table of them
_BY_NUMBER = re.compile(rb"\x8e\x06(..)\x26\xff\x9f\xfc\xff", re.DOTALL)
_PROLOGUE = b"\x55\x8b\xec"
# what the routines are where timers are numbers, in the order they come in their segment,
# before the one that runs them
_KINDS = ("starts", "stops", "asks about", "asks the count of")
# xor ax,ax / call far: how the one before them starts, which empties the table and has no
# arguments or variables to make a frame for
_FRAMELESS = b"\x33\xc0\x9a"
_NOTICE = b"MS Run-Time Library"


@dataclass(frozen=True)
class Timers:
    """Where a game's timers are kept: the routines' segment, each routine, and the numbered ones."""

    segment: int
    routines: dict[int, str]  # where each routine starts in memory, and what it does
    numbered: (
        tuple[tuple[int, int], ...] | None
    )  # the routine of each number, from 1, where they have numbers


def find_timers(program: Program) -> Timers:
    """The routines that keep the timers, where Spellcasting 101 has them or by a table of numbers."""
    image = program.image()
    memory = image.memory
    framed = [offset for offset, kind in ROUTINES.items() if kind != "empties"]
    if all(memory.startswith(_PROLOGUE, SEGMENT * 16 + offset) for offset in framed):
        return Timers(SEGMENT, {SEGMENT * 16 + offset: kind for offset, kind in ROUTINES.items()}, None)

    root = memory[: program.root_size]
    by_number = _BY_NUMBER.search(root)
    notice = memory.find(_NOTICE)
    if not by_number or notice < 0:
        raise ValueError("the routines that keep the timers weren't found")
    segments = sorted(image.segment_references())

    def segment_of(linear: int) -> int:
        return max(segment for segment in segments if segment * 16 <= linear)

    # the routine that calls a timer by its number, and the one call to it, from the routine
    # that runs the timers
    start = root.rindex(_PROLOGUE, 0, by_number.start())
    base = segment_of(start)
    call = root.find(bytes([0x9A, *struct.pack("<HH", start - base * 16, base)]))
    if call < 0:
        raise ValueError("the routine that runs the timers wasn't found")
    segment = segment_of(call)
    runs = root.rindex(_PROLOGUE, segment * 16, call)
    starts = [
        match.start()
        for match in re.finditer(re.escape(_PROLOGUE), root[:runs])
        if match.start() >= segment * 16
    ]
    empties = root.find(_FRAMELESS, segment * 16, starts[0] if starts else runs)
    if len(starts) != len(_KINDS) or empties < 0:
        raise ValueError("the routines that keep the timers aren't the ones that are known")
    routines = {empties: "empties", **dict(zip(starts, _KINDS))}

    table = image.word(notice - 8 + int.from_bytes(by_number[1], "little")) * 16
    numbered = []
    while table + 4 * len(numbered) + 2 in image.relocations:
        offset, target = struct.unpack_from("<HH", memory, table + 4 * len(numbered))
        numbered.append((target, offset))
    return Timers(segment, routines, tuple(numbered))


# Spellcasting 201's, by number, from the same reading: most from what its routine prints
TIMER_NAMES_201 = {
    1: "the day's initiation",
    2: "the upperclassmen find you",
    3: "the serpent heads speak",
    4: "the simulation ends",
    5: "sleepiness",
    6: "the class bell",
    7: "the tower clock chimes",
    8: "nothing",
    9: "a word forgotten",
    10: "a byte forgotten",
    11: "what was raised sinks back",
    12: "the simpleberries wear off",
    13: "the speed potion wears off",
    14: "the mascot swells back",
    15: "the party starts",
    16: "you open your eyes",
    17: "the Brus make ready",
    18: "the Poolside Party",
    19: "Hiddenmolar and Cowpatty whisper",
    20: "word of Tickingclock's death",
    21: "Tickingclock's errand",
    22: "Tickingclock goes",
    23: "the sewer gurgles",
    24: "the last newspaper goes",
    25: "a second byte forgotten",
    26: "the Appliance runs",
    27: "the Sorcerer returns",
    28: "the banana fetches water",
    29: "the army of bananas",
    30: "a knock from the yard",
    31: "twelve bytes forgotten",
    32: "everyone out of the pool",
    33: "the Welcome Back Party fills",
    34: "the Royal Parade",
    35: "the hellhounds come",
    36: "the bier is brought in",
    37: "you are yourself again",
    38: "Hiddenmolar tells all",
    39: "the messenger",
    40: "the cloak is finished",
    41: "the cloak is brought",
    42: "the iron turns to copper",
    43: "the copper dissolves",
    44: "the hanging",
    45: "the cook and the hors d'oevres",
    46: "the tree is a sapling again",
    47: "the Social Hall, turn by turn",
    48: "a count down by sixes",
    49: "seen in the Ancient Lab",
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
    number: int | None = None  # the timer's number, where they have numbers


def timer_code(program: Program, timer: tuple[int, int]) -> str | None:
    """
    Where a timer's work is done, when its routine in the root is only a call to there and a
    return, or when what the table has for it is a thunk.
    """
    thunk = program.thunk_at(*timer)
    if thunk:
        return f"{thunk.target_segment:04x}:{thunk.target_offset:04x} in overlay {thunk.overlay}"
    image = program.image()
    at = timer[0] * 16 + timer[1]
    if image.memory[at] != 0x9A or image.memory[at + 5] != 0xCB or at + 3 not in image.relocations:
        return None
    offset, segment = struct.unpack_from("<HH", image.memory, at + 1)
    thunk = program.thunk_at(segment, offset)
    if thunk:
        return f"{thunk.target_segment:04x}:{thunk.target_offset:04x} in overlay {thunk.overlay}"
    return f"{segment:04x}:{offset:04x} in the root"


def _named(before: bytes, kind: str, timers: Timers) -> tuple[tuple[int, int] | None, int | None, int | None]:
    """The timer a call names, the count it starts it with, and its number, from the code before it."""
    timer = count = number = None
    if timers.numbered is None:
        # mov ax,<offset> / mov dx,<segment> / push dx / push ax: the routine, pushed last
        if before[4] == 0xB8 and before[7] == 0xBA and before[10:] == b"\x52\x50":
            offset, segment = struct.unpack_from("<HxH", before, 5)
            timer = (segment, offset)
    elif before[8] == 0xB8 and before[11] == 0x50:
        # mov ax,<number> / push ax: the number, pushed last
        (number,) = struct.unpack_from("<H", before, 9)
        if not 1 <= number <= len(timers.numbered):
            return None, None, None
        timer = timers.numbered[number - 1]
    # mov ax,<count> / push ax, before it
    at = 0 if timers.numbered is None else 4
    if timer and kind == "starts" and before[at] == 0xB8 and before[at + 3] == 0x50:
        count = struct.unpack_from("<h", before, at + 1)[0]
    return timer, count, number


def timer_calls(program: Program, names: dict[int, str], shapes: dict[int, str]) -> list[TimerCall]:
    """Every call that starts, stops or asks about a timer, in the root and in each overlay."""
    timers = find_timers(program)
    root = program.image()
    labels = routine_labels(program, names, shapes)
    readers = [CodeReader(root, 0, program.root_size, None, labels.get(None, {}))]
    for overlay in program.overlays:
        if not overlay.preloaded:
            start, end = overlay.load_segment * 16, overlay.end_segment * 16
            image = program.image(overlay.number)
            readers.append(CodeReader(image, start, end, overlay.number, labels.get(overlay.number, {})))
    calls = []
    for reader in readers:
        for at, kind in reader.calls(timers.routines, timers.segment):
            timer, count, number = _named(reader.memory[at - 12 : at], kind, timers)
            base = reader.segment_of(at)
            calls.append(
                TimerCall(
                    kind, timer, count, reader.overlay, base, at - base * 16, reader.routine(at), number
                )
            )
    return calls

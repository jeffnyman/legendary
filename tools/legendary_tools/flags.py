"""What the program does with the objects' flags: every place it tests, sets or clears one.

An object's record has its flags from offset 8, a bit each. The program never reads them
there. Three routines, one after another, do it all, each given an object and a flag's
number: one tests the flag, one sets it and one clears it. The game's source called them
with the flag as a constant, so each call says which flag it is about, and whose routine the
call is in says what the flag is for: the routine for `open` sets flag 12, so 12 is open.

The calls are found here by their bytes. The compiler pushed the flag and then the object,
and then called, far from another segment or near from the routines' own.
"""

from __future__ import annotations

import bisect
import re
from dataclasses import dataclass

from .handlers import action_routines, object_routines
from .plink86 import Image, Plink86Program

# what the flags are, as Spellcasting 101's code uses them. A flag that isn't here is one
# that nothing has and nothing asks about
FLAG_NAMES = {
    2: "its name takes `an`",
    3: "plural: `some`, and `they`",
    4: "takes `some`, and is still `it`",
    5: "its name takes no article",
    6: "seen by the player",
    7: "not listed among a room's contents",
    8: "taking it is special",
    9: "gives light",
    10: "touched",
    11: "a door",
    12: "open",
    13: "locked",
    14: "can't be opened or closed",
    15: "a container",
    16: "left out of `all`",
    17: "lit",
    19: "can be seen through",
    20: "can be read",
    21: "can be got in or on",
    22: "burns",
    23: "can be taken",
    24: "can be worn",
    25: "a key",
    26: "cuts",
    27: "cuts",
    28: "can be eaten",
    29: "what's dropped in it lands outside it",
    30: "magic: a spell, its box, or the spell book",
    31: "can be lit",
    32: "to be looked for",
    33: "things can be on it",
}

# four of them are something else for a place, type 1, or a person, type 7
PLACE_FLAG_NAMES = {7: "outdoors", 10: "visited"}
PERSON_FLAG_NAMES = {8: "can be asked things", 9: "female"}


def flag_name(flag: int, kind: int | None = None) -> str | None:
    """What a flag is, for an object of a type if one is given; None when it isn't known."""
    special = PLACE_FLAG_NAMES if kind == 1 else PERSON_FLAG_NAMES if kind == 7 else {}
    return special.get(flag) or FLAG_NAMES.get(flag)


def describe_flag(flag: int) -> str:
    """What a flag is, for every type at once."""
    name = FLAG_NAMES.get(flag, "not known")
    for label, special in (("a place", PLACE_FLAG_NAMES), ("a person", PERSON_FLAG_NAMES)):
        if flag in special:
            name += f"; for {label}, {special[flag]}"
    return name


# the three routines end alike. Each has the flag's byte in es:[bx+di] and its bit in cl:
# mov al,es:[bx+di] / sub ah,ah / mov cl,[bp-8] / shr ax,cl / and ax,1
_TESTS = bytes.fromhex("268a01 2ae4 8a4ef8 d3e8 250100")
# mov al,1 / mov cl,[bp-8] / shl al,cl / or es:[bx+di],al
_SETS = bytes.fromhex("b001 8a4ef8 d2e0 260801")
# mov al,1 / mov cl,[bp-8] / shl al,cl / not al / and es:[bx+di],al
_CLEARS = bytes.fromhex("b001 8a4ef8 d2e0 f6d0 262001")
_PROLOGUE = b"\x55\x8b\xec"
# the action's segment, where the game keeps what it is performing, and the data's
_ACTION_WORDS = {0x00: "the object", 0x56: "the second object"}
_DATA_WORDS = {0x42: "the room", 0x44: "the actor"}


@dataclass(frozen=True)
class FlagUse:
    """One call about a flag."""

    flag: int
    kind: str  # tests, sets or clears
    overlay: int | None  # the overlay the call is in, or None for the root
    segment: int
    offset: int
    asked_of: str  # the object, as far as the call says: one by number, or where it's taken from
    routine: str  # whose routine the call is in


def flag_routines(program: Plink86Program) -> dict[int, str]:
    """Where the three routines start, in the root, each with what it does."""
    image = program.image()
    root = image.memory[: len(program.mz.image)]
    found: dict[int, str] = {}
    for kind, ending in (("tests", _TESTS), ("sets", _SETS), ("clears", _CLEARS)):
        at = root.find(ending)
        if at < 0 or root.find(ending, at + 1) >= 0:
            raise ValueError(f"the routine that {kind} a flag wasn't found")
        found[root.rindex(_PROLOGUE, 0, at)] = kind
    return found


class _Reader:
    """The calls in one stretch of code: the root's, or an overlay's."""

    def __init__(self, image: Image, start: int, end: int, overlay: int | None, labels: dict[int, str]):
        self.memory = image.memory
        self.image = image
        self.start, self.end, self.overlay = start, end, overlay
        self.labels = labels
        self.segments = sorted(image.segment_references())
        # a routine starts at a prologue, or where a switch's case calls: a routine that has
        # no arguments and no variables of its own has no prologue
        starts = {match.start() for match in re.finditer(re.escape(_PROLOGUE), self.memory[start:end])}
        self.starts = sorted({start + at for at in starts} | {at for at in labels if start <= at < end})

    def segment_of(self, linear: int) -> int:
        return max((segment for segment in self.segments if segment * 16 <= linear), default=0)

    def routine(self, linear: int) -> str:
        """Whose routine a place is in: an action's or an object's, or the routine's address."""
        index = bisect.bisect_right(self.starts, linear) - 1
        start = self.starts[index] if index >= 0 else self.start
        if start in self.labels:
            return self.labels[start]
        segment = self.segment_of(start)
        where = "" if self.overlay is None else f" in overlay {self.overlay}"
        return f"the routine at {segment:04x}:{start - segment * 16:04x}{where}"

    def _word(self, at: int) -> int:
        return int.from_bytes(self.memory[at : at + 2], "little")

    def _flag(self, push_end: int) -> int | None:
        """The flag, from mov ax,<flag> / push ax ending at a place."""
        if self.memory[push_end - 4] == 0xB8 and self.memory[push_end - 1] == 0x50:
            flag = self._word(push_end - 3)
            return flag if 1 <= flag <= 48 else None
        return None

    def arguments(self, call: int, names: dict[int, str]) -> tuple[int, str] | None:
        """The flag and the object a call is given: the flag was pushed first, then the object."""
        memory = self.memory

        def far(offset: int) -> str:
            return _ACTION_WORDS.get(offset) or _DATA_WORDS.get(offset) or "a word kept far"

        def numbered(number: int) -> str:
            return f"{number} {names.get(number, '?')}"

        # each way the object is pushed: its bytes' count, how they're known, and what they say
        pushes = (
            # mov es,<a segment's variable> / push es:[n]: a word the game keeps far
            (
                9,
                lambda c: c[:2] == b"\x8e\x06" and c[4:7] == b"\x26\xff\x36",
                lambda c: far(c[7] | c[8] << 8),
            ),
            # mov ax,<object> / push ax
            (4, lambda c: c[0] == 0xB8 and c[3] == 0x50, lambda c: numbered(c[1] | c[2] << 8)),
            # push [bp+n]
            (
                3,
                lambda c: c[:2] == b"\xff\x76",
                lambda c: "its argument" if c[2] < 0x80 else "a variable of its own",
            ),
            # push [n]
            (4, lambda c: c[:2] == b"\xff\x36", lambda c: _DATA_WORDS.get(c[2] | c[3] << 8, "a variable")),
            # push es:[n]
            (5, lambda c: c[:3] == b"\x26\xff\x36", lambda c: far(c[3] | c[4] << 8)),
            # push es:[a register]
            (3, lambda c: c[0] == 0x26 and c[1] == 0xFF, lambda c: "a word kept far"),
            # push a register
            (1, lambda c: 0x50 <= c[0] <= 0x57, lambda c: "a register"),
        )
        for length, fits, says in pushes:
            code = memory[call - length : call]
            flag = self._flag(call - length)
            if fits(code) and flag is not None:
                return flag, says(code)
        return None

    def calls(self, routines: dict[int, str], segment: int) -> list[tuple[int, str]]:
        """Each call to one of the routines here: where it is, and which routine it calls."""
        found = []
        for start, kind in routines.items():
            offset = start - segment * 16
            far = bytes([0x9A, offset & 255, offset >> 8, segment & 255, segment >> 8])
            at = self.memory.find(far, self.start, self.end)
            while at >= 0:
                if at + 3 in self.image.relocations:
                    found.append((at, kind))
                at = self.memory.find(far, at + 1, self.end)
        if self.overlay is None:
            # push cs / call near, from the routines' own segment
            at = self.memory.find(b"\x0e\xe8", segment * 16, self.end)
            while 0 <= at < min(self.end, segment * 16 + 0x10000):
                target = at + 4 + int.from_bytes(self.memory[at + 2 : at + 4], "little", signed=True)
                kind = routines.get(segment * 16 + ((target - segment * 16) & 0xFFFF))
                if kind and self.segment_of(at) == segment:
                    found.append((at, kind))
                at = self.memory.find(b"\x0e\xe8", at + 1, self.end)
        return sorted(found)


def _labels(
    program: Plink86Program, names: dict[int, str], shapes: dict[int, str]
) -> dict[int | None, dict[int, str]]:
    """Whose routine starts where, by overlay: the actions' and the objects' routines."""
    labels: dict[int | None, dict[int, str]] = {}

    def add(overlay: int | None, at: int, label: str, prefix: str) -> None:
        here = labels.setdefault(overlay, {})
        here[at] = f"{here[at]}, {label}" if at in here and here[at].startswith(prefix) else prefix + label

    actions, before = action_routines(program)
    for number, routine in sorted(actions.items()):
        said = f"{number} ({shapes[number]})" if number in shapes else str(number)
        add(routine.overlay, routine.segment * 16 + routine.offset, said, "action ")
    for number, routine in sorted(before.items()):
        add(routine.overlay, routine.segment * 16 + routine.offset, str(number), "before action ")
    shared: dict[tuple[int | None, int], list[int]] = {}
    for number, routine in sorted(object_routines(program).items()):
        shared.setdefault((routine.overlay, routine.segment * 16 + routine.offset), []).append(number)
    for (overlay, at), numbers in shared.items():
        said = ", ".join(f"{number} {names.get(number, '?')}" for number in numbers[:3])
        more = f" and {len(numbers) - 3} more" if len(numbers) > 3 else ""
        labels.setdefault(overlay, {}).setdefault(at, f"object {said}{more}")
    return labels


def flag_uses(
    program: Plink86Program, names: dict[int, str], shapes: dict[int, str]
) -> tuple[list[FlagUse], int]:
    """
    Every call about a flag, in the root and in each overlay, and how many more calls there
    are whose flag isn't a constant. Objects are named from `names`, and actions said with a
    command that makes them from `shapes`.
    """
    routines = flag_routines(program)
    root = program.image()
    segments = sorted(root.segment_references())
    segment = max(found for found in segments if found * 16 <= min(routines))
    labels = _labels(program, names, shapes)
    readers = [_Reader(root, 0, len(program.mz.image), None, labels.get(None, {}))]
    for overlay in program.overlays:
        if not overlay.preloaded:
            readers.append(
                _Reader(
                    program.image(overlay.number),
                    overlay.load_segment * 16,
                    overlay.end_segment * 16,
                    overlay.number,
                    labels.get(overlay.number, {}),
                )
            )
    uses = []
    missed = 0
    for reader in readers:
        for at, kind in reader.calls(routines, segment):
            given = reader.arguments(at, names)
            if given is None:
                missed += 1
                continue
            base = reader.segment_of(at)
            where = reader.routine(at)
            uses.append(FlagUse(given[0], kind, reader.overlay, base, at - base * 16, given[1], where))
    return uses, missed

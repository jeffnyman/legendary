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
from .program import Image, Program

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


# Spellcasting 201's, the same way. Its table of flags gives a place's and a person's flags
# numbers of their own, so one name does for each. One that isn't here isn't known
FLAG_NAMES_201 = {
    1: "its name takes no article",
    2: "its name takes `an`",
    3: "plural: `some`, and `they`",
    4: "takes `some`, and is still `it`",
    5: "seen by the player",
    6: "touched, or for a place visited",
    7: "taking it is special",
    8: "not listed among a room's contents",
    9: "gives light",
    10: "a door",
    11: "can't be opened or closed",
    12: "can be read",
    13: "open",
    14: "locked",
    15: "can be taken",
    16: "a place in the sewer",
    17: "a place at the university",
    18: "left out of `all`",
    19: "lit",
    20: "can be seen through",
    21: "things can be on it",
    22: "can be got in or on",
    23: "burns",
    24: "a container",
    25: "can be worn",
    26: "a key",
    27: "cuts",
    28: "cuts",
    29: "can be eaten",
    30: "what's dropped in it lands outside it",
    31: "magic: a spell, its box, or the spell book",
    32: "can be lit",
    33: "outdoors",
    34: "a part of the Sorcerer's Appliance",
    36: "a bed",
    37: "someone other than you",
    38: "female",
}
# Timequest's, the same way. A flag's number is its bit, as in Spellcasting 101, and the
# numbers are its own
FLAG_NAMES_TIMEQUEST = {
    1: "takes `some`, and is still `it`",
    2: "touched",
    3: "seen by the player",
    4: "left out of `all`",
    5: "its name takes no article",
    6: "its name takes `an`",
    7: "plural: `some`, and `they`",
    8: "not listed among a room's contents",
    11: "open",
    12: "can be taken, and hasn't been: taking it can be special",
    14: "can be opened and closed",
    15: "a container",
    16: "burns",
    17: "a door",
    18: "what's dropped in it lands outside it",
    19: "can be eaten",
    21: "a liquid",
    22: "cuts",
    23: "lit",
    24: "things can be on it",
    25: "cuts",
    26: "can be read",
    28: "a key",
    29: "can be turned on and off",
    30: "can be taken",
    32: "can be seen into",
    33: "an animal",
    34: "turned on",
    35: "can be got in or on",
    36: "locked",
    37: "can be worn",
    38: "worn",
}
PLACE_FLAG_NAMES_TIMEQUEST = {2: "visited", 8: "outdoors", 23: "has light"}
PERSON_FLAG_NAMES_TIMEQUEST = {
    10: "female",
    11: "someone other than you",
    12: "can't answer: asleep, or dead",
}
# Spellcasting 301's, the same way: its table gives every type's flags their own numbers
FLAG_NAMES_301 = {
    1: "its name takes no article",
    2: "its name takes `an`",
    3: "plural: `some`, and `they`",
    4: "takes `some`, and is still `it`",
    5: "seen by the player",
    6: "touched, or for a place visited",
    7: "taking it is special",
    8: "not listed among a room's contents",
    9: "gives light",
    10: "an animal",
    11: "can't be opened or closed",
    12: "a door",
    14: "can be read",
    15: "locked",
    16: "open",
    17: "things can be on it",
    18: "a container",
    19: "can be taken",
    20: "a place under water",
    23: "lit",
    24: "can be seen through",
    26: "can be got in or on",
    27: "burns",
    30: "can be worn",
    31: "a key",
    32: "cuts",
    33: "cuts",
    35: "can be eaten",
    37: "magic: a spell, its box, or the spell book",
    38: "can be lit",
    39: "a bed",
    41: "a tube of paint",
    45: "a seahorse",
    46: "outdoors",
    47: "someone other than you",
    48: "female",
}
# Gateway's, the same way: the things Timequest's flags are, numbered anew by the game's table
FLAG_NAMES_GATEWAY = {
    1: "takes `some`, and is still `it`",
    2: "touched, or for a place visited",
    4: "its name takes no article",
    5: "its name takes `an`",
    6: "plural: `some`, and `they`",
    7: "seen by the player",
    8: "not listed among a room's contents",
    10: "can be taken, and hasn't been: taking it can be special",
    12: "open",
    13: "a liquid",
    14: "can be opened and closed",
    15: "a door",
    16: "opened for you as you go through it",
    17: "what's dropped in it lands outside it",
    18: "can be eaten",
    19: "a container",
    21: "alive: an animal or a plant",
    22: "lit, or for a place has light",
    23: "can be worn",
    24: "cuts",
    26: "can be read",
    27: "cuts",
    28: "things can be on it",
    29: "can be taken",
    30: "a key",
    31: "can be seen through",
    33: "turned on",
    34: "can be got in or on",
    35: "locked",
    36: "can be turned on and off",
    37: "playing cards",
    38: "can be sat on",
    39: "outdoors",
    40: "female",
    41: "a person, and not you or a monster",
    42: "can't answer: asleep, or dead",
}
# Eric the Unready's: Gateway's flags under other numbers, from what has each as the game
# starts and what the actions that test it say. Flags 3, 9, 30 and 38 aren't known
FLAG_NAMES_ERIC = {
    1: "takes `some`, and is still `it`",
    2: "its name takes `an`",
    4: "its name takes no article",
    5: "touched, or for a place visited",
    6: "a mark its own routine keeps, of something that has happened",
    7: "plural: `some`, and `they`",
    8: "seen by the player",
    10: "can be taken, and hasn't been: taking it can be special",
    11: "not listed among a room's contents",
    12: "alive: a person or an animal",
    13: "lit, or for a place has light",
    14: "open",
    15: "can be opened and closed",
    16: "things can be on it",
    17: "what's dropped in it lands outside it",
    18: "female",
    19: "can be eaten or drunk",
    20: "a door",
    21: "a liquid",
    22: "a container",
    23: "burns",
    24: "cuts",
    25: "can't answer: asleep, or dead",
    26: "can be taken",
    27: "cuts",
    28: "can be read",
    29: "locked",
    31: "a key",
    32: "got out of before you go anywhere",
    33: "can be seen through",
    34: "outdoors",
    35: "turned on, or alight",
    36: "can be turned on and off, or lit",
    37: "can be got in or on",
    39: "can be worn",
    40: "can be spoken to",
}

GAME_101, GAME_TIMEQUEST, GAME_201 = "Spellcasting 101", "Timequest", "Spellcasting 201"
GAME_301, GAME_GATEWAY, GAME_ERIC = "Spellcasting 301", "Gateway", "Eric the Unready"
# each game's names: for any object, and what a place's and a person's are instead
_NAMES = {
    GAME_101: (FLAG_NAMES, PLACE_FLAG_NAMES, PERSON_FLAG_NAMES),
    GAME_TIMEQUEST: (FLAG_NAMES_TIMEQUEST, PLACE_FLAG_NAMES_TIMEQUEST, PERSON_FLAG_NAMES_TIMEQUEST),
    GAME_201: (FLAG_NAMES_201, {}, {}),
    GAME_301: (FLAG_NAMES_301, {}, {}),
    GAME_GATEWAY: (FLAG_NAMES_GATEWAY, {}, {}),
    GAME_ERIC: (FLAG_NAMES_ERIC, {}, {}),
}


def flag_name(flag: int, kind: int | None = None, game: str | None = GAME_101) -> str | None:
    """What a flag is, for an object of a type if one is given; None when it isn't known."""
    if game not in _NAMES:
        return None
    names, place, person = _NAMES[game]
    special = place if kind == 1 else person if kind == 7 else {}
    return special.get(flag) or names.get(flag)


def describe_flag(flag: int, game: str | None = GAME_101) -> str:
    """What a flag is, for every type at once."""
    if game not in _NAMES:
        return "not known"
    names, place, person = _NAMES[game]
    name = names.get(flag, "not known")
    for label, special in (("a place", place), ("a person", person)):
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
# Spellcasting 201's three have a routine find the flag's byte and bit first, from its table
# of flags, and end: les bx,[bp-4] / mov al,es:[bx] / sub ah,ah / mov cl,[bp-6] / shr ax,cl /
# and ax,1
_TESTS_LOOKED_UP = bytes.fromhex("c45efc 268a07 2ae4 8a4efa d3e8 250100")
# les bx,[bp-4] / mov al,1 / mov cl,[bp-6] / shl al,cl / or es:[bx],al
_SETS_LOOKED_UP = bytes.fromhex("c45efc b001 8a4efa d2e0 260807")
# the same / not al / and es:[bx],al
_CLEARS_LOOKED_UP = bytes.fromhex("c45efc b001 8a4efa d2e0 f6d0 262007")
# Gateway II's, from a later compiler, the same three with the bit taken first:
# les bx,[bp-6] / mov cl,[bp-2] / mov al,es:[bx] / shr al,cl / and ax,1
_TESTS_LATER = bytes.fromhex("c45efa 8a4efe 268a07 d2e8 250100")
# les bx,[bp-6] / mov cl,[bp-2] / mov al,1 / shl al,cl / or al,es:[bx] / mov es:[bx],al
_SETS_LATER = bytes.fromhex("c45efa 8a4efe b001 d2e0 260a07 268807")
# the same / not al / and al,es:[bx] / mov es:[bx],al
_CLEARS_LATER = bytes.fromhex("c45efa 8a4efe b001 d2e0 f6d0 262207 268807")
_PROLOGUE = b"\x55\x8b\xec"
# the words the game keeps that a call can take its object from. Spellcasting 101's: in the
# action's segment, where it keeps what it is performing, and in the data's
_ACTION_WORDS = {0x00: "the object", 0x56: "the second object"}
_DATA_WORDS = {0x42: "the room", 0x44: "the actor"}
# each way the routines end, with the words that go with it: Spellcasting 201's room is the
# one word of its own that is known
_ENDINGS = (
    ((_TESTS, _SETS, _CLEARS), _ACTION_WORDS, _DATA_WORDS),
    ((_TESTS_LOOKED_UP, _SETS_LOOKED_UP, _CLEARS_LOOKED_UP), {0x2A06: "the room"}, {}),
    ((_TESTS_LATER, _SETS_LATER, _CLEARS_LATER), {}, {}),
)


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


def _find_routines(program: Program) -> tuple[dict[int, str], dict[int, str], dict[int, str]]:
    """Where the three routines start, and the words a call to them can take its object from."""
    image = program.image()
    root = image.memory[: program.root_size]
    for endings, action_words, data_words in _ENDINGS:
        found: dict[int, str] = {}
        for kind, ending in zip(("tests", "sets", "clears"), endings):
            at = root.find(ending)
            if at >= 0 and root.find(ending, at + 1) < 0:
                found[root.rindex(_PROLOGUE, 0, at)] = kind
        if len(found) == len(endings):
            return found, action_words, data_words
    raise ValueError("the routines that test and change a flag weren't found")


def flag_routines(program: Program) -> dict[int, str]:
    """Where the three routines start, in the root, each with what it does."""
    return _find_routines(program)[0]


class CodeReader:
    """The calls in one stretch of code: the root's, or an overlay's."""

    def __init__(  # noqa: PLR0913
        self,
        image: Image,
        start: int,
        end: int,
        overlay: int | None,
        labels: dict[int, str],
        *,
        words: tuple[dict[int, str], dict[int, str]] = (_ACTION_WORDS, _DATA_WORDS),
    ):
        self.action_words, self.data_words = words
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
            return self.action_words.get(offset) or self.data_words.get(offset) or "a word kept far"

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
            # mov cx,<object> / push cx, as a later compiler does it after the flag's AX
            (4, lambda c: c[0] == 0xB9 and c[3] == 0x51, lambda c: numbered(c[1] | c[2] << 8)),
            # push [bp+n]
            (
                3,
                lambda c: c[:2] == b"\xff\x76",
                lambda c: "its argument" if c[2] < 0x80 else "a variable of its own",
            ),
            # push [n]
            (
                4,
                lambda c: c[:2] == b"\xff\x36",
                lambda c: self.data_words.get(c[2] | c[3] << 8, "a variable"),
            ),
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


def routine_labels(
    program: Program, names: dict[int, str], shapes: dict[int, str]
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


def flag_uses(program: Program, names: dict[int, str], shapes: dict[int, str]) -> tuple[list[FlagUse], int]:
    """
    Every call about a flag, in the root and in each overlay, and how many more calls there
    are whose flag isn't a constant. Objects are named from `names`, and actions said with a
    command that makes them from `shapes`.
    """
    routines, *words = _find_routines(program)
    root = program.image()
    segments = sorted(root.segment_references())
    segment = max(found for found in segments if found * 16 <= min(routines))
    labels = routine_labels(program, names, shapes)
    readers = [CodeReader(root, 0, program.root_size, None, labels.get(None, {}), words=(words[0], words[1]))]
    for overlay in program.overlays:
        if not overlay.preloaded:
            readers.append(
                CodeReader(
                    program.image(overlay.number),
                    overlay.load_segment * 16,
                    overlay.end_segment * 16,
                    overlay.number,
                    labels.get(overlay.number, {}),
                    words=(words[0], words[1]),
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

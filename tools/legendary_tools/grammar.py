"""The game's grammar: which verbs take which prepositions and objects, and the action each makes.

The program has a table of ten bytes for each form a command can take, in order of the verb's
number and then the preposition's, which it searches by halves:

    0  the verb, a word's number
    2  the preposition that follows the verb, or 0
    4  flags, kept with the sentence; they differ with where the objects may be found
    5  the action when the command has no object, or 0 when it must have one
    6  the action when it has one object
    7  where this form's entries start in the second table
    8  how many of them it has

The second table has six bytes for each way a form can go on to a second object: the
preposition before it, flags for it, a byte not yet understood, and the action.

Of a form's flags, and a second object's, two are known. 20 is that the object should be in
the actor's hands, and is taken first when it isn't, with "[Taking the cloak first.]"; 10 is
that it must be somewhere about the actor, and the command is refused when it isn't.

Before the forms, from the start of their segment, is a byte for each action. Its bit 1 is
that the action takes no turn: the game's clock doesn't move for it.

A word that is a synonym is looked up as the word it stands for. An action is a number; what
the game does about one is its own business, and isn't here.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass, field

from .data import Word
from .plink86 import Plink86Program

# lea ax,[bx+table] / mov dx,<segment> / push dx / push ax / push [bp+8] / push [bp+6] /
# push cs / call: a record of either table handed to the routine that compares it
_RECORD = re.compile(rb"\x8d\x87(..)\xba(..)\x52\x50\xff\x76\x08\xff\x76\x06\x0e\xe8", re.DOTALL)


@dataclass(frozen=True)
class Second:
    """A way a form goes on to a second object."""

    preposition: int
    flags: int
    unknown: int
    action: int


@dataclass(frozen=True)
class Form:
    """A form a command can take."""

    verb: int
    preposition: int
    flags: int
    action_alone: int
    action_with_object: int
    seconds: list[Second] = field(default_factory=list)


TAKEN_FIRST = 0x20  # the object is taken first when the actor doesn't have it in hand
MUST_BE_HELD = 0x10  # the object must be somewhere about the actor
NO_TURN = 0x01  # of an action's byte: the action takes no turn


def _tables(program: Plink86Program) -> tuple[bytes, int, int, int]:
    """The program's memory, the forms' segment, and where the two tables start in it."""
    memory = program.image().memory
    root = memory[: len(program.mz.image)]
    found = {
        (int.from_bytes(match[2], "little"), int.from_bytes(match[1], "little"))
        for match in _RECORD.finditer(root)
    }
    if len(found) < 2:
        raise ValueError("the code that searches the grammar wasn't found")
    # the forms come first, and the second table follows them
    (segment, first), (_, second) = sorted(found)[:2]
    return memory, segment, first, second


def action_flags(program: Plink86Program) -> bytes:
    """A byte for each action, the first for action 1: what's in the forms' segment before them."""
    memory, segment, first, _ = _tables(program)
    return memory[segment * 16 : segment * 16 + first]


def read_grammar(program: Plink86Program, words: list[Word]) -> list[Form]:
    """The forms, in the table's order."""
    memory, segment, first, second = _tables(program)
    forms_at = segment * 16 + first
    seconds_at = segment * 16 + second

    forms = []
    for at in range(forms_at, seconds_at, 10):
        verb, preposition, flags, alone, with_object, start, count = struct.unpack_from(
            "<HHBBBBB", memory, at
        )
        # every form starts with a verb's number
        if verb > len(words) or (verb and not words[verb - 1].parts & 1):
            raise ValueError(f"the grammar's form at {at - segment * 16:#x} doesn't start with a verb")
        seconds = [
            Second(*struct.unpack_from("<HBBB", memory, seconds_at + 6 * index))
            for index in range(start, start + count)
        ]
        forms.append(Form(verb, preposition, flags, alone, with_object, seconds))
    return forms

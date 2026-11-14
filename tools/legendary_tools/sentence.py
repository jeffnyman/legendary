"""The grammar of a sentence: the rules the parser matches a command's words against.

The parser is a general one. It has a grammar as data, in the form the game's writers would
have typed it, rule by rule, and a routine that matches the words of a command against it,
trying each way a rule can go and backing up when one fails. Even the rules' names are in the
program: `<noun_phrase>`, `<obj_list>`, `<sentence_terminator>`.

The data is three kinds of record, all in the segment DS has:

- a symbol, ten bytes: its kind, a far pointer to its name, and two words. Kind 0 is a rule;
  kind 2 a word of some part of speech, the first word being the part's bit; kind 1 a token
  of the lexer's, the second word being its code; kind 4 the end of the command.
- a way a rule can go, fourteen bytes: a far pointer to its name, which is the rule's with a
  number after it, one to its rule, one to a row of far pointers to its symbols, and their
  count. A count of 0 is a way that is nothing at all.
- a list of all the ways, six bytes each, a 3 and a far pointer, in the rules' order.

The ways are found here by their names, so no address is written down.
"""

from __future__ import annotations

import re
import struct
from dataclasses import dataclass

from .plink86 import Plink86Program

_NOTICE = b"MS Run-Time Library"
# a way's name is its rule's with a number before the closing bracket
_WAY_NAME = re.compile(rb"<([a-z_]+)(\d+)>\0")


@dataclass(frozen=True)
class Way:
    """A way a rule can go: the symbols it's made of, in order."""

    rule: str
    name: str
    symbols: list[str]


def read_sentence_grammar(program: Plink86Program) -> list[Way]:
    """Every way of every rule, in the order the program has them."""
    image = program.image()
    memory = image.memory
    notice = memory.find(_NOTICE)
    if notice < 0:
        raise ValueError("the program's data segment wasn't found")
    segment = (notice - 8) // 16
    base = segment * 16

    def text(offset: int) -> str:
        return memory[base + offset : memory.index(b"\0", base + offset)].decode("ascii")

    def pointer(at: int) -> int | None:
        """The offset a far pointer at a place has, when it is one into this segment."""
        offset, to = struct.unpack_from("<HH", memory, at)
        return offset if at + 2 in image.relocations and to == segment else None

    # where each way's name is, by its offset in the segment
    names = {match.start() - base for match in _WAY_NAME.finditer(memory, base, base + 0x10000)}
    ways = []
    for at in sorted(image.relocations):
        # a way starts with a pointer to its name; the relocation is on the pointer's segment
        start = at - 2
        name = pointer(start)
        if name not in names:
            continue
        rule, symbols = pointer(start + 4), pointer(start + 8)
        if rule is None:
            continue
        count = struct.unpack_from("<H", memory, start + 12)[0]
        rule_name = pointer(base + rule + 2)
        if rule_name is None or (count and symbols is None):
            continue
        made = []
        for index in range(count):
            symbol = pointer(base + (symbols or 0) + 4 * index)
            symbol_name = pointer(base + symbol + 2) if symbol is not None else None
            made.append(text(symbol_name) if symbol_name is not None else "?")
        ways.append(Way(text(rule_name), text(name), made))
    if not ways:
        raise ValueError("the program has no grammar of this kind")
    return ways

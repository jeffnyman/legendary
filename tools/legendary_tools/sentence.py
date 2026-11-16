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

Spellcasting 201 has the same records with the names taken out: a symbol is six bytes and a
way starts with its rule. Its ways are found by the list of them, and its symbols are named
by what they are. Its grammar is Spellcasting 101's, symbol for symbol, so its rules are given
the names they had there; a grammar that isn't the same would have its rules numbered.
"""

from __future__ import annotations

import re
import struct
import zlib
from dataclasses import dataclass

from .program import Image, Program

_NOTICE = b"MS Run-Time Library"
# a way's name is its rule's with a number before the closing bracket
_WAY_NAME = re.compile(rb"<([a-z_]+)(\d+)>\0")


# a list of the ways: 3 and a far pointer for each, at least this many, one after another
_LISTED = 8
# the symbols that aren't rules, by what they are: a token's code, and a part of speech's bit
TOKEN_NAMES = {
    -20: "<semi>",
    -19: ".",
    -18: "<comma>",
    -17: "<then>",
    -16: "<but>",
    -15: "<all>",
    -14: "<to>",
    -13: "<and>",
    -7: "<str>",
    -1: "<eol>",
}
PART_NAMES = {1: "<verb>", 2: "<noun>", 4: "<adj>", 8: "<prep>", 16: "<art>"}
# Spellcasting 101's symbols, in the order the program has them, and what its grammar comes
# to with each symbol put as its place in that order: a later game with the names taken out
# and the same grammar is given these
NAMES_101 = (
    "<adj_gotten>",
    "<adj_list>",
    "<adjective_phrase>",
    "<all_but_list>",
    "<all_item>",
    "<all_object>",
    "<direct_address_object>",
    "<inf_intro>",
    "<intro>",
    "<noun_phrase>",
    "<obj_conjunction>",
    "<obj_gotten>",
    "<obj_list>",
    "<obj_list_item>",
    "<obj_spec>",
    "<object>",
    "<pe_adjective_phrase>",
    "<pe_article>",
    "<pe_object_pair>",
    "<pe_obj_spec>",
    "<pe_object>",
    "<pe_prep>",
    "<sentence_gotten>",
    "<sentence_terminator>",
    "<standard_terminator>",
    "<verb>",
    "<prep>",
    "<adj>",
    "<noun>",
    "<art>",
    "<str>",
    "<and>",
    "<to>",
    "<all>",
    "<but>",
    "<then>",
    "<comma>",
    "<semi>",
    ".",
    "<eol>",
)
SHAPE_101 = 2626204826


@dataclass(frozen=True)
class Way:
    """A way a rule can go: the symbols it's made of, in order."""

    rule: str
    name: str
    symbols: list[str]


def read_sentence_grammar(program: Program) -> list[Way]:
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
    return ways or _unnamed(image, segment)


def _shape(ways: list[tuple[int, list[int]]]) -> tuple[list[int], int]:
    """The symbols a grammar has, in their order in memory, and a number for its shape."""
    order = sorted({symbol for rule, symbols in ways for symbol in [rule, *symbols]})
    place = {symbol: index for index, symbol in enumerate(order)}
    shape = repr([(place[rule], [place[symbol] for symbol in symbols]) for rule, symbols in ways])
    return order, zlib.crc32(shape.encode())


def _unnamed(image: Image, segment: int) -> list[Way]:
    """The ways of a grammar kept without its names, found by the list of them."""
    memory = image.memory
    base = segment * 16

    def listed(at: int) -> int:
        """How many entries of the list of ways start at a place."""
        count = 0
        while (
            memory[at + 6 * count : at + 6 * count + 2] == b"\x03\x00"
            and at + 6 * count + 4 in image.relocations
        ):
            count += 1
        return count

    start = next(
        (at for at in range(base, min(base + 0x10000, len(memory) - 6)) if listed(at) >= _LISTED), None
    )
    if start is None:
        raise ValueError("the program has no grammar of this kind")
    read: list[tuple[int, list[int]]] = []
    for index in range(listed(start)):
        way = base + struct.unpack_from("<H", memory, start + 6 * index + 2)[0]
        rule, _, symbols, _, count = struct.unpack_from("<5H", memory, way)
        read.append(
            (rule, [struct.unpack_from("<H", memory, base + symbols + 4 * each)[0] for each in range(count)])
        )

    order, shape = _shape(read)
    rules = sorted({rule for rule, _ in read})

    def name(symbol: int) -> str:
        if shape == SHAPE_101 and len(order) == len(NAMES_101):
            return NAMES_101[order.index(symbol)]
        kind, first, second = struct.unpack_from("<HHh", memory, base + symbol)
        if kind == 0:
            return f"<rule_{rules.index(symbol) + 1}>" if symbol in rules else "?"
        return (PART_NAMES.get(first) if kind == 2 else TOKEN_NAMES.get(second)) or "?"

    counts: dict[int, int] = {}
    ways = []
    for rule, symbols in read:
        counts[rule] = counts.get(rule, 0) + 1
        ways.append(
            Way(name(rule), f"{name(rule)[:-1]}{counts[rule]}>", [name(symbol) for symbol in symbols])
        )
    return ways

# Tools

Scripts used to take the games apart. They read a game's files and print what they find; none
of them is needed to build Legendary. They need Python 3.9 or later, and, for `dis` alone,
[Capstone](https://www.capstone-engine.org/).

They run from this folder as they are:

```
python -m legendary_tools <command> <the game's folder> ...
```

or, installed, from anywhere, as `legendary-tools`:

```
pip install -e ".[disasm]"
legendary-tools <command> <the game's folder> ...
```

The game's folder is the one with its `...MAIN.EXE` in it. Addresses and segments are in hex,
relative to where the program loads, as the files have them. How the programs are built is in
[docs/spellcasting-101.md](../docs/spellcasting-101.md#how-the-program-is-built).

## The program

These read a program linked by PLINK86: *Spellcasting 101* and *Timequest*.

```
python -m legendary_tools exe <the game's folder>
python -m legendary_tools overlays <the game's folder>
python -m legendary_tools thunks <the game's folder> [--overlay N]
python -m legendary_tools segments <the game's folder> [--overlay N]...
python -m legendary_tools refs <the game's folder> <segment>
python -m legendary_tools dump <the game's folder> <address> [<bytes>] [--overlay N]...
python -m legendary_tools dis <the game's folder> <address> [<instructions>] [--overlay N]...
python -m legendary_tools loads <the game's folder>
python -m legendary_tools objects <the game's folder> [<number>] [--tree] [--saved]
```

- **`exe`** prints each program's MZ header: its image, what's in the file after it, the memory
  it asks for, and where it starts.
- **`overlays`** prints PLINK86's table: each overlay under the place it loads at, with its
  size, its relocations, and where it is in which file.
- **`thunks`** lists the ways into the overlays: each thunk's address, its overlay, and the
  routine it goes to.
- **`segments`** lists every segment the relocations name, with how often and what it's in.
  The often-named ones are the data, the library and the system's own routines.
- **`refs`** lists the places that name a segment, in the root and in every overlay, each with
  the word before it, which is the offset when the place is a far pointer's. It finds what
  points into a segment: `refs <folder> 2701` finds the table of exits that uses the
  fireplace's message.
- **`dump`** prints memory in hex and as text.
- **`dis`** prints instructions. A far call to a thunk is marked with the overlay and routine
  it goes to, and an instruction with a segment in it is marked as having one. An instruction
  that takes a string's pointer from an array has the string written beside it, as
  `string 2.25: If you want to climb the chimney...`, when the code it's in loads one block
  into that array; when it loads several, the blocks it could be are listed.
- **`loads`** lists every call to the routine that loads a string block: where the call is,
  the block, its count of strings, and the array of pointers it's loaded into. That is how the
  program names a string, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#how-the-program-names-a-string). It
  ends with the blocks no call names by number. It reads *Spellcasting 101* in full;
  *Timequest* takes most of its block numbers from variables, which aren't followed.

`segments`, `dump` and `dis` build the program's memory first: the root, the data, and the
overlays `--overlay` names, which can't be two that load at the same place. Given a routine's
address from `thunks` and no overlay, `dis` loads the overlay that routine is in:

```
python -m legendary_tools dis ../../legend/sc101/game 3670:054e
```

- **`objects`** lists the game's objects as it starts, from the program's memory: each
  one's number, type, flags, names, and what it's in. It starts with the types. With a number,
  it prints that object in full: its flags, where it is, what's on each of its lists, a room's
  exits with where each goes, and its record in words, with the far pointers in brackets. With
  `--tree`, it prints each room and holder with what's in it, and in that. With `--saved`, it
  prints the parts of `RESTART.DAT` and their sizes, which come to the file's. How the objects
  are kept is in [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-objects). It reads
  *Spellcasting 101* only: the code that finds the tables isn't *Timequest*'s.

## The data files

```
python -m legendary_tools strings <the game's folder> [--words]
python -m legendary_tools text <the game's folder> [<block>]
python -m legendary_tools vocab <the game's folder>
```

- **`strings`** prints the table that starts `...STR.DAT`: each block's size unpacked, its size
  packed, its count of strings, and where it is in the file. With `--words`, it lists the 128
  words the text is packed with, each with its code.
- **`text`** unpacks the text: every block's strings, or one block's, a string to a line, each
  after its block and its number in it, as `2.0`. Line ends and tabs in a string are written
  `\n` and `\t`. It reads *Spellcasting 101* and *Timequest*; the later games pack their text
  another way. How the packing works is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-text).
- **`vocab`** lists the words that start `VOCAB.DAT`, numbered in the file's order.

## The modules

- **`mz.py`**: a DOS program's MZ header, image and relocations.
- **`plink86.py`**: PLINK86's table of overlays, the thunks, and the program's memory with
  overlays loaded. `src/Legendary.Core/Programs` is the same in C#.
- **`objects.py`**: the table of objects, the types, each record's tree fields and a room's
  exits, and the parts of a saved state.
- **`loads.py`**: finding the routine that loads a string block and the calls to it, and from
  them the block behind an array.
- **`data.py`**: the data files, as far as they're understood: the string file, unpacked, and
  the vocabulary's words. `src/Legendary.Core/Text` has the string file in C#.
- **`game.py`**: finding a game's files, whatever case their names are in.
- **`cli.py`**: the commands.

## Checking the scripts

[Ruff](https://docs.astral.sh/ruff/) lints and formats them and [ty](https://docs.astral.sh/ty/)
checks their types, both set up in `pyproject.toml`. With [uv](https://docs.astral.sh/uv/),
from this folder:

```
uv run ruff check .
uv run ruff format --check .
uv run ty check .
```

`uv run` makes a `.venv` here with the `dev` group in it the first time. Without uv, install
`ruff` and `ty` and run the same three commands without `uv run`.

An editor with the whole repository open runs ty from the folder above this one, where
`ty.toml` points it at this folder and at the `.venv` here. Until that's made (`uv sync`, or
any `uv run`, in this folder), ty there has no Capstone to find, and says so.

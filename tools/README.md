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

These read a program linked by PLINK86, *Spellcasting 101* and *Timequest*, or by
.RTLink/Plus 4.10, *Spellcasting 201*. The commands from `loads` down read only the first
two so far, but for `objects`, which reads all three.

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
python -m legendary_tools grammar <the game's folder> [<verb>]
python -m legendary_tools actions <the game's folder> [<number>]
python -m legendary_tools flags <the game's folder> [<flag>]
python -m legendary_tools timers <the game's folder>
python -m legendary_tools sentence <the game's folder>
```

- **`exe`** prints each program's MZ header: its image, what's in the file after it, the memory
  it asks for, and where it starts.
- **`overlays`** prints the linker and its loader's table: each overlay under the place it
  loads at, with its size, its relocations, and where it is in which file. For a .RTLink
  program it also counts the records the program and its run-time library are kept as.
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
  it prints that object in full: its flags, the nouns and adjectives it's called by, its routine,
  where it is, what's on each of its lists, what's also in a room without being in its tree, a
  room's exits with the direction and where each goes, and its record in words, with the far pointers in brackets. With
  `--tree`, it prints each room and holder with what's in it, and in that. With `--saved`, it
  prints the parts of `RESTART.DAT` and their sizes, which come to the file's. How the objects
  are kept is in [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-objects). It reads
  *Spellcasting 101* and, with its names from `OBJECT.DAT` and its other differences,
  [*Spellcasting 201*](../docs/spellcasting-201.md#the-objects); the flags' names are the first's
  alone. For *Timequest* it finds the table and lists each object's number, type and names,
  and no more: its records are laid out another way.

- **`grammar`** lists the forms a command can take, from the program's tables: for each verb
  and the preposition after it, the action made with no object, with one, and with a second
  after each preposition it allows, as `drop OBJECT in OBJECT: action 51`. Each line ends
  with where the object, and the second, may be looked for and what's asked of it, from the
  form's flags. With a verb, it lists that verb's forms. It needs `VOCAB.DAT` for the words, and
  reads *Spellcasting 101* and *Timequest*. How the table is kept is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-grammar).

- **`actions`** lists each action with its routine, whether it takes no turn, the routine that comes before it when
  there is one, and the commands that make it, from the grammar: the first four, or all of
  them with an action's number. An object's routine is in what `objects` prints for it. Both
  are read from the code's own switches, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#from-an-action-to-what-happens), and
  `dis` at a routine's address shows it. It reads *Spellcasting 101* only.

- **`flags`** lists the objects' flags: for each, what it is, how many objects of which types
  have it as the game starts, and how often the program tests, sets and clears it. With a
  flag's number, it lists every object that has it and every call about it: where the call
  is, whose routine it's in, and what object it asks about, as `313a:2293 in overlay 1: action
  128 (open OBJECT) tests it of the object`. That is how a flag's meaning is found, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#what-the-flags-are). `objects` with
  a number says each of the object's flags by name. It reads *Spellcasting 101* only.

- **`timers`** lists the game's timers, the routines it puts by to run on a later turn or on
  every turn: each one's routine, the routine in an overlay that it calls, and every call that
  starts it, with its count, stops it or asks whether it is going, with whose routine the call
  is in. How the timers work is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-timers). It reads *Spellcasting
  101* only.

- **`sentence`** prints the grammar the parser matches a command against, rule by rule, with
  the names the program has for the rules: `<noun_phrase> ::= <pe_article>
  <pe_adjective_phrase> <noun>`. `grammar` is which verbs go with which prepositions; this is
  what a command is at all. It is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-parser), and reads *Spellcasting
  101* only.

## The data files

```
python -m legendary_tools music <the game's folder> [--out <folder>]
python -m legendary_tools pictures <the game's folder> [--out <folder>]
python -m legendary_tools regions <the game's folder>
python -m legendary_tools sounds <the game's folder> [--out <folder>]
python -m legendary_tools strings <the game's folder> [--words]
python -m legendary_tools text <the game's folder> [<block>]
python -m legendary_tools vocab <the game's folder>
```

- **`music`** lists the songs in each `.MUS` file: each one's size and which of its two
  formats it's in, with a MIDI file's track names. With `--out`, it writes each to the folder
  as it is, a `.mid` or a `.rol` file, which other programs play.
- **`pictures`** lists the pictures in each `.PIC` file: each one's number, size, the way it's
  packed, whether it has a palette, and how many places it's drawn at. With `--out`, it
  writes every picture to the folder as a PNG file, named for its file and number, as
  `s101_e1_038.png`: the EGA's in their own colors, the CGA's in black and white. How they're
  packed is in [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-pictures).
- **`regions`** lists the regions in each `.RGN` file: for each picture that has them, the
  object the rest of the picture is, and each rectangle, in the EGA's pixels, with the object
  that's there. The objects are named when the program's table of them can be read, which is
  for *Spellcasting 101*. It is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#what-is-where-in-a-picture).
- **`sounds`** lists the `.RS` files: each sound's length, how many times each of its bytes
  is played, and so how long it lasts. With `--out`, it writes each to the folder as a WAV
  file. Both are in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-music-and-the-sounds), and both
  read *Timequest* too.
- **`strings`** prints the table that starts `...STR.DAT`: each block's size unpacked, its size
  packed, its count of strings, and where it is in the file. With `--words`, it lists the 128
  words the text is packed with, each with its code.
- **`text`** unpacks the text: every block's strings, or one block's, a string to a line, each
  after its block and its number in it, as `2.0`. Line ends and tabs in a string are written
  `\n` and `\t`. It reads *Spellcasting 101* and *Timequest*; the later games pack their text
  another way. How the packing works is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-text).
- **`vocab`** lists the vocabulary in `VOCAB.DAT`: each word's number, its parts of speech,
  and, for a synonym, the word it stands for and as what. It reads *Spellcasting 101*'s and
  *Timequest*'s plain file and *Spellcasting 201*'s packed one, which is in
  [docs/spellcasting-201.md](../docs/spellcasting-201.md#the-vocabulary).

## The modules

- **`mz.py`**: a DOS program's MZ header, image and relocations.
- **`program.py`**: what the two linkers' programs share: an overlay, a thunk, and the
  program's memory with overlays loaded.
- **`plink86.py`**: PLINK86's table of overlays and its thunks.
  `src/Legendary.Core/Programs` is the same in C#.
- **`rtlink.py`**: a .RTLink/Plus program: its records and their fixups, its run-time
  library linked in, and the overlay manager's table and thunks. How it works is in
  [docs/spellcasting-201.md](../docs/spellcasting-201.md#how-the-program-is-built).
- **`handlers.py`**: the routine of each object and each action, read from the switches in the
  code that call them.
- **`sentence.py`**: the rules of the parser's grammar and the ways each can go, found by the
  names the program keeps for them.
- **`grammar.py`**: the table of the forms a command can take, and the second table of the
  ways each goes on to a second object.
- **`objects.py`**: the table of objects, the types, each record's tree fields and a room's
  exits, and the parts of a saved state.
- **`loads.py`**: finding the routine that loads a string block and the calls to it, and from
  them the block behind an array.
- **`pictures.py`**: the directory of a picture file, the four ways its pixels are packed,
  and a picture as a PNG file. `src/Legendary.Core/Pictures` is the same in C#, without the
  PNG.
- **`sound.py`**: the songs of a music file, and a RealSound file as a WAV file.
  `src/Legendary.Core/Sound` has both in C#, without the WAV.
- **`regions.py`**: the regions of a region file. `src/Legendary.Core/Pictures` has it in C#.
- **`data.py`**: the data files, as far as they're understood: the string file, unpacked, and
  the vocabulary. `src/Legendary.Core/Text` has the string file in C#.
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

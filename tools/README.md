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
.RTLink/Plus 4.10, *Spellcasting 201*, 5.02, *Spellcasting 301*, *Gateway* and *Eric the
Unready*, or 6.10, [*Gateway II*](../docs/gateway-ii.md#the-linker-is-a-newer-one); or one the linker left
whole, [*Companions of Xanth*](../docs/companions-of-xanth.md#how-the-program-is-built). All of them read
*Spellcasting 201*; what each makes of *Timequest* is said with it. Of *Spellcasting 301*,
the first seven read the program, and `objects`, with `--saved`, `actions`, `flags`, `timers`, `grammar`, `sentence`,
`loads`, `strings`, `text` and `vocab` read it too; the
rest [stop at something kept a new way](../docs/spellcasting-301.md#still-to-do).

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
  it asks for, and where it starts. For a 32-bit program bound to DOS/4GW, as
  [*Mission Critical*](../docs/mission-critical.md#how-the-program-is-built) is, it goes on to
  the linear executable in the file: its objects, where each expects to load, where it starts,
  and how many addresses it holds.
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
  For a 32-bit program it takes an address, and lists the places that hold it, in the code or in
  the data: `refs <folder> c0594` finds the one routine that names `RESTART.DAT`.
- **`dump`** prints memory in hex and as text. A 32-bit program has no segments, so its
  address is one number, as `c0594`.
- **`dis`** prints instructions. A far call to a thunk is marked with the overlay and routine
  it goes to, and an instruction with a segment in it is marked as having one. An instruction
  that takes a string's pointer from an array has the string written beside it, as
  `string 2.25: If you want to climb the chimney...`, when the code it's in loads one block
  into that array; when it loads several, the blocks it could be are listed. A `mov ax` that
  is the offset of a far pointer to text has the text beside it, as
  `"The statue is two or three times larger than lifesize."`, and so has one that is a
  string's number in *Spellcasting 201*.
- **`loads`** lists every call to the routine that loads a string block: where the call is,
  the block, its count of strings, and the array of pointers it's loaded into. That is how the
  program names a string, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#how-the-program-names-a-string). It
  ends with the blocks no call names by number. It reads *Spellcasting 101* in full;
  *Timequest* takes most of its block numbers from variables, which aren't followed. For
  *Spellcasting 201*, which loads no arrays and
  [names a string by one number](../docs/spellcasting-201.md#how-the-program-names-a-string),
  it lists every call that does, with the string. For *Spellcasting 301*, which names a
  string [as a far pointer no text is at](../docs/spellcasting-301.md#how-the-program-names-a-string),
  it lists every place that does, in code or in data, and says how many of the file's
  strings are named.

For a 32-bit program `dis` reads 32-bit instructions, and writes beside one the text that an
address in it points to. `overlays`, `thunks` and `segments` have nothing to say of one.

`segments`, `dump` and `dis` build the program's memory first: the root, the data, and the
overlays `--overlay` names, which can't be two that load at the same place. Given a routine's
address from `thunks` and no overlay, `dis` loads the overlay that routine is in:

```
python -m legendary_tools dis ../../legend/sc101/game 3670:054e
```

- **`objects`** reads [*Mission Critical*'s](../docs/mission-critical.md#the-objects) too, from
  its 32-bit program: the list, one object in full with a room's views and exits, `--tree`,
  and with `--saved` the variables the game saves, each with its name where one is known.
- **`objects`** lists the game's objects as it starts, from the program's memory: each
  one's number, type, flags, names, and what it's in. It starts with the types. With a number,
  it prints that object in full: its flags, the nouns and adjectives it's called by, its routine,
  where it is, what's on each of its lists, what's also in a room without being in its tree, a
  room's exits with the direction and where each goes, and its record in words, with the far pointers in brackets. With
  `--tree`, it prints each room and holder with what's in it, and in that. With `--saved`, it
  prints the parts of `RESTART.DAT` and their sizes, which come to the file's. How the objects
  are kept is in [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-objects). It reads
  *Spellcasting 101* and, with its names from `OBJECT.DAT` and its other differences,
  [*Spellcasting 201*](../docs/spellcasting-201.md#the-objects); each game's flags have their own names. It reads
  [*Spellcasting 301*'s](../docs/spellcasting-301.md#the-objects), whose lists end another
  way and whose records name their routines by number, and says of an object which routine
  [gives its text and which says whether a phrase means it](../docs/spellcasting-301.md#two-more-kinds-of-routine).
  It reads [*Gateway*'s](../docs/gateway.md#the-objects), which are kept the same way, and
  [*Eric the Unready*'s](../docs/eric-the-unready.md#the-objects). It reads [*Timequest*](../docs/timequest.md#the-objects) too, whose records are
  between the two, with a [kind of exit](../docs/timequest.md#the-exits) of its own.

- **`grammar`** lists the forms a command can take, from the program's tables: for each verb
  and the preposition after it, the action made with no object, with one, and with a second
  after each preposition it allows, as `drop OBJECT in OBJECT: action 51`. Each line ends
  with where the object, and the second, may be looked for and what's asked of it, from the
  form's flags. With a verb, it lists that verb's forms. It needs `VOCAB.DAT` for the words, and
  reads *Spellcasting 101*, *Timequest* and *Spellcasting 201*. How the table is kept is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-grammar), and what the third
  changes in [docs/spellcasting-201.md](../docs/spellcasting-201.md#the-grammar).

- **`actions`** lists each action with its routine, whether it takes no turn, the routine that comes before it when
  there is one, and the commands that make it, from the grammar: the first four, or all of
  them with an action's number. An object's routine is in what `objects` prints for it. Both
  are read from the code's own switches, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#from-an-action-to-what-happens), and
  `dis` at a routine's address shows it. It reads all three games.

- **`flags`** lists the objects' flags: for each, what it is, how many objects of which types
  have it as the game starts, and how often the program tests, sets and clears it. With a
  flag's number, it lists every object that has it and every call about it: where the call
  is, whose routine it's in, and what object it asks about, as `313a:2293 in overlay 1: action
  128 (open OBJECT) tests it of the object`. That is how a flag's meaning is found, which is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#what-the-flags-are). `objects` with
  a number says each of the object's flags by name. It names *Spellcasting 101*'s flags,
  [*Timequest*'s](../docs/timequest.md#the-flags),
  [*Spellcasting 201*'s](../docs/spellcasting-201.md#what-the-flags-are),
  [*Spellcasting 301*'s](../docs/spellcasting-301.md#what-the-flags-are),
  [*Gateway*'s](../docs/gateway.md#the-flags) and
  [*Eric the Unready*'s](../docs/eric-the-unready.md#the-flags),
  [*Gateway II*'s](../docs/gateway-ii.md#what-the-flags-are) and
  [*Companions of Xanth*'s](../docs/companions-of-xanth.md#the-flags), and for the last
  six gives each flag's bit and the types that have it too, from the game's table of flags.

- **`timers`** lists the game's timers, the routines it puts by to run on a later turn or on
  every turn: each one's routine, the routine in an overlay that it calls, and every call that
  starts it, with its count, stops it or asks whether it is going, with whose routine the call
  is in. How the timers work is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-timers). It reads *Spellcasting
  101* and *Spellcasting 201*, whose timers are
  [numbers](../docs/spellcasting-201.md#the-timers), and names each one's by
  [what its routine does](../docs/spellcasting-201.md#what-each-timer-does). It reads
  [*Timequest*'s](../docs/timequest.md#the-timers) too, 124 numbered timers, with
  [a name for each](../docs/timequest.md#what-each-timer-does), and
  [*Spellcasting 301*'s](../docs/spellcasting-301.md#what-each-timer-does) 65,
  [*Gateway*'s](../docs/gateway.md#what-each-timer-does) 108 and
  [*Eric the Unready*'s](../docs/eric-the-unready.md#what-each-timer-does) 72,
  [*Gateway II*'s](../docs/gateway-ii.md#what-each-timer-does) 58 and
  [*Companions of Xanth*'s](../docs/companions-of-xanth.md#the-timers) 45, of which 27 have
  a name.

- **`sentence`** prints the grammar the parser matches a command against, rule by rule, with
  the names the program has for the rules: `<noun_phrase> ::= <pe_article>
  <pe_adjective_phrase> <noun>`. `grammar` is which verbs go with which prepositions; this is
  what a command is at all. It is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-parser). It reads all three
  games; *Spellcasting 201* keeps [the same grammar without the
  names](../docs/spellcasting-201.md#the-parsers-own-grammar), which are given back to it.

## The data files

```
python -m legendary_tools extract <the game's folder, or a folder of games> [--out <folder>]
python -m legendary_tools music <the game's folder> [--out <folder>]
python -m legendary_tools pictures <the game's folder> [--out <folder>]
python -m legendary_tools regions <the game's folder>
python -m legendary_tools sounds <the game's folder> [--out <folder>]
python -m legendary_tools strings <the game's folder> [--words]
python -m legendary_tools text <the game's folder> [<block>]
python -m legendary_tools vocab <the game's folder>
```

- **`extract`** writes a game's pictures, sounds, music and fonts out to be looked at and
  listened to, in a folder for each: `media/sc101/pictures`, `sounds`, `music` and, for a
  game that has them, `fonts` and `animations`. It is `pictures`, `animations`, `sounds`,
  `music` and `fonts` with `--out`, all at once. Given a folder of games, as `../legend`, it
  does each game in it, and says for each how much it wrote. A game's files may be in its
  folder, in a `game` inside it, or a folder deeper, as *Eric the Unready*'s are. The `media` folder is beside
  `tools`, and the repository ignores it: what's in it is the games' own, and no more the
  repository's to hold than the games are. `--out` puts it somewhere else. It says how many
  pictures it couldn't read, of any that are kept a way that isn't known. Beside those
  folders it writes `index.html`, one page to look through them all in a browser: every
  picture, a file of the game's at a time, each under its number and size; every sound, to
  be played there; the songs, as links, with the AdLib's to be played there too; and each font, as a line set in it and all its
  characters.
  The sound of an AdLib song is the FM chip's, which the player has and these scripts
  don't. So `extract` has the player make it: it runs `legendary --songs` for each game,
  which plays each song through Ad Lib's driver as the game does and records the chip, a WAV
  file beside each `.rol` or `.mdi`. That needs `dotnet`, and takes some minutes a game;
  `--no-wav` leaves it out, and without `dotnet` it is left out and said so.
- **`disc`** is for a game that came on a CD and reads some of its files from the disc, as
  [*Gateway II*](../docs/gateway-ii.md#the-pictures) reads its pictures. With an image of
  the CD beside the game, in its folder or in a `cd` inside it, plain ISO 9660 or raw, it lists what the game's
  folder on the disc has that the game's own folder hasn't; `LEGEND.INI` says which folder
  that is. With `--copy` it copies those files into the game's folder, which is then whole:
  nothing else here reads the image, and a game's folder can be kept without it.
  [*Mission Critical*](../docs/mission-critical.md#the-three-cds) came on three CDs, and with
  the three images there it does that for each, folders inside the game's folder too, and a
  file that's on more than one CD once.
- **`animations`** lists the animations among a game's `.PIC` files, which *Gateway II*
  has: Autodesk Animator's `.FLI` files, each with its frames, size and length. With
  `--out`, it writes each to the folder as an animated PNG file, which a browser plays.
- **`flags`** reads [*Mission Critical*'s](../docs/mission-critical.md#the-flags) too: each
  flag's name where it's known, its bit and types, what starts with it, and how often the
  program tests, sets and clears it.
- **`ships`** lists the ships of [*Mission Critical*'s battle game](../docs/mission-critical.md#the-battle-game),
  each a drawing in lines: its points and lines, in full and far off, and its size. With
  `--out`, it draws each as a PNG file, from above, from the side and from ahead.
- **`stars`** lists the 166 stars of that game's map: name, place in the sky, distance and
  kind, and under each the stars it is joined to.
- **`coolant`** lists the paths of that game's `CSP.DAT`, which read as
  [its reactor's coolant](../docs/mission-critical.md#the-coolants-paths): each one's steps.
- **`scenes`** lists [*Mission Critical*'s filmed scenes](../docs/mission-critical.md#the-filmed-scenes),
  its `.Q` files: each one's version, frames, length, and whether it has sound. With a name,
  as `mc700`, only that one. With `--out`, it writes each as an animated PNG file, which a
  browser plays, and its sound as a WAV file; the ten minutes of the opening take a few
  minutes and about a hundred megabytes, so a name is worth giving.
- **`verbs`** lists the verbs of a game played with the mouse,
  [*Companions of Xanth*](../docs/companions-of-xanth.md#the-verbs): each one's number,
  name and how many objects it takes, with how many objects have it as the verb a click
  does and how many list it, and then the pairs of objects that verbs of two take. For
  [*Mission Critical*](../docs/mission-critical.md#the-verbs) it gives each verb's action and
  the action's routine.
- **`fonts`** lists the fonts in each `.FNT` file, which the games from *Spellcasting 301*
  have: how high each is, how many characters, and how wide. With `--out`, it writes each to
  the folder as two PNG files, all its characters and a line set in it. The format is in
  [the notes](../docs/spellcasting-301.md#the-fonts), and what
  [*Mission Critical*'s add to it](../docs/mission-critical.md#the-fonts): fonts with one
  table, and one in colors.
- **`music`** lists the songs in each `.MUS` file: each one's size and which of its two
  formats it's in, with a MIDI file's track names. With `--out`, it writes each to the folder
  as it is, a `.mid` file for the MT-32 and, for the AdLib, a `.rol` file in *Spellcasting
  101* and Ad Lib's kind of MIDI file, `.mdi`, [in the games
  after](../docs/spellcasting-201.md#the-music). It reads every game's.
  [*Mission Critical*'s](../docs/mission-critical.md#the-music) is in `.XMI` files, the Miles
  drivers' extended MIDI, and each song is written as a Standard MIDI File.
- **`pictures`** lists the pictures in each `.PIC` file: each one's number, size, the way it's
  packed, whether it has a palette, and how many places it's drawn at. With `--out`, it
  writes every picture to the folder as a PNG file, named for its file and number, as
  `s101_e1_038.png`: the EGA's in their own colors, the CGA's in black and white. How they're
  packed is in [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-pictures). It
  writes *Spellcasting 101*'s, *Timequest*'s and *Spellcasting 201*'s, the last two of which
  [say how a picture is packed another way](../docs/timequest.md#the-pictures), and
  *Spellcasting 301*'s and *Gateway*'s, which are
  [imploded](../docs/spellcasting-301.md#the-pictures) and come in 256 colors too. A picture
  of a kind that isn't known is left out, and counted.
  [*Mission Critical*'s](../docs/mission-critical.md#the-pictures) are packed as LHA packs a
  file, and most are changes to another picture: one is written as what it makes of the
  picture before it in its file, where there is one it fits in, and as it is where there isn't.
  Its 2,949 pictures take a minute or so.
- **`regions`** lists the regions in each `.RGN` file: for each picture that has them, the
  object the rest of the picture is, and each rectangle, in the EGA's pixels, with the object
  that's there. *Gateway II* and *Companions of Xanth* keep a region as a list of points, and
  for those it gives a rectangle's corners in pixels, or says a region is an outline, with how
  many corners it has and the box they are in. The objects are named when the program's table of them can be read, which is
  for *Spellcasting 101*. It is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#what-is-where-in-a-picture).
- **`sounds`** lists the `.RS` files: each sound's length, how many times each of its bytes
  is played, and so how long it lasts. With `--out`, it writes each to the folder as a WAV
  file. Where a game has a `.VOC` file of speech, as *Companions of Xanth* has, it counts its
  lines and writes each as a WAV file too, in a folder of the file's name.
  [*Mission Critical*](../docs/mission-critical.md#the-speech) has three, and each line of
  theirs is a WAV file already, which is written as it is. Both are in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-music-and-the-sounds), and both
  read *Timequest* too.
- **`speech`** lists the lines of a game's speech, from its `.VOC` file, each with how long it
  is and the text it speaks, where the program says which: for *Companions of Xanth*, 985 of
  its 1,222, and for [*Mission Critical*](../docs/mission-critical.md#what-a-line-says), 748
  of its 1,077, in three files, with 94 more said to be a sound, a line spoken by its number,
  or a part of the ship's briefing. It is in
  [docs/companions-of-xanth.md](../docs/companions-of-xanth.md#what-a-line-says).
- **`tour`** lists the guided tours in the `.HLP` files of a game played with the mouse, which
  are recordings of the game being played: the words each shows, and where it clicks. It is
  in [docs/companions-of-xanth.md](../docs/companions-of-xanth.md#the-guided-tours).
- **`strings`** prints the table that starts `...STR.DAT`: each block's size unpacked, its size
  packed, its count of strings, and where it is in the file. With `--words`, it lists the 128
  words the text is packed with, each with its code.
- **`text`** unpacks the text: every block's strings, or one block's, a string to a line, each
  after its block and its number in it, as `2.0`. Line ends and tabs in a string are written
  `\n` and `\t`. It reads *Spellcasting 101*, *Timequest* and *Spellcasting 201*, whose
  packing [differs a little](../docs/spellcasting-201.md#the-text), and *Spellcasting 301*
  and *Gateway*, whose file [says each string's length](../docs/spellcasting-301.md#the-text). How the packing works is in
  [docs/spellcasting-101.md](../docs/spellcasting-101.md#the-text).
- **`vocab`** lists the vocabulary in `VOCAB.DAT`: each word's number, its parts of speech,
  and, for a synonym, the word it stands for and as what. It reads *Spellcasting 101*'s and
  *Timequest*'s plain file and *Spellcasting 201*'s packed one, which is in
  [docs/spellcasting-201.md](../docs/spellcasting-201.md#the-vocabulary), and
  [*Spellcasting 301*'s](../docs/spellcasting-301.md#the-vocabulary) and *Gateway*'s, which
  say the packed text's size another way.

## The modules

- **`mz.py`**: a DOS program's MZ header, image and relocations.
- **`program.py`**: what the two linkers' programs share: an overlay, a thunk, and the
  program's memory with overlays loaded.
- **`plink86.py`**: PLINK86's table of overlays and its thunks.
- **`rtlink.py`**: a .RTLink/Plus program: its records and their fixups, its run-time
  library linked in, and the overlay manager's table and thunks. How it works is in
  [docs/spellcasting-201.md](../docs/spellcasting-201.md#how-the-program-is-built).
  `src/Legendary.Core/Programs` has these three in C#, and builds the same memory byte for
  byte.
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
- **`pictures.py`**: the directory of a picture file, the five ways its pixels are packed,
  and a picture as a PNG file. `src/Legendary.Core/Pictures` is the same in C#, without the
  PNG, and reads the first game's alone.
- **`animations.py`**: a `.FLI` animation's frames, and an animated PNG of them.
- **`iso.py`**: a CD's image, read far enough to list a folder and take a file out.
- **`fonts.py`**: a font file's header, its widths and its letters, and a picture of them.
- **`gallery.py`**: the page `extract` writes to look through a game's pictures, sounds,
  songs and fonts.
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

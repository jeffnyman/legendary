# Gateway II: Homeworld

Legend Entertainment, 1993. The second game from Frederik Pohl's books, by Glen Dahlgren
and Mike Verdu.

This is the start of the first look: where the game's files are, how its program is
built, its text, and how its engine differs from the games' before. What is known for certain is said plainly; what is inferred says so. Addresses are
`segment:offset` in hex, with segments relative to where the program loads. Every one of
them can be looked at with the [tools](../tools/README.md).

## The copy looked at

The copy here is from Legend's collection *The Lost Adventures of Legend*, whose files are
dated 1996: one disc with eight games' folders and four demos. The copy has two
parts.

- **The installed game**, a folder of 46 files: the program, its text, vocabulary and
  objects, the music, fonts and regions.
- **The disc**, as an image of 397 megabytes, with a folder `GW2` of 76 files.

The 37 files that are in both are the same, byte for byte. What the disc has more is the
pictures, 38 files and 24 megabytes of them, and what the installed folder has more is what
the installer wrote: `RESTART.DAT`, and `LEGEND.INI`, which says where the rest is:

```
MODE=VGA
MOUSE=MOUSE
GAMEDATA=D:\GW2\
SAVEDATA=C:\GATEWAY2\
```

So everything but the pictures can be read from the game's folder alone, and the pictures
are on the disc and nowhere else. The folder has no recorded sounds, and neither has the
disc's.

The disc has the six games before this one too, in `S101`, `S201`, `S301`, `TQ`, `GW` and
`ERIC`; *Companions of Xanth* in `XANTH`; and `DGATE`, `MISSION`, `SHANNARA` and `HOBOKEN`,
which by their sizes and what is in them are demos of later games.

## How the program is built

As *Gateway* and *Eric the Unready* are: a program for the player to run, the game, its
overlays, a run-time library the two programs share, and the linker's startup code.

| File | What it is |
|---|---|
| `HOME.EXE` | What the player runs |
| `HOMEMAIN.EXE` | The game |
| `HOMEMAIN.OVL` | Its overlays, 341,318 bytes |
| `HOME.RTL` | The run-time library |
| `RTLINKST.COM` | The linker's startup code, 11,031 bytes where it was 7,615 |

| What | How many |
|---|---|
| Segments | 274 |
| Records in the program | 54 |
| Records in the library | 75 |
| Relocations in the program | 5,773 |
| Overlays | 34, one of them empty |
| Thunks | 1,003 |

The program starts at `1F61:0835`, in the overlay manager's segment, and its memory ends
at `56A0`. The overlays load at three places, each past the biggest overlay of the one
before:

| Place | Overlays |
|---|---|
| `3246` | 9 |
| `34AD` | 5 |
| `372D` | 20 |

Every one of the 1,003 thunks goes into the overlay it names.

### The linker is a newer one

The linker is .RTLink/Plus 6.10, where the three games before were linked by 5.02: the
header's version word, at `34`, is `0262`. Its file is 5.02's in every way but one.

**It has a second table of symbols.** The header has a table of symbols at `10`, eight
bytes each, by which the library's fixups name things in the program. 6.10 has another
after it, at the address in the header's `4A`, with how many at `4C`: one symbol here, in
the same form. A fixup says what its numbers are of, a segment, a group, a symbol or a
segment's value, by a number from 0 to 3; 6.10 has a fifth, 4, for a symbol of the second
table.

One fixup in the whole library uses it. It is in the run-time's startup code, a far call
with 3 as its argument, made, by the code around it, as the program ends in an error; and
what it calls is in the same segment at `0634`. Why that one call is named another way isn't known: what was
needed was to know where it goes, and the table says.

The records, the other fixups, the manager's table and the thunks are read by what reads
5.02's, with nothing changed.

## What the tools read as it is

| What | How much |
|---|---|
| The program and its overlays | all of it |
| The vocabulary | 2,028 words |
| The text | 4,875 strings, [with one change](#the-text) |
| The objects | 521, [in records of a new design](#the-objects) |
| The grammar | 383 forms, making 179 actions |
| The timers | 58 |
| The music | 82 songs in one file, 41 of them for the AdLib |
| The fonts | 17 |
| The regions | 9 files |

And what they don't yet:

- **The flags' routines and the saved game.** The code the tools look for isn't as it is
  in the games before: [the engine was built again](#the-engine-was-built-again). The
  objects, the grammar and the timers were the same, and are read now.
- **The pictures**, which are on the disc, and whose files are named another way:
  `EP_LAND.PIC` and `HT_DOCK4.PIC`, where the earlier games had `GATE_301.PIC`.

## The text

`HOMESTR.DAT` is 32 blocks of 4,875 strings, 628,562 bytes of text packed into 313,989,
with a tree of 216 nodes and 128 words: the commonest are `After`, `Fogram`, `Gateway`,
`He`, `Heechee`.

It is kept as [*Spellcasting 301*'s](spellcasting-301.md) is, a word for every string
saying how many bytes it is packed into, with one thing changed. **A block's size is four
bytes**, where it was two, so its entry in the table that starts the file is six bytes and
not four: how many strings, a word, and the size. No block needs it: the biggest is 56,371
bytes, under the 65,535 a word holds. The game before, *Eric the Unready*, still has two.

Read that way the file comes out whole: each block's strings add up to its size, the
blocks end where the file does, and all 4,875 strings unpack to plain text.

### How a string is named

As from *Spellcasting 301*: by a far pointer whose segment is `F000`, and whose offset is
the block times 1,024 and the string's place in it. The routine most are handed to, which
prints, is at `18A2:11FC`. There are 5,054 places that name a string, and 4,785 of the
4,875 strings are named by one. Block 1 is named by none: it is the help, the keys and what the mouse
does, shown by its block as *Eric the Unready*'s is.

## The engine was built again

The games before were found by the same few instructions in each: the ones that read the
table of objects, compare a number with their count, and look a type up. None of them is
in this game as it was. The routines are the same routines, in the same order, but they
were compiled by a later compiler and with the data kept another way:

- **A register is cleared with `xor`**, where the compiler before wrote `sub`.
- **The data segment's variables are read as they are**, `mov ax,[3336]`, where every game
  before loaded a segment into `ES` first even for those. So the count of objects is a
  plain word of the data segment, at `4B4C:3336`.
- **A table's entry is found with `BX`**, where it was `SI`.

Set beside *Eric the Unready*'s, 1,177 of the root's first 2,600 instructions are the same
but for their addresses, where that game and *Spellcasting 301* had 2,519. The routine that
performs an action is at `0000:0184`, where it was at `01A4` in the three games before.

## The objects

The table is found, and it is kept as before: six bytes an object, its type and a far
pointer to its record, in segment `27D7` from `00BE`. There are 521 objects, with names
from `OBJECT.DAT`: object 1 is `you`, in the Living Room.

**The records are a new design.** There are seven types, where there were eight, and
their sizes are none of the earlier games':

| Type | What it is here | How many | Bytes | Bytes of flags |
|---|---|---|---|---|
| 1 | A room | 123 | 22 | 2 |
| 2 | A thing | 210 | 28 | 6 |
| 3 | A thing with two lists: a ship, a cockpit, a pit, a dome | 79 | 32 | 6 |
| 4 | A name with nothing to it: `yes`, `love` | 5 | 12 | 2 |
| 5 | What is everywhere: the sky, a wall, a number | 7 | 18 | 2 |
| 6 | Scenery: stairs, mountains, cables | 73 | 18 | 6 |
| 7 | A person or a creature | 24 | 34 | 6 |

What each type is for is said from the objects that are of it, and not from the code.

A record starts as before, with its two names as places in `OBJECT.DAT` and then its
flags. After that the fields are in another order. The number of the object's routine and
where its nouns and its adjectives start are last in most types, where the words came
straight after the flags:

| Field | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| The object it is in | | `0A` | `0A` | | | | `0A` |
| Which of that one's lists it is on, a byte | | `0C` | `0C` | | | | `0C` |
| The next on that list | | `0E` | `0E` | | | | `0E` |
| Its own lists | `06` | `10` | `10`, `12` | | `0A`, `0C` | `0A` | `10`, `12`, `14` |
| Its routine's number | `0A` | `16` | `1A` | `0A` | `10` | `0C` | `1C` |
| Its nouns, and after them its adjectives | `10` | `18` | `1C` | `06` | `06` | `0E` | `1E` |
| A room's exits: how many, and the first | `0C`, `0E` | | | | | | |
| A room's row of what is also there | `14` | | | | | | |

The first four rows are from the routines that read and write each, which pick the place
by the object's type, as in every game before. The routine's number and the words have no
such routine that was found, and are from the records: the number counts up through the
objects, 505 of them, and the words' places count up beside it.

They are right by what they give. Every object on a list says it is in the object whose
list it is, all 261 of them; and every room's exits start where the room before's end, 404 exits
in all.

Three fields the routines read aren't known: a word at `12`, `14` and `16` of types 2, 3
and 7; another at `08`, `14` and `18` of types 1, 2 and 3; and one at `16` and `0E` of
types 3 and 5, which is two words at `18` in type 7. The player's are 100, and 500 and
1,000.

### The flags

There are 42, kept as from *Spellcasting 201*: a table says for each which bit it is and
which types have it. It is where it has always been, after the types' table, which is
seven entries of three bytes here and so ends on an odd byte: the flags start a byte on,
at `27D7:0016`. No code names it the old way, and the tools take it from there. Eight
flags are every type's, and the highest bit is 38, which is why four of the types have six
bytes of flags.

The flags have no names yet.

### The tables beside the objects

| What | Where | How it was found |
|---|---|---|
| The nouns of every object, a row each | `27D7:0CF4` | It follows the table of objects |
| The adjectives | `27D7:1F52` | It follows the nouns' last row |
| A room's row of what is also there | `2B9A:17AE` | The code that reads a row |
| The exits, four bytes each | `27D7:346A` | The code that compares a direction; and it follows the adjectives |
| The exits' routines, 61 far pointers | `27D7:3ABA` | It follows the exits |
| The exits' texts, 19 of them | `27D7:3BAE` | The code that reads one |
| The exits a variable opens, 5 rows of eight bytes | `27D7:3BFA` | The code that tests one |
| The objects' routines, 505 far pointers | `2DA0:0000` | The code that calls one |

The first three tables of the segment are where they have always been: the nouns start
where the table of objects ends, as in *Spellcasting 301*, and the adjectives where the
nouns end, a row ending at a word with its top bit set. The tools take those two from
that, and the others from the code that reads each, which is the later compiler's: an
index is doubled twice with `add bx,bx`, where the compiler before shifted it.

With them an object's words read, by the vocabulary: the tuning fork is `fork` or
`artifact`, and `blue`, `tuning`, `heechee` or `metal`. All 521 objects have a noun.

**A room has one row, where it had two.** What is also there is a row of four bytes an
object, in a segment of its own; the row of where can be seen into from a room, which the
games before had beside it, has no field in the record that was found.

**The exits are of four kinds**, 404 of them: 225 to a room, 160 that a routine decides,
14 that only say something, and 5 that a variable opens. There are no doors among them,
where every game from *Spellcasting 201* had some; a door here may be a routine's to
decide, which hasn't been looked at.
What an exit says is a string named by its number, as from *Spellcasting 301*, and reads:

```
exit north, kind 3: says: You wander towards the ice structures, but find them too ...
```

**The objects' routines** are called through one table, by the number in the record, from
the routine at `0525:00EE`: 516 objects have one, and all but one are in the overlays, in
19 of the 34.

## The grammar

The two tables are in segment `2B9A`, with what is also in a room after them: the forms
at `045E`, 383 of ten bytes each, and the ways a form goes on to a second object at
`1354`, six bytes each. Before the forms is the table of actions, as from *Spellcasting
301*: eleven far pointers, the routines that run before an action, and then six bytes an
action, its flags and its routine.

A form is kept as before, and the tools read it with nothing changed once the tables are
found:

```
383 forms
activate OBJECT: action 1  (the object held, carried, here, in things here)
aim OBJECT at OBJECT: action 2  (the object held, carried; must be had, the second here, in things here)
ask OBJECT about OBJECT: action 4  (the object here, the second anywhere)
```

They make 179 actions, and each action's routine is found, with its overlay. The parser's
own grammar, which `sentence` prints, is read as it was too.

What changed is how the code reads them. The games before handed a row's address to a
routine that compared it; this one reads a row's fields where they are, with the row's
number times ten or six in `BX`. The tools find the two tables by those.

## The timers

There are 58, numbered, and kept as from *Spellcasting 201*: a table of the ones that are
going, four bytes each, a timer's number and its count.

| What | Where |
|---|---|
| The table of timers going | `3C73:9010` |
| How many are going, and which is being run | `4B4C:3136`, `3138` |
| The routines: empty the table, start, stop, ask about, ask the count, run | `0234:0008`, `0026`, `0096`, `00F8`, `0130`, `016C` |
| The routine that calls a timer's routine by its number | `0611:000A` |

The tools list 55 of them, each with its routine and the places that start it, stop it
and ask about it:

```
timer 2, 1f61:3491, which calls 372d:2750 in overlay 27
    372d:007d in overlay 23: object 16 Landing Site starts it with a count of 7
    372d:01ed in overlay 23: object 16 Landing Site asks about it
```

Of the 261 calls to those routines, 44 aren't read: the later compiler makes a call's
arguments in ways the tools don't take yet, and the three timers no call is found for are
likely among them. The timers have no names yet.

### What isn't read yet

Two things found by code of the old kind still aren't: the routines that test and change
a flag, which `flags` needs to say where each flag is used, and the code that saves a
game, which says how `RESTART.DAT` is laid out.

## Still to do

- The routines that test and change a flag, the saved game, and the pictures.
- The 44 calls about a timer that aren't read, and the timers' names.
- The flags' names, and the three fields of a record that aren't known.
- The game hasn't been run on the PC, which will need the disc as a drive for its pictures.

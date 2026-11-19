# Gateway II: Homeworld

Legend Entertainment, 1993. The second game from Frederik Pohl's books, by Glen Dahlgren
and Mike Verdu.

This is the start of the first look: where the game's files are, how its program is
built, its text, its objects, grammar and timers, how its engine differs from the games' before,
and its pictures and animations. What is known for certain is said plainly; what is inferred says so. Addresses are
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
| The saved game | [by its parts](#a-saved-game), which add up to the file |
| The pictures | 1,130, once [copied from the CD](#the-pictures), and 17 animations |
| The music | 82 songs in one file, 41 of them for the AdLib |
| The fonts | 17 |
| The regions | 9 files, 797 regions, [in a new form](#the-regions) |

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

The tools list 56 of them, each with its routine and the places that start it, stop it
and ask about it:

```
timer 2, 1f61:3491, which calls 372d:2750 in overlay 27
    372d:007d in overlay 23: object 16 Landing Site starts it with a count of 7
    372d:01ed in overlay 23: object 16 Landing Site asks about it
```

Of the 261 calls to those routines, 17 aren't read, and the two timers no call is found
for, 21 and 41, are likely among them.

**A call's arguments are made another way.** The compiler before put each constant in
`AX` and pushed it. This one puts the first in `AX` and the next in `CX`, `mov ax,7` /
`push ax` / `mov cx,198` / `push cx`, so that most calls with two constants, a timer's
count and its number or a flag and its object, went unread until the tools took that too.

### What each timer does

Each is named from what its routine prints when it runs, and the ones that print nothing
from whose routines start them:

| Timer | What it does | Timer | What it does |
|---|---|---|---|
| 1 | a question, which yes and no end | 30 | the ape men in the Grasslands |
| 2 | the pictures fade away | 31 | the spear at the River Bank |
| 3 | nothing | 32 | the alien robot comes, click-clack |
| 4 | nothing | 33 | the guidething wakes and speaks |
| 5 | the artist's own | 34 | the ship's computer speaks, turn by turn |
| 6 | a diggle crawls by | 35 | the Place of Seeing, turn by turn |
| 7 | the hypo unit's own, every turn | 36 | the field generator's own |
| 8 | the crystal rod and the portal change color | 37 | the metal disk seals, and you fall |
| 9 | a button pops out | 38 | a probe crashes |
| 10 | a screen goes wild | 39 | something falls from the sky |
| 11 | a tentacle grabs the kord | 40 | the technicians flood out of the temple |
| 12 | the insect creatures attack, turn by turn | 41 | the technicians swarm in |
| 13 | the creatures enter the specimen cage | 42 | the speaker speaks |
| 14 | the creatures leave the specimen cage | 43 | the beams of light shift back |
| 15 | the creatures in the cage slow and die | 44 | the workthing and the case, turn by turn |
| 16 | the creatures scrabble again | 45 | the workthing comes back from the vault |
| 17 | the green blob, turn by turn | 46 | the tram, turn by turn |
| 18 | the carnivores in the clearing | 47 | the prototype's power runs low |
| 19 | the fruit pod's own | 48 | the prototype's battery recharges |
| 20 | the lizard takes the red egg | 49 | sinking into the slime |
| 21 | nothing | 50 | the PV goes dormant |
| 22 | the man with the thermos, turn by turn | 51 | the escort and the red tag |
| 23 | your Heechee escort hurries you on | 52 | a voice in your condominium |
| 24 | the lecture hall's light turns green | 53 | the terrorists' attack, turn by turn |
| 25 | your communicator pings | 54 | the grenade explodes |
| 26 | your escort grows impatient | 55 | the match's own |
| 27 | the Heechee city's own | 56 | the radio's messages, turn by turn |
| 28 | the students and the hologram | 57 | the hijacking's own |
| 29 | the ape men in the Cavern | 58 | a strange creature emerges |

Timers 3, 4 and 21 are routines of three bytes that do nothing. Timer 1 is started by no
call that is read; `yes` and `no` stop it, and `down` asks whether it is going. Twelve
print nothing. Seven are named for whose routines start them: 5, 7, 19, 27, 36, 55 and 57.
Five are named from the objects their routines name: 28 to 31, and 51.

## Where a flag is used

The three routines that test, set and clear a flag are at `0261:0576`, `05AE` and `05E4`,
the later compiler's versions of the ones from *Spellcasting 201*: each has a routine
find the flag's byte and bit from the table, and then does its one thing. The tools find
them by how they end, and `flags` says where every flag is tested and changed: 1,157 calls,
with 26 more whose flag or object isn't a constant.

```
flag 3: not known (bit 2, for every type)
    at the start, on 9 objects: 2 of type 1, 1 of type 2, 1 of type 5, 5 of type 7
    tested 4 times, set 1, cleared 1
```

### What the flags are

They are the flags of *Gateway* and *Eric the Unready* under other numbers again, and 37
of the 42 have names, each from the objects that start with it and the actions that test
it:

| Flag | What it says | Has it at the start | By what |
|---|---|---|---|
| 1 | takes `some`, and is still `it` | 15 | ice, dust, water |
| 2 | not known | 0 |  |
| 3 | its name takes no article | 9 | you, Diana Tolson, Mission Control |
| 4 | plural: `some`, and `they` | 84 | stairs, mountains, icicles |
| 5 | its name takes `an` | 50 | an access panel, an escape ship, an artist |
| 6 | a mark its own routine keeps, of something that has happened | 4 | tested 103 times and set 41, most by an object's own routine |
| 7 | touched, or for a place visited | 0 | `get` and `open` set it |
| 8 | seen by the player | 9 | set 81 times, nearly all by objects' routines |
| 9 | can be taken, and hasn't been: taking it can be special | 198 | routines before `drop`, `throw` and `put` test it; taking clears it |
| 10 | not listed among a room's contents | 282 | `get` clears it |
| 11 | alive: an animal or a plant | 33 | `feed`, `kill`, `scare` and `greet` test it |
| 12 | lit, or for a place has light | 124 | every room, and one thing |
| 13 | can't answer: asleep, or dead | 0 | `ask`, `attack`, `show` and `tell` test it; nothing starts with it |
| 14 | not known | 18 |  |
| 15 | things can be on it | 38 | `drop on`, `empty` and `climb out` test it: shelves, a bed, a platform |
| 16 | a door | 4 | the four doors |
| 17 | can be taken | 54 | routines before `drop`, `throw` and `put` test it |
| 18 | can be read | 31 | `examine` tests it: screens, books, a plaque |
| 19 | can be opened and closed | 24 | `open` and `close` test it |
| 20 | locked | 6 | `open`, `close` and `unlock` test it |
| 21 | open | 39 | tested 192 times; `empty` and `examine` test it |
| 22 | can be turned on and off, or lit | 11 | `light`, `extinguish` and `activate` test it |
| 23 | a liquid | 4 | water, hot springs, ooze |
| 24 | a key | 1 | `lock` and `unlock` test it: the crystal rod |
| 25 | cuts | 3 | `cut` tests it: the crystal cutters, a needle |
| 26 | another mark its own routine keeps | 2 | tested 53 times and set 33, most by an object's own routine |
| 27 | can be eaten | 0 | `eat` tests it; nothing starts with it |
| 28 | burns | 11 | a torch, a match, books |
| 29 | turned on, or alight | 5 | `activate` sets it and `extinguish` clears it |
| 30 | not known | 0 |  |
| 31 | not known | 2 |  |
| 32 | can be got in or on | 18 | `climb in` tests it: chairs, a bed, a tree |
| 33 | a weapon, which can cut | 7 | `cut` tests it: spears, a laser rifle |
| 34 | can be worn | 14 | `put on` and `remove` test it |
| 35 | a person | 18 | `greet`, `tell` and `feed` test it: Diana Tolson, ape men, the Heechee |
| 36 | a container | 40 | `drop in`, `empty`, `pour` and `look in` test it |
| 37 | what's dropped in it lands outside it | 0 | `drop` tests it; nothing starts with it |
| 38 | female | 2 | Diana Tolson, and a dead scientist |
| 39 | outdoors | 34 | 34 rooms: the Landing Site, the Village |
| 40 | not known | 3 |  |
| 41 | can be sat on | 18 | `sit` tests it |
| 42 | can be folded | 1 | `fold` tests it: the blue coverall |

Five aren't named. Flag 14 is on a glow, a uniform, some rocks and a dome, and one routine
tests it, the first of overlay 1, which is the routine that tested *Eric the Unready*'s
unnamed flag 9. Flag 2 is tested once and on nothing; 31 is on the giant robot and the
green blob; 40 is on a container, a belt and a case, and `examine` and `open` test it; and
nothing has or uses 30.

## A saved game

`RESTART.DAT` is 15,839 bytes, and is laid out as *Spellcasting 301*'s is:

| Bytes | What |
|---|---|
| 34 | That start it, from `4B4C:81DE` |
| 13,430 | The records of the 521 objects |
| 2,365 | 295 saved variables |
| 8 | Four words more |
| 2 | A count of timers, 0 as the game starts, and four bytes for each |

The parts add up to the file, to the byte. The routine that writes them is in overlay 4,
from `3246:0444`, and is the one the games before have, compiled again: the table of
saved variables, six bytes each, where one is, its segment and its size, is at
`23C5:037C`, and how many there are is a word of the data segment, at `4B4C:31AE`. There
are 295, where *Eric the Unready* has 84 and *Gateway* 261.

The four words are copied from elsewhere in the data segment as a game is saved, and
written from `908C`, `908E`, `9088` and `908A`. What they are isn't known, as it isn't in
*Spellcasting 301*.

## The regions

The nine `.RGN` files say what is where in the pictures, for the mouse. Their form is new
with this game: a region is an object's number, a count of points, and the points, each
two words, across and down in pixels, where it was an object and a rectangle in six bytes.
Two points are a rectangle's corners and more are the corners of an outline: 678 of the
797 regions are outlines, the longest of 143 corners. [*Companions of
Xanth*](companions-of-xanth.md#the-regions) keeps its regions the same way.

## The pictures

The game's own folder has none. They are on the CD, in the folder `LEGEND.INI` names, and
the tools' `disc` command lists what a game's CD has that its folder hasn't and, with
`--copy`, copies it in: 39 files here, 38 of them named `.PIC`. With them the folder is
whole, and nothing reads the CD's image again.

The 38 are two kinds of file under one name.

**Twenty-one are picture files**, `HOME_101.PIC` to `HOME_401.PIC`, kept as
[*Spellcasting 301*'s](spellcasting-301.md) are and read with nothing changed: 1,144
entries, 1,130 of them with a picture. They are for three displays, by the file's number:
517 for the VGA in 16 colors, 604 for the VGA in 256, and 23 for the wider screen of 16
colors. There are none for the EGA, which every game before had, and none for the Super
VGA, which *Eric the Unready* has.

**Seventeen are animations**, and nothing of Legend's: they are `.FLI` files as Autodesk
Animator wrote them, 320 by 200 in 256 colors, named `.PIC`. They are the title,
`GW2_LOGO.PIC` and `LEGEND.PIC`, and the scenes between the game's parts: a ship over a
planet in `EP_LAND.PIC`, and `HT_DOCK4.PIC`, `HH_EXPLO.PIC` and the rest. Together they are
2,760 frames, just under three minutes.

A `.FLI` file is 128 bytes and then its frames:

| At | What |
|---|---|
| 0 | The file's size, four bytes |
| 4 | `AF11` |
| 6 | How many frames |
| 8 | The width, and at 10 the height |
| 12 | 8, the bits a pixel |
| 16 | How long a frame is shown, in seventieths of a second: 5 in most |

A frame is its size, `F1FA`, a count of chunks, and the chunks, each its size, a kind, and
what the kind says: 11 sets colors, 12 changes some lines of the picture before, 13 makes
it black, 15 is a whole picture packed a line at a time, and 16 a whole picture as it is.
The format is Autodesk's and well known; what is here is that these files are that, and
every one of the seventeen reads to its last frame with only those five kinds.

The tools' `animations` command lists them, and with `--out` writes each as an animated
PNG, which a browser plays. The frames looked at are right: the title's letters, `GATEWAY
2 HOMEWORLD`, and the ship over its planet.

## Where a command is parsed and performed

The routines are the ones *Eric the Unready* has, compiled again, and were found by
setting the two games' code side by side, as that game's were found from *Spellcasting
301*'s. Less of it lines up, 1,177 of the root's first 2,600 instructions where that pair
had 2,519, but the routines come in the same order and do the same things.

| What | Where |
|---|---|
| The routine that performs an action | `0000:0184` |
| How many are being performed, one inside another | `4B4C:3120` |
| The action, a byte, and the direction after it | `4B4C:9132`, `9133` |
| Its object and its second object | `4B4C:9084`, `9082` |
| The room, and the actor | `4B4C:314E`, `3150` |
| The turns taken, and the clock, in minutes of the day | `4B4C:3154`, `3156` |
| Where the main loop's `setjmp` returns | `0000:0750` |
| A token taken: the next, after a full stop, after `and` | `3246:20F9`, `218A`, `2208`, in overlay 2 |
| The word a token is | `4B4C:9086` |
| The form found, which one, and the action it makes | `0000:0C5C`, `4B4C:9092`, `3C73:3AE6` |
| The objects found for a lot of phrases | `0135:0A2B` |
| The two lots, twelve bytes each, and how many | `3C73:3ACE`, `3C73:3AC8` |

**The action and its objects are in the data segment.** Every game before kept them in
a far segment with the lots of phrases, and read them through `ES`. Here the action, its
direction, its two objects, the word a token is and the form found are plain variables of
the data segment, and only the lots and the timers are where they were, in `3C73`.

The routine that performs an action makes the same ten calls in the same order, to an
object's routine, the routine before an action, and the action's own, and tests what each
answers; they are closer together, the routine being shorter. Going somewhere is action
134.

The game counts turns and minutes both, as *Eric the Unready* does: it starts at eight in
the morning on turn 0, and a command takes a minute and a turn.

## The game as it runs

The game starts on [the PC](the-pc.md) and takes commands, and the player's `--watch` and
`--panel` follow it:

```
world: 8:00 am, turn 0, in Living Room (288)
world:   exits: southwest to Bedroom (289); southeast to Foyer (290)
world:   timer 50 (the PV goes dormant) started: 10 turns to go
world:   words: examine [verb] sofa [noun]
world:   form 68: examine OBJECT, which is action 46
world:   object "sofa", looked for anywhere near: white sofa (306)
world:   action 46: examine white sofa (306)
world:     answered by white sofa (306), asked as the object: 1
world: 8:02 am, turn 2, in Living Room (288)
world:   words: southeast [verb, adjective or noun]
world:   form 282: southeast, which is action 134
world:   action 134: go southeast
world:   you (1) moved: from Living Room (288) to Foyer (290)
```

**It asks for its pictures on drive D:**, at `D:\GW2\`, as `LEGEND.INI` tells it. The
player gives it one of two things. When the game's folder has the pictures, copied from
the CD by the tools' `disc`, the PC makes `D:\GW2` the game's own folder, with no CD in
the drive. Otherwise, when an image of the CD is beside the game, it puts that in the
drive. With the first, the game opens `LEGEND.PIC`, `GW2_LOGO.PIC` and `CD_ROM.PIC`, plays
its title, and goes on into the story: the Artifact, six kilometers long, among the stars.
The second hasn't been tried.

**It writes `RESTART.DAT` as it starts**, in the folder `LEGEND.INI` names for saved
games, `C:\GATEWAY2\`: the state the game begins from, so that `restart` can go back to
it. The games before came with that file and only read it. The game's folder is the root
of drive C: here and has no such directory, and the game said `Unable to open file
'C:\GATEWAY2\RESTART.DAT'` when the story reached your home. So the player makes that
directory the game's own folder too: what the game reads there is the game's, and what it
writes goes with its saved games.

Its title's animations play, and its music, and the game plays on from there.

## Still to do

- What starts a saved game, and the four words before its timers.
- The game with an image of its CD in the drive.
- The words at `4B4C:3152` and `3158`, 1 as the game starts.
- Flags 2, 14, 30, 31 and 40, and what the timers that print nothing do.
- The three fields of a record that aren't known.

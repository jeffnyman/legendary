# Gateway

Legend Entertainment, 1992. Frederik Pohl's *Gateway*, made a game by Glen Dahlgren and Mike
Verdu, on the system of [*Spellcasting 301*](spellcasting-301.md), which came out the same
year.

This is the first look, and so far it is how the program is built, its objects, and what
of it the tools already read. What is known for certain is said plainly; what is inferred says so. Addresses
are `segment:offset` in hex, with segments relative to where the program loads. Every one of
them can be looked at with the [tools](../tools/README.md).

## How the program is built

As *Spellcasting 301* is: Microsoft C, linked by .RTLink/Plus 5.02, with a run-time library
and overlays.

| File | What it is |
|---|---|
| `GATE.EXE` | What the player runs |
| `GATEMAIN.EXE` | The game |
| `GATEMAIN.OVL` | Its overlays, 498,115 bytes |
| `GATE.RTL` | The run-time library the two programs share |
| `RTLINKST.COM` | The linker's startup code |

| What | How many |
|---|---|
| Segments | 283 |
| Groups | 2: the data's at `4C0B:0000`, and the overlay manager's at `1CF4:0000` |
| Records in the program | 56 |
| Records in the library | 64 |
| Overlays | 58 |
| Thunks | 1,284 |

The program starts at `1CF4:076C`, in the overlay manager's segment.

### The overlay manager

It is *Spellcasting 301*'s, with its table in another place again. The three words that say
where the table is are found the same way: the count is at `1CF4:0C19` and the entries
start at `1CF4:0C1F`, six bytes on. That game's are eight bytes on and *Spellcasting 201*'s
four, so what the manager keeps between the count and the first entry differs from one
build of it to the next, and the tools take any of the three.

An entry is the same 18 bytes, and a thunk the same ten. The thunks come after the
manager's segment, from `1CF4:12C7`, and every one goes into the overlay it names.

The 58 overlays load at six places:

| Place | Overlays |
|---|---|
| `33DF` | 5 |
| `3727` | 4 |
| `3A56` | 35 |
| `3A65` | 5 |
| `3A77` | 7 |
| `3A9F` | 2 |

The last four are close together, 15, 18 and 40 paragraphs apart, where the earlier games'
places were each past the biggest overlay of the one before. So an overlay at `3A56`
would lie over the three places after it, and how the manager keeps them apart, or which
can be in memory together, hasn't been worked out.

`GATEMAIN.OVL` is laid out as before, and is 13 bytes short of where the last overlay's size
would end it.

## What is already read

These are kept as *Spellcasting 301* keeps them, and are read by what reads that game:

- **The vocabulary**: 2,644 words.
- **The text**: 56 blocks and 6,013 strings, 663,324 bytes packed into 335,613.
- **The grammar**: 402 forms that make 195 actions, with the actions' routines in a table
  and 18 of them with a routine that comes first.
- **The parser's own grammar**, the same 46 ways.
- **The pictures**: [imploded](spellcasting-301.md#the-pictures), 1,519 of them, 15 of no
  size.
- **The saved game**: 34 bytes that start it, 20,650 in the records of 734 objects, 1,079 in
  261 saved variables, and a count of timers, which come to `RESTART.DAT`'s 21,765 bytes
  exactly. It has no four words before the count, as *Spellcasting 301*'s has.
- **The music, the sounds, and the pictures' regions.**

## The objects

734 of them, in the eight types: 110 rooms, 241 things, 46 people, 15 doors, and the rest
scenery and words. They are kept as *Spellcasting 301*'s are, with their names in
`OBJECT.DAT`, their flags by a table, lists that end at a word with its top bit set, and
each record with the number of its routine.

| What | Where |
|---|---|
| The table of types, three bytes each | `27E4:0000` |
| The table of flags, four bytes each: 42 flags | `27E4:0018` |
| The table of objects, six bytes each | `27E4:00C0` |
| How many objects | `4C0B:3BEE` |
| The nouns, and the adjectives | `27E4:11F4`, `27E4:29AE` |
| What's also in a room, and what's seen from it | `27E4:6418`, `4C0B:3C08` |
| The exits, four bytes each: 518 rows | `27E4:417E` |
| The doors an exit goes by | `27E4:4996` |
| What an exit says, and the exits a variable opens | `27E4:4AD2`, `4C0B:3BF0` |

The records are that game's with the flags of [*Timequest*](timequest.md#the-objects): four
bytes of them, where that game had two, for a room, a person and two kinds of scenery.

| Type | Bytes | Bytes of flags | Its routine's number |
|---|---|---|---|
| 1, a room | 26 | 4 | `10` |
| 2, a thing | 36 | 6 | `22` |
| 3 | 12 | 2 | `0A` |
| 4 | 20 | 2 | `12` |
| 5 | 22 | 4 | `14` |
| 6, a door | 24 | 2 | `16` |
| 7, a person | 32 | 4 | `1E` |
| 8 | 24 | 4 | `16` |

A thing's record is four bytes shorter than *Spellcasting 301*'s: it doesn't have the four
that game added. The other fields follow the flags and the two lists of words in the order
they have there.

So do the exits. A room's are rows of the one table, and they are of *Timequest*'s five
kinds, which the *Spellcasting* games had cut to three: 232 to a room, 35 by a door, 13 that
only say something, 236 a numbered routine decides, and 2 a variable opens. What an exit
says is named as a string of the file is, by a far pointer with `F000` for its segment. An
exit a variable opens has its row another way than *Timequest*'s: the room, a word that
isn't understood, the variable, and the number of one of the exits' texts for what it says
while it's shut, where that game had a far pointer to the text. From Below Cave, up leads to
the Entry Chamber once a byte is set, and until then says there doesn't seem to be any way
to climb the cliff.

### The routines

709 of the 734 objects have a routine, and there are 696 routines. That is more than a
table of far pointers in one segment was made to hold, so the table is in two parts of 512,
at `2F10:0000` and `2F90:0000`. The routine that calls an object's, at `0476:00F8`, takes the
number's top bits for the part and its low nine for the place in it.

The two other kinds of routine [*Spellcasting 301* keeps in
tables](spellcasting-301.md#two-more-kinds-of-routine) are here too: 44 objects have one that
gives their own text, and 209 one that says which object a phrase means.

With the routines named, the tools find the timers and every use of a flag:

- **The timers**: 108, numbered, kept by the same six routines, in segment `02E3`. 106 are
  started, stopped or asked about by number, in 594 calls.
- **The flags**: 42, in the game's table, with 1,300 calls about one read.

## How the program names a string

As [*Spellcasting 301* does](spellcasting-301.md#how-the-program-names-a-string): as a far
pointer with `F000` for its segment and, for its offset, the block times 1,024 and the
string's place in it. The routine that prints is at `189C:1069`.

| How | Places |
|---|---|
| Handed at once to the routine that prints | 4,468 |
| Made in code for something else | 324 |
| Kept as data | 1,389 |

That is 6,181 places, and 5,948 of the file's 6,013 strings. Far more of them are kept as
data than in that game, which had 231; what the tables of them are for hasn't been looked at.

The first look had this wrong. With the overlays not yet read, only the root's code was
searched, and it found 140 strings and took the game to name the rest some other way.

## Still to do

- The flags and the timers have no names.
- The game hasn't been run on the PC.

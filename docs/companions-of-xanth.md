# Companions of Xanth

Legend Entertainment, 1993. Piers Anthony's *Demons Don't Dream*, made a game by Michael
Lindner. It is Legend's first game without a parser: everything is done with the mouse, on
pictures and on lists of verbs and things.

This is the start of the first look: what is in the game's folder, what of it the tools
already read, how its program is built, its objects, and the verbs it has in place of a
parser, and how an action is performed. What is known for certain is said plainly; what
is inferred says so. Addresses are `segment:offset` in hex, with segments relative to where
the program loads. Every one of them can be looked at with the [tools](../tools/README.md).

## What the folder has

| File | What it is |
|---|---|
| `XANTH.EXE` | The game: there is no program to run before it |
| `XANTH.OVL` | Its overlays, 326,091 bytes |
| `XANTHSTR.DAT` | The text |
| `OBJECT.DAT`, `RESTART.DAT` | The objects' names, and the state the game starts from |
| `XANTH_00.HLP` to `XANTH_02.HLP` | Three files of a kind the earlier games don't have, not looked at |
| `XANTH_nn.PIC`, `.RGN`, `.FNT`, `.MUS`, and `.RS` files | Pictures, their regions, fonts, music and sounds |
| `LEGEND.INI` | Where the game looks: `GAMEDATA=D:\XANTH\`, `SAVEDATA=C:\XANTH\` |

There is no `VOCAB.DAT`: a game with no parser has no words to know. But `OBJECT.DAT` and
`RESTART.DAT` are here as in every game before, so the world is still objects with records.

The game came on a CD, and an image of it is beside the game, in `cd`. It is a raw image,
2,352 bytes a sector with a `.cue` file, where *Gateway II*'s was plain ISO 9660: each
sector as the disc has it, with its 2,048 bytes of data sixteen bytes in. The tools' `disc`
reads either. The disc's folder `XANTH` has 96 files, and the game's folder has all but
four of them: three short lists of the installer's, and `XANTH_01.VOC`, 190,251,008
bytes, which by its name and size is the game's speech. It starts with a count, 1,327, and
that many places in the file; it hasn't been looked at further, or copied.

## What the tools read as it is

| What | How much |
|---|---|
| The text | 80 blocks, 5,542 strings, 387,571 bytes packed into 196,695 |
| The pictures | 27 files, 3,294 entries |
| The music | 4 files, 86 songs |
| The sounds | 29, RealSound |
| The fonts | 6 |
| The regions | 17 files |

The text, the pictures, the music and the regions are kept as the games before keep
theirs. The regions matter more here than they did: they say which part of a picture is
which object, which in this game is how the player says what they mean.

## How the program is built

It is Microsoft C linked by .RTLink/Plus, as every game since *Spellcasting 201*, but put
together a third way.

The games from *Spellcasting 201* to *Gateway II* are two programs, a launcher and the
game, that share a run-time library in a file of its own; and the game isn't a program DOS
can load, but records the linker's startup code puts in place. **This one is whole**: one
ordinary program, which DOS loads and relocates from its MZ header, with the linker's
overlay manager and the library linked into it. `XANTH.EXE` says so itself, in the text
`.RTLink(R)/Plus run-time code` where the manager's code begins.

| What | How much |
|---|---|
| The MZ header | 73,401 bytes, nearly all relocations: 5,304 of them |
| The image | 191,656 bytes |
| After the image | 43,193 bytes |
| Overlays | 70 |
| Thunks | 883 |

The program starts at `1DC1:0835`, in the overlay manager's segment, at the same offset
*Gateway II*'s manager starts at. The manager's table is found as before, and every one of
the 883 thunks goes into the overlay it names. The 70 overlays are small, from one to
seven kilobytes, and load at three places:

| Place | Overlays |
|---|---|
| `30CB` | 15 |
| `321E` | 53 |
| `322B` | 2 |

### What comes after the image

The file is longer than its MZ header says the program is, by 43,193 bytes. They are the
rest of the program's memory: its far data and its data segment, which come after the
place the overlays load. The file leaves out the stretch between, where the overlays go,
and the linker's own code reads the rest in.

Nothing in the header says where it goes, and no relocation of the header's is in it.
Where it goes was worked out from two things. The data segment of a Microsoft C program
starts eight bytes before the text `MS Run-Time Library`, which is 19,592 bytes into the
43,193; and the data segment is `38AF`, the segment past the overlays that the program's
relocations name most, 597 times. So the bytes go from `33E6:0008`, just past the last
overlay, which ends at `33E5`. Put there, 41 of the 75 places where the program pushes
the address of something in its data segment land on the start of a string; put anywhere
else tried, one to nine do.

The tools and `Legendary.Core` read the program that way.

## The objects

There are 498, and they are kept as [*Gateway II*'s](gateway-ii.md#the-objects) are: a
table of six bytes an object, its type and a far pointer to its record, after the types'
table and the flags' in the same segment, `23A1`, with the count a word of the data
segment, at `38AF:084C`. The names are in `OBJECT.DAT`. Object 1 is `You`, in the bedroom.

There are five types, where that game has seven:

| Type | What it is here | How many | Bytes | Bytes of flags |
|---|---|---|---|---|
| 1 | A room | 70 | 18 | 2 |
| 2 | A thing | 326 | 28 | 4 |
| 3 | A thing with two lists: a desk, an envelope, a computer | 69 | 32 | 4 |
| 4 | What is in many places: a door, a path, the sky | 14 | 14 | 2 |
| 5 | A person or a creature | 19 | 32 | 2 |

What each type is for is said from the objects that are of it, and not from the code.

| Field | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| The object it is in | | `08` | `08` | | `06` |
| Which of that one's lists it is on, a byte | | `0A` | `0A` | | `08` |
| The next on that list | | `0C` | `0C` | | `0A` |
| Its own lists | `06` | `0E` | `0E`, `10` | `06` | `0C`, `0E`, `10` |
| Its routine's number | `0A` | `1A` | `1E` | `0C` | `1E` |
| A room's exits: how many, and the first | `0E`, `10` | | | | |

All but the last row are from the routines that read and write each, which pick the place
by the object's type. They are the routines *Gateway II* has, in the same order, from the
same compiler told to make faster code: where that game multiplies an object's number by
six, this one adds it up. A room's exits are from the records.

**A record has no words.** The two words that say where an object's nouns and adjectives
start, which every game before has, aren't here: nothing is typed, so nothing has a name
to be typed by.

**Three fields are new**, and they are what a game played with the mouse needs of an
object in place of words:

| Field | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Its icon, a picture's number | `0C` | `14` | `18` | | `18` |
| The verb a click on it does | | `16` | `1A` | `08` | `1A` |
| Where its list of verbs starts | | `18` | `1C` | `0A` | `1C` |

Each has a routine to read it, [as the others have](#the-objects), and what they are is
from the code that calls those.

They are right by what they give: every object on a list says it is in the object whose
list it is, all 296 of them; every room's exits start where the room before's end, 152
exits in all; and the routines' numbers go from 1 to 448. The letter from Pia and a
bracelet are in the envelope, which is on the desk, which is in the bedroom.

There are 23 flags, in a table as before. None has a name yet.

## The verbs

Where the games before had a vocabulary and a grammar, this one has a table of verbs: 89
of them, eight bytes each, in segment `22F6` from its start.

| At | What |
|---|---|
| 0 | The verb's name, a place in the data segment: `Read`, `Talk to` |
| 2 | What joins it to a second object, or 0: the `to` of `Attach ... to` |
| 4 | How many objects it takes: 0, 1 or 2 |
| 6 | Its number |

Sixty-four take one object, 22 take two, and three take none: `Look`, `Wait` and `Walk`.
The numbers go from 1 to 92 with three left out, 43, 57 and 86. Up to 84, `Wipe`, they
are in the alphabet's order, and the seven after it aren't, so those look to have been
added late: `Launch`, `Lower`, `Raise`, `Draw ... on`, `Attack ... with`, `Dig up ... with`
and `Pet`. One is out of place among the first, `Look at` at 19, between `Enter` and
`Fill`: where a verb beginning with E would be, as `Examine` does.

**An object has a verb of its own**, which is what a click on it does when no verb has
been chosen. 124 objects have one: `Open` for 26, `Flip` for 23, `Talk to` for 18 of the
19 people, `Read` for 11. The letter from Pia is read, the bracelet worn, the computer
turned on. The routine that settles what a click means, around `08A7:5700`, asks for it
first, and falls back on `Look at`, or on `Talk to` for an object with flag 23.

**An object has a list of the verbs it takes**, beside the ones everything takes. The
lists follow the table of objects in its segment, where the lists of nouns were in the
games before, each ended by a zero, and an object's record says where its own starts. 262
objects have one: the Post-It takes `Attach ... to`, `Remove` and `Read`, and Metria
`Kiss`, `Grab` and `Chase`. Every verb in every list is one the table has.

**And a table says which two objects a verb of two takes**: 70 rows of a verb and two
objects, after the verbs in the same segment, from `22F6:04D8`. `Put diskette on disk
drive`, `Attach Post-It to personal computer`, `Unlock gate with key`, `Tie rope to
anchor`. The same routine looks a verb and its two objects up there.

**The picture is the object's icon.** It is a number from 263 to 472, on 142 objects: the
rooms, the people, and the things that can be carried. A routine that makes a list of
what is shown puts down each object with its picture, or with 256 when it has none.

A number is a file and a place in it: the file's number times 256, and the picture's.
Every one is of file 1, `XANTH_01.PIC`, which has 217 pictures, numbered to 216, and the
highest an object names is 472, which is 256 and 216. Every picture an object names is 24
by 20, small enough to be one of a row. And they are what they should be: the envelope's, 263, is an envelope, the letter's two pages, the Post-It's a
yellow note and the bracelet's a ring; Nada's and Metria's are their faces; and the
rooms' are small drawings of them, a desk with a computer for the bedroom, a door for the
entrance, a refrigerator for the kitchen. Picture 0 of the file, 256, is a blank square.

That the high byte is the file is from these alone, where it is always 1: no object names
a picture of another file, so it hasn't been seen to be anything else.

The tools' `verbs` command lists the verbs and the pairs, and `objects` with a number says
an object's icon, its own verb and its list.

## Once a verb is settled

**A verb is an action, and it is performed by the routine the games with a parser have.**
Under the mouse and the pictures the game is the one Legend had been writing since
*Spellcasting 101*: something makes an action and up to two objects, and a routine
performs them, asking each object concerned and then the action itself. Here the something
is a click where it was a sentence.

The routine is at `08A7:3C27`, and it is *Gateway II*'s to its warning, `[Warning:
infinite perform recursion terminated.]`, which this game has too.

| What | Where |
|---|---|
| The routine that performs an action | `08A7:3C27` |
| How many are being performed, one inside another | `38AF:0100` |
| The action, a word now, where it was a byte | `38AF:6A06` |
| Its object and its second object | `38AF:6A04`, `6A02` |
| The routine that calls an object's routine | `08A7:711A` |
| The routine that calls the one before an action | `08A7:54B4` |
| The routine that calls an action's own | `08A7:54D9` |

It makes the same ten calls in the same order: four to objects' routines, one to the
routine that comes before the action, four more to objects', and the action's own, and
stops at the first that answers.

**An action's routine is found by the action's number**, in a table of six bytes each, the
number and a far pointer, at `22F6:02C8`, between the verbs and the pairs. The games
before went straight to the entry by the number. There are 88, every one in an overlay.
Three verbs have none, `Enter`, `Mail` and `Press`, so what those do is all in the
objects' routines; and two actions have a routine and no verb, 43 and 57, which are two
of the three numbers the table of verbs leaves out, so they are done and never chosen.

**Two actions have a routine that comes before**, `Give ... to` and `Put ... on`, called
by a switch and not from a table.

**An object's routine** is called by the number in its record, as from *Spellcasting
301*, from a table of far pointers: 448 routines, for 492 of the 498 objects, in 45 of the
70 overlays, which is likely why the overlays are small and many; that an overlay is a
place and what is in it hasn't been checked.

The tools' `verbs` lists each verb's routine, and `objects` with a number an object's.

## Still to do

- The speech, `XANTH_01.VOC`, which is on the CD alone.
- The exits' table and the flags' names.
- How a click becomes a verb and its objects: the routine around `08A7:5700` settles what
  is meant, and what calls it, from the pictures' regions and the lists, hasn't been
  followed.
- The `.HLP` files.
- The game hasn't been run on the PC.

# Companions of Xanth

Legend Entertainment, 1993. Piers Anthony's *Demons Don't Dream*, made a game by Michael
Lindner. It is Legend's first game without a parser: everything is done with the mouse, on
pictures and on lists of verbs and things.

This is the start of the first look: what is in the game's folder, what of it the tools
already read, how its program is built, its objects, and the verbs it has in place of a
parser, how a click becomes a command, and how an action is performed. What is known for certain is said plainly; what
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
bytes, which is [the game's speech](#the-speech). `disc --copy` puts them in the folder.

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

There are 23 flags, in a table as before; [what they are](#the-flags) is below.

## The exits

A room's exits are rows of one table, as from *Timequest*: four bytes each, a direction, a
kind and a word, at `23A1:10FA`, in the segment of the objects' table, 152 rows for 66 of the 70 rooms. The routine that walks,
at `08A7:60CA`, goes through a room's rows for the direction.

| Kind | How many | Its word |
|---|---|---|
| 1 | 72 | The room it leads to |
| 2 | 3 | A row of the doors' table, at `38AF:084E`: the room, and the door, which must have flag 14, open, or the game says it isn't |
| 4 | 77 | The number of a routine, which gives the room, or says why not and gives none |

Half the exits are a routine's, where the games before had a few: an exit here mostly
depends on something. The routines are 56 far pointers, called at `08A7:70A5`. The three
doors are the pier's gate, a door at the fairway and the barrow's.

The directions are the ten of a compass with up and down; none is `in` or `out`. The
tools' `objects` with a room's number lists its exits.

## The flags

The three routines that test, set and clear a flag are at `08A7:25EA`, `2624` and `2658`,
and the program calls them 751 times, of which the tools read 732. The names are from who
has each flag as the game starts and what tests it; the tools' `flags` shows both.

| Flag | What it is | From |
|---|---|---|
| 1 | Takes `some` | The routine that makes a name tests it; the desert, smoke, lava, mustard, snow and water have it |
| 2 | Its name takes no article | The same; You, Grundy, Kim and the other people named |
| 3 | Plural | The same; cliffs, meadows, pencils, flowers, dishes |
| 4 | Its name takes `an` | The same; ocean, envelope, eye, icicles, egg, arch |
| 5 | Touched, or for a place visited | Set on what is taken; tested of rooms, 40 times, by two routines |
| 6 | Seen by the player | Set where a thing is named, and on what is taken, and on the room |
| 7 | Not yet taken or moved | On 106 things; the one place that clears it is the routine that takes a thing |
| 8 | Alive | The 19 people, and 28 things; `Poke` and `Give ... to` test it |
| 10 | A mark its own routine keeps | Tested 190 times and set 75, nearly all by objects' own routines |
| 11 | Things can be on it | `Put ... on` tests it of the second object; desk, table, mat, pier |
| 12 | Can be taken | `Take` tests it |
| 13 | Can be seen through | One window has it; `Look at` and `Open` test it |
| 14 | Open | Tested 196 times, set 31, cleared 30; a door must have it |
| 15 | Can be opened and closed | `Open` and `Close` test it |
| 17 | A container | `Put ... on` tests it of the second object; envelope, icebox, boxes |
| 19 | A place in the player's own world | Four rooms: bedroom, entrance, kitchen, screen |
| 20 | Came in the game's box | Five things, which the game box's routine tests it of |
| 21 | Outdoors | 39 rooms; `Throw ... at` tests it of the room |
| 22 | Female | The routine that makes a name tests it; Kim, Nada, Jenny, the ogress, Metria, Ma Anathe |
| 23 | A person | 16 of the people; `Give ... to` tests it, and a click on one is `Talk to` |

Flags 7, 13, 19 and 20 are the least sure. Three have no name: 9, which 61 of the rooms
have and one routine tests; 16, which nothing has at the start and a cherry bomb sets; and
18, which 56 things that can be carried have.

The words for where the player is and who is acting are the other way round from what was
taken above them: `38AF:0256` is the room, by its being what the exits are asked of, and
`38AF:0258` the actor.

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

## From a click to a command

A command is three words, a verb and up to two objects, as a sentence was in the games
before. The player makes it a piece at a time, pointing at a verb in a list and at things
in the picture or in a list, and the game puts the pieces together.

| What | Where |
|---|---|
| Where the pointer is, across and down | `38AF:69E4`, `69E6` |
| The sentence being made: its verb, object and second object | `38AF:0050`, `0052`, `0054` |
| The routine that looks at what the pointer is on | `08A7:175F` |
| The routine that adds a piece to the sentence | `08A7:0594` |
| The routine that settles it, and makes its words | `08A7:55D3` |
| The command settled: its verb, object and second object | `38AF:69FA`, `6A0A`, `6A08` |

**What the pointer is on.** The routine at `08A7:175F` hands the pointer's place to one
that says which part of the screen it is in, which item of that part, and what kind of
thing that is. Two kinds are an object, and a routine gives its number from the part and
the item; one kind is a verb, from the list of them. Anything else is nothing.

**Putting the pieces together.** The routine at `08A7:0594` is given the sentence so far
and what was just pointed at, a verb or an object. A verb becomes the sentence's verb if
it has none. An object becomes its object if it has none, and its second object if it has
one and this is another. Then it has the sentence settled.

**Settling it.** The routine at `08A7:55D3` is where [the verbs' tables](#the-verbs) are
used. With no verb it takes the object's own. It looks the verb up in the object's list,
and a verb of two with its two objects in the table of pairs. It makes the sentence's
words, from the verb's name, the objects' names and the verb's joining word, and it leaves
the command in the three words it is performed from.

**A turn.** The routine that does a turn, around `08A7:3E33`, copies the command to where
the action and its objects are kept, and [performs it](#once-a-verb-is-settled). Then, as
in the games with a parser, it calls the player's routine and the room's, runs the timers
and counts the turn, at `38AF:025C`. `Walk`, verb 81, is the one special case: its object
isn't an object but the direction, and is put in a byte of its own, at `38AF:6E5C`.

### The regions

Which thing a place in a picture is, is said by the `.RGN` files, as before. Their form is
new, and *Gateway II* has it too: the tools read both games' regions wrongly until now.

A file still starts with six bytes for each picture: where its regions start, the object
the rest of the picture is, and how many regions. A region was six bytes, an object and a
rectangle in bytes. Now it is an object's number, a count of points, and the points, each
two words, across and down in pixels:

```
picture 0, the rest of it 34 bedroom
    0, 85 to 263,121  35 desk
    0,  0 to  85, 71  45 window
    an outline of 8 corners, in  77, 67 to 105, 96  46 pen holder
    an outline of 15 corners, in  79, 70 to 249,116  39 personal computer
```

Two points are a rectangle's corners. More are the corners of an outline, so a region can
be the shape of the thing: 591 of this game's 728 regions are, the longest of 80 corners,
and 678 of *Gateway II*'s 797. Read this way every picture's regions end where the next
picture's start, in every file of both games.

### The parts of the screen

The screen is 320 by 200, and the routine at `08A7:09AC`, called after each turn, lays it
out. The numbers are in its code, and the pictures it draws are the sizes of the boxes
it draws them in:

| Part | Left, top to right, bottom | What shows it |
|---|---|---|
| The verbs, a list down the left | 2, 4 to 49, and as far down as the list goes | Each verb's box is added as it is written |
| Four buttons under them | 4, 168 to 45, 195, in two rows of two | Four boxes drawn there |
| The picture | 51, 4 to 314, 125 | 264 by 122, which is what the regions are in |
| The words of the sentence being made, and what the game says | 51, 128 to 314, 151 | |
| A box of 32 by 44, by its shape the compass | 51, 152 to 82, 195 | Picture 257 is drawn there, and is 32 by 44 |
| The things carried, as icons | 88, 152 to 287, 195 | Picture 258, of 200 by 44: two rows of eight icons of 24 by 20 |
| Two buttons, one over the other | 293, 152 to 314, 173, and 293, 173 to 314, 195 | Pictures of 22 by 44 |

That the box of 32 by 44 is the compass, and what each of the four buttons is, are guesses
from the shapes; the game hasn't been run to see.

**What can be pointed at** is kept in lists the game makes as it draws, up to eight of
them, and not in a table in the program: how many lists at `38AF:51DE`, a far pointer to
each at `38AF:67C2`, and how many items each has at `38AF:67E2`. An item is 20 bytes:

| Offset | What |
|---|---|
| 0 | What kind of thing it is |
| 1 | Its flags; with `80` it can't be pointed at |
| 2 | Its rectangle: left, top, right and bottom, a word each |
| 10 | For an object, its number |
| 14, 16 | For a region with an outline, how many corners, and a far pointer to them |

The routine at `18A5:218A` goes through every list for the items whose rectangle the
pointer is in, and gives the last, so what is drawn later is on top. The test, at
`18A5:2BE2`, is the rectangle, and for a kind 3 with corners, whether the pointer is inside
the outline too.

| Kind | What | How it is known |
|---|---|---|
| 1 | A button | The two buttons at the right are added as this |
| 3 | A region of the picture | The one kind tested against an outline |
| 7 | An object's icon | Added with the object's number and a box of 24 by 20 |
| 8 | A verb in the list | Added by the routine that writes the verbs |

Kinds 3 and 7 are the two that give an object, which is the word at 10; kind 8 gives a
verb, by where it is in the list. Kinds 2, 4, 5 and 6 are added by routines that haven't
been read.

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

## The speech

`XANTH_01.VOC` is every line the game speaks, and isn't Creative's file of that name. It
starts with a word, 1,327, and that many offsets of four bytes, of which the last is the
file's length: 1,326 numbers for lines, 104 of them with nothing, so 1,222 lines. A line
is from its offset to the next and is the sound alone, a byte a sample with 128 for
silence, as a Sound Blaster plays it. Every line starts at a multiple of 4,096: the header
is filled out with `FF`, and a line with silence.

**How fast it plays is by ear, not read.** Nothing in the file says. The Sound Blaster's
driver in the program takes its speeds from a table at `38AF:4218`, of millionths of a
second a sample: 60, 120, 181 and 241, which are the RealSound speeds, and then 91 and 45,
which are 10,989 and 22,222 a second. The pitch of the voices fits 16,572 and 22,222 both,
and played at each, it is 22,222 that sounds right and 16,572 slow. At that the file is
143 minutes, with a line of five seconds in the middle and one of nearly four and a half
minutes the longest. The call that plays a line, and so the speed it asks for, hasn't been
found; nor has what gives a line its number.

The tools' `sounds` counts the lines, and with `--out` writes each as a WAV file.

## Still to do

- What says which line of the speech goes with which text, and the call that plays one.
- Flags 9, 16 and 18, and the 56 routines of the exits.
- What the four buttons under the verbs do, and the kinds of thing on the screen that
  haven't been read: 2, 4, 5 and 6.
- The `.HLP` files.
- The game hasn't been run on the PC.

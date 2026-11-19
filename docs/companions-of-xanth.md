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
| `XANTH_00.HLP` to `XANTH_02.HLP` | [The game's guided tours](#the-guided-tours), which the earlier games don't have |
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
| The verbs, a list down the left | 2, 4 to 49, and as far down as the list goes | The tour clicks `Take` at 18, 9 and `Put` at 15, 17 |
| The compass | 4, 129 to 45, 167 | The tour clicks directions at 12, 135 and 12, 148 |
| Four buttons under it, in two rows of two | 4, 168 to 45, 195 | The tour clicks them: the system's menu at 35, 174, the map at 10, 187, undo at 35, 185 |
| The picture | 51, 4 to 314, 125 | 264 by 122, which is what the regions are in |
| The command being made, and what the game says | 51, 128 to 314, 151 | |
| Boxes for the companions | 51, 152 to 82, 195 | Picture 257 is drawn there, and is 32 by 44; the tour says what they are for |
| The things carried, as icons | 88, 152 to 287, 195 | Picture 258, of 200 by 44: two rows of eight icons of 24 by 20. The tour clicks one at 102, 162 |
| Two buttons, one over the other | 293, 152 to 314, 173, and 293, 173 to 314, 195 | Pictures of 22 by 44 |

The game's own [guided tour](#the-guided-tours) says what the parts are for, and its clicks
are where the code puts them. The fourth button under the compass, at the top left, is by
elimination the one to wait, which the tour names and doesn't click. What the two buttons
at the right do isn't said; by their place they move through the things carried. The map
button changes the picture for a map of the rooms visited, and becomes the button back.

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

## The timers

The timers are the parser games': a routine is put by with a count of turns, and after
each turn that takes time the counts go down and the routines whose count is out are
called. They are numbered, as from *Timequest*: the table holds up to sixteen at once, four
bytes each, a number and a count, and a second table has a far pointer for each of the 45
numbers.

| What | Where |
|---|---|
| The routine that empties the table | `08A7:208B` |
| The ones that start a timer, stop it, ask if it is going, and ask its count | `08A7:209F`, `2118`, `2191`, `21D5` |
| The routine that runs them after a turn | `08A7:2222` |
| The routine that calls a timer by its number | `08A7:6600` |
| How many are in the table, and which is being run | `38AF:00EC`, `00EE` |

The program calls the four 152 times, of which the tools read 143, about 42 of the 45
timers. A timer is started with a count 72 times, 49 of them with a number in the code,
which is from 1 to 15 turns. None is started to run every turn, which the games before
did for what goes on all the time; here that is left to the rooms' and objects' routines.

Twenty-seven have a name, from what their routine prints:

| Timer | What | Started by |
|---|---|---|
| 2 | The courier brings the package | The telephone, 9 turns on |
| 3 | The telephone rings | Four routines, 12 turns on |
| 5 | A companion beckons through the cavern door | The Dark Cavern |
| 6 | The cherry bomb goes off | The cherry bomb, the turn after |
| 8 | The butter melts | The buttercup and the firefly, 5 turns on |
| 9 | Doubts about the companion chosen | A tree and a spring |
| 12 | A cough drop falls from the bush | The forest |
| 13 | Edsel's offer refused: the game is lost | 10 turns on |
| 14 | The eye scream melts | The ice palace and the eye scream |
| 15 | A door shimmers in the desert | The desert |
| 20 | The firefly escapes in the barrow | The spring and the barrow |
| 25 | The agony moss's pain | The agony moss, 2 turns on |
| 26 | The cracker dough in the oven | The oven |
| 27 | The firecracker explodes | The firecracker, the turn after |
| 28 | The wineskin, given and snatched back | The lava path |
| 31 | The well overflows | Not found |
| 32 | The virus stops Com-Pewter | The cylinder |
| 34 | The sword waved in the desert | The desert |
| 35 | The castle's little door slams closed | The castle door and the little door, 3 turns on |
| 36 | Jenny's song | Two routines |
| 37 | The cricket at the pier | The pier, 5 turns on |
| 38 | The Gap Dragon comes nearer | The gap |
| 39 | Stanley's firestorm | 2 turns on |
| 40 | Fracto's snowstorm | Two routines |
| 42 | The telephone rings, as the game starts | The bedroom, 2 turns on |
| 43 | Kim takes The Prize | The attic |
| 44 | A voice from Nada's arms in the dungeon | The dungeon |

The names are from a line or two of what each prints, and not from reading the routine
through, so they say what a timer is about more surely than what it does. The other
eighteen print nothing that says, or weren't bounded well enough to tell.

The tools' `timers` lists each timer with every call about it.

## The guided tours

The three `.HLP` files are the game's help: recordings of someone playing, which the game
plays back by moving its own pointer and clicking, with words shown as it goes.
`XANTH_00.HLP` is the whole tour, of 26 clicks; `XANTH_01.HLP` is the part on making a
command, and `XANTH_02.HLP` the part on the compass.

A file is text, a line an event:

```
00050 0008  18   9   0
00000 0004  18   9   1
00000 4000   0   0   0
20, 16, 280, That series of mouse clicks created a game command and executed it.
```

The numbers are the time since the event before, in thousandths of a second; the event;
where the pointer is, across and down; and a button.

| Event | What |
|---|---|
| `0008` | The pointer is here. There is one every tick of the PC's clock, so they are 50 or 60 apart |
| `0004` | A click here, with button 1 |
| `4000` | Words are shown. The line after is where, across and down, how wide, and the words |
| `8000` | The end |
| `0002` | Not understood: it comes among the others, nearly always with no button |

The tour is also the game's makers saying how it works, and it gives their names for
things found above: an object's own verb, used when it is clicked with no verb chosen, is
its **default verb**, and the verbs an object adds to the list are its **special verbs**.
A command isn't done until a second click; until then it changes as the pointer moves,
with the default verb for whatever is pointed at. The right button, or a click on nothing,
clears it. And a thing inside an open thing carried is drawn to the right of it.

*Mission Critical* has one such file, `MC001.HLP`, in the same form. The tools' `tour`
lists a game's tours: what each says, and where it clicks.

## The speech

`XANTH_01.VOC` is every line the game speaks, and isn't Creative's file of that name. It
starts with a word, 1,327, and that many offsets of four bytes, of which the last is the
file's length: 1,326 numbers for lines, 104 of them with nothing, so 1,222 lines. A line
is from its offset to the next and is the sound alone, a byte a sample with 128 for
silence, as a Sound Blaster plays it. Every line starts at a multiple of 4,096: the header
is filled out with `FF`, and a line with silence.

**How fast it plays isn't in the file.** The Sound Blaster's driver in the program takes
its speeds from a table at `38AF:4218`, of millionths of a second a sample: 60, 120, 181
and 241, which are the RealSound speeds, and then 91 and 45, which are 10,989 and 22,222 a
second. It is the last: [the game, running](#the-game-as-it-runs), gives the Sound Blaster
each piece of a line at 22,222 a second, and played at that the voices sound right, where
at 16,572 they are slow. So the file is 143 minutes, with a line of five seconds in the
middle and one of nearly four and a half minutes the longest. What gives a line its number
hasn't been found.

The tools' `sounds` counts the lines, and with `--out` writes each as a WAV file.

## The game as it runs

The game starts on [the PC](the-pc.md) and is played by the mouse, with its pictures, its
music and its speech, and the player's `--watch` and `--panel` follow it:

```
world: turn 0, in bedroom (34)
world:   exits: northwest to entrance (51)
world:   timer 3 (the telephone rings) started: 12 turns to go
world:   action 68: Take Post-It(tm) (44)
world:     answered by the action's own routine: 1
world:   Post-It(tm) (44) moved: from personal computer (39), its list 2 to You (1)
world:   Post-It(tm) (44): flag 5, touched, set
world: turn 1, in bedroom (34)
world:   action 81: go northwest
world:   You (1) moved: from bedroom (34) to entrance (51)
world:   exits: southeast to bedroom (34); west to kitchen (64); north: routine 0 decides
```

There are no words to follow, so an action is said by its verb, and the rest is as for the
games with a parser: who answered, what moved, which flags changed, the timers. The game
keeps no time of day, and the watch says the turn alone.

**It needs nothing new of the PC.** As *Gateway II* does, it asks for its files at
`D:\XANTH\` and keeps its saved games in `C:\XANTH\`, and the player makes both the
game's own folder. It takes the mouse through a handler of its own, which draws the
pointer, and Escape cuts short each line of the demons' wager that the game opens with.

**The game says how fast its speech is.** It reads a line from `XANTH_01.VOC` 16,384
bytes at a time and gives each to the Sound Blaster at 22,222 samples a second, which is
what [was taken by ear](#the-speech).

**The screen is as [read from the code](#the-parts-of-the-screen)**, seen with the game
running: the verbs, the compass with `WAIT`, the system's button, `MAP` and `UNDO` under
it, the picture, the line of text, the boxes for the companions, and the things carried
with two arrows at their right, which by their look move through them.

What isn't followed is the click itself, as the parser games' words are: which thing the
pointer is on, and the command as it is made. And where the main loop comes back to when a
routine gives an action up hasn't been found, so that isn't said.

## Still to do

- What says which line of the speech goes with which text.
- For the watch: the command as it is made by clicks, and where the main loop's `setjmp`
  returns.
- Flags 9, 16 and 18, the 56 routines of the exits, and the eighteen timers with no name.
- The kinds of thing on the screen that haven't been read: 2, 4, 5 and 6; and event
  `0002` of the tours.

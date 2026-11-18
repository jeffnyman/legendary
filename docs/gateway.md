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

## What each timer does

108 timers, by number. A timer's routine mostly prints, and what it prints is named where
it's used, so the names are from the text beside each routine.

| Timer | What it is | Timer | What it is |
|---|---|---|---|
| 1 | the woman leaves you the gold card | 55 | the spider robot comes for the waste |
| 2 | the poker game, turn by turn | 56 | the spider robot goes |
| 3 | the beast limps about | 57 | the robot's waste box |
| 4 | the Dorman sun sets | 58 | the field actuator's hum rises |
| 5 | a byte changed | 59 | something crashes through the anemones |
| 6 | two bytes forgotten | 60 | the spider decides |
| 7 | it has dried off | 61 | the spider's pincers |
| 8 | they have dried off | 62 | the enraged spider |
| 9 | the Ballroom, turn by turn | 63 | a worm bursts from the ground |
| 10 | the wheel turns, turn by turn | 64 | the worm chews at your helmet |
| 11 | the beast goes, with the last berries | 65 | the pod's tongue |
| 12 | the boulder lands on you | 66 | your drink, every thirty turns |
| 13 | the portals flare | 67 | the snake strikes |
| 14 | the demon wanders, turn by turn | 68 | the old man's room, every three turns |
| 15 | the demons grow restless | 69 | it burrows out of sight |
| 16 | the hydra's head is knocked into the inferno | 70 | it scampers out of view |
| 17 | the demon stops coughing | 71 | the bartender snores |
| 18 | the demon saws at the net | 72 | the salamander comes out of the lake |
| 19 | tiredness, every thirty turns | 73 | the salamander goes |
| 20 | the hydra hunts for you | 74 | a wailing from the east |
| 21 | the beast slams you against the wall | 75 | a wailing, further off |
| 22 | the dust spills from the bag | 76 | a bubbling moan |
| 23 | Thom Seldridge comes into the bar | 77 | nothing |
| 24 | Terri Neilson comes to the bar | 78 | the aliens file off |
| 25 | the robot bartender comes over | 79 | the program insists, turn by turn |
| 26 | the robot bartender trundles away | 80 | the firestorm from the broken pyramid |
| 27 | the robot bartender chirps | 81 | the tech goes back to his newsletter |
| 28 | Nubar snatches the drink | 82 | the chasm walls lurch, turn by turn |
| 29 | Nubar plays the trivia game | 83 | at the Tunnel Antechamber |
| 30 | Terri Neilson at the bar, turn by turn | 84 | the shoots grow around you |
| 31 | the boots come round the planet | 85 | the salamander comes up in the tank |
| 32 | nothing | 86 | the cheeping stops |
| 33 | the prospectors fill the conference room | 87 | Becker begins his story |
| 34 | the ship handling class ends | 88 | Becker's story ends |
| 35 | a guard clears the room for a meeting | 89 | a seed spins into the air |
| 36 | the meeting ends | 90 | it finishes the leaves and runs |
| 37 | a man comes down the corridor | 91 | sleepiness, every ten turns |
| 38 | Perry scurries back | 92 | the blister disappears |
| 39 | Thom Seldridge leaves the bar | 93 | Becker goes through the wooden door |
| 40 | Nubar Kamalian leaves the bar | 94 | a leaf swats the air |
| 41 | Terri Neilson leaves the bar | 95 | the raft sinks |
| 42 | a look at where you are, every thirty turns | 96 | a sense of forgetting something |
| 43 | a word forgotten | 97 | an opening appears in the wall |
| 44 | the MP arrests you at the Museum | 98 | Becker sprints ahead |
| 45 | a creature scurries across the desert | 99 | what comes through Becker's door |
| 46 | a yellow creature flits near | 100 | Becker asleep, turn by turn |
| 47 | the blue fog clears | 101 | the tech explains the Guide |
| 48 | people stare below your navel | 102 | your allotted time ends |
| 49 | the alien berries, turn by turn | 103 | the banner, every ten turns |
| 50 | the old maintenance man appears | 104 | Nubar Kamalian sits down next to you |
| 51 | the old man looks for his key | 105 | the Tanning Room, turn by turn |
| 52 | the old man finishes and goes | 106 | Becker wants a nap |
| 53 | the portal disappears | 107 | Becker wakes |
| 54 | the Hydra lair's rock and path, every ten turns | 108 | the tan, every sixty turns |

Named from what they change or move, having no text that was found: 5, 6 and 43, which only
set or clear variables; 9, 30, 39, 40, 42, 54, 57, 66, 83, 103 and 105. Timers 32 and 77
are a return and nothing else. Timers 77, 86, 99 and 101 aren't started by number.

Many restart themselves with a count of 1 and so run every turn while a scene lasts: the
poker game, the roulette wheel, the demon, the program that insists.

## The flags

42 flags, each with a bit and the types that have it, in the table at `27E4:0018`. Three
routines in segment `0157` do everything with them: `03A6` tests one, `03E0` sets it and
`0413` clears it.

They are the things [*Timequest*'s flags](timequest.md#the-flags) are, more than the
*Spellcasting* games': a thing that can be taken has one flag before it has been and another
after, and a person can be one who can't answer. The names are from the code that asks
about each, or from what has it.

| Flag | What it is | From |
|---|---|---|
| 1 | takes `some` | what has it |
| 2 | touched; a place, visited | `get` and `open` set it |
| 4 | its name takes no article | what has it |
| 5 | its name takes `an` | what has it |
| 6 | plural | what has it |
| 7 | seen by the player | set 191 times |
| 8 | not listed among a room's contents | `get` clears it |
| 10 | can be taken, and hasn't been | the routine before `get` asks it, and `get` clears it |
| 12 | open | `open` sets it, and says `already open` |
| 13 | a liquid | what has it; `throw` asks |
| 14 | can be opened and closed | `open` and `close` |
| 15 | a door | the doors |
| 16 | opened for you as you go through it | an exit by a door that isn't open says `[Opening it first.]` and opens it, or says it isn't open |
| 17 | what's dropped in it lands outside it | what has it: a scale; `drop` asks |
| 18 | can be eaten | `eat` |
| 19 | a container | `open` says what's found in one; `empty`, `pour` |
| 21 | alive | what has it; `kill`, `attack`, `greet`, `feed` |
| 22 | lit; a place, has light | every place has it |
| 23 | can be worn | `put on`, `remove` |
| 24 | cuts | `cut` |
| 26 | can be read | `examine` |
| 27 | cuts | `cut` |
| 28 | things can be on it | `climb in`, `empty`, `pour` |
| 29 | can be taken | the routines before `get`, `drop` and `throw` |
| 30 | a key | `open` asks it of what's used, and goes on to `unlock` |
| 31 | can be seen through | `open` doesn't say what's found in one |
| 33 | turned on | `activate` sets it, `put out` clears it |
| 34 | can be got in or on | `climb in`, `look in` |
| 35 | locked | `open` says it appears to be locked |
| 36 | can be turned on and off | `activate`, `light`, `put out` |
| 37 | playing cards | what has it; `fold` asks |
| 38 | can be sat on | `sit` |
| 39 | outdoors | 53 of the 110 places; `throw`, `look` |
| 40 | female | what has it |
| 41 | a person, and not you or a monster | everyone but you, the hydra and the demons |
| 42 | can't answer | `ask`, `tell`, `attack` |

Flag 16 is new. An exit by a door that isn't open asks it of the door: with it, the game
says `[Opening the door first.]`, opens it, and lets you through; without, the door isn't
open and you stay. Six doors have it, the ones to rooms.

Flags 17, 21, 37 and 41 are named from what has them. Not named: 3, which the ripe berries
alone have; 9, which nothing starts with and the objects' routines test 231 times and set
119, each for a purpose of its own; 11, which 56 objects of scenery have; 25, which 29 things
to carry have; and 32. Flag 20 is in the table and nothing has it or asks about it.

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

- Six flags have no names, and some timers are named only from what they move.
- The game hasn't been run on the PC.

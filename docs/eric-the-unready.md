# Eric the Unready

Legend Entertainment, 1993. Bob Bates's comedy of a knight who is not up to it, on the
system of [*Gateway*](gateway.md) and [*Spellcasting 301*](spellcasting-301.md).

This is what the first look found: how the program is built, what of it the tools already
read, and its objects. What is known for certain is said plainly; what is inferred says so.
Addresses are `segment:offset` in hex, with segments relative to where the program loads.
Every one of them can be looked at with the [tools](../tools/README.md).

## The copy looked at

The game came on floppy disks and, later, on a CD. The copy here has both beside the
installed game: its folder has `floppy`, the disks' files; `CD`, an image of the disc; and
`ERIC`, the game as installed, which is what is read. So the game's files are a folder
deeper than the other games', and the tools look there for them.

It is run as `ERIC XGA XMOUSE BLASTER 7 220`, with `MT32 2 330` after it for a Roland
MT-32. `XGA` is new: the earlier games were told `VGA` at the most.

## How the program is built

As *Gateway* is: Microsoft C, linked by .RTLink/Plus 5.02, with a run-time library and
overlays in files of their own. The linker's files are the same, and the tools read them
with nothing changed.

| File | What it is |
|---|---|
| `ERIC.EXE` | What the player runs |
| `ERICMAIN.EXE` | The game |
| `ERICMAIN.OVL` | Its overlays, 372,336 bytes |
| `ERIC.RTL` | The run-time library the two programs share |
| `RTLINKST.COM` | The linker's startup code |

| What | How many |
|---|---|
| Segments | 248 |
| Records in the program | 58 |
| Records in the library | 68 |
| Relocations in the program | 6,269 |
| Overlays | 29 |
| Thunks | 990 |

The program starts at `2307:076C`, in the overlay manager's segment, and its memory ends
at `572B`. The 29 overlays load at three places, each past the biggest overlay of the one
before, as in the games before *Gateway*:

| Place | Overlays |
|---|---|
| `31DF` | 6 |
| `345B` | 3 |
| `37C8` | 20 |

## What the tools read as it is

With nothing changed for this game:

| What | How much |
|---|---|
| The vocabulary | 1,683 words |
| The text | 30 blocks, 4,923 strings, 472,425 bytes packed into 242,183 |
| The grammar | 450 forms, making 200 actions |
| The actions' routines | found, each with its overlay |
| The music | 80 songs in one file, 40 of them for the AdLib |
| The sounds | 53, RealSound |
| The fonts | 20 |
| The regions | 6 files |
| The pictures | 938 of 1,285, and all of them [with one change](#the-pictures) |

The pictures took one change, [below](#the-pictures).

## The objects

There are 537, in the eight types the system has had since *Spellcasting 101*:

| Type | What it is here | How many | Bytes | Bytes of flags |
|---|---|---|---|---|
| 1 | A room | 74 | 24 | 2 |
| 2 | A thing | 225 | 36 | 6 |
| 3 | A name with nothing to it: `yes`, `music`, a pirate spoken of | 17 | 12 | 2 |
| 4 | What is everywhere: the sky, the sun, a wall, a number | 11 | 20 | 2 |
| 5 | Scenery that many rooms share: water, trees, hills | 38 | 22 | 4 |
| 6 | A door, in two rooms | 4 | 26 | 4 |
| 7 | A person or a creature | 58 | 32 | 4 |
| 8 | Something fixed in a room, or a part of something | 110 | 22 | 4 |

What each type is for is said from the objects that are of it, and not from the code.

The table of them, six bytes an object, and the tables beside it are found by the same
code as in *Gateway*, in segment `2B79`: the nouns at `2B79:0D4E`, the adjectives at
`2B79:183C`, the exits at `2B79:1CF2` and the doors at `2B79:2006`. The lists end at a
word with its top bit set, as from *Spellcasting 301*.

A record is *Gateway*'s, in three types changed:

- **A room** is *Spellcasting 301*'s, 24 bytes with two bytes of flags, where *Gateway*'s
  had four.
- **A door** is two bytes longer, 26, with four bytes of flags.
- **Something fixed** is two bytes shorter, 22.

Each was read off the records themselves: a record ends with the number of the object's
routine, which counts up through the objects, and the words before it fall into place from
there. They are right by what they give. Every object on a list says it is in the object
whose list it is, for all 537; a room's exits start where the room before's end; and
`RESTART.DAT` is as long, to the byte, as the records make it.

| What | How many |
|---|---|
| Names, from `OBJECT.DAT` | 537 |
| Routines of objects, by number | 517 |
| Flags | 40, of which 33 are set on something as the game starts |
| Exits | 197 |

The exits are of three of the five kinds *Gateway* has: 88 to a room, 6 through a door, and
103 that are a routine's to decide, more than half, where *Gateway* had under half.

## The pictures

There are 1,309 entries in 39 files, 24 of them a palette alone and 1,285 with a picture,
kept as [*Spellcasting 301*'s](spellcasting-301.md) are:
packed by PKWARE's implode, each saying the display it is for. The file's number says it
too, `ERIC_1nn.PIC` for display 1 and so on.

| Display | What | Entries | The biggest |
|---|---|---|---|
| 1 | The EGA | 28 | |
| 2 | The VGA, 16 colors | 481 | |
| 3 | The VGA, 256 colors | 429 | |
| 4 | A screen of 16 colors wider than the VGA's | 24 | 713 by 108 |
| 5 | The Super VGA, 256 colors | 347 | 640 by 480 |

**Display 5 is new**, and is what the `XGA` the game is run with asks for: 640 by 480 in
256 colors. The title's picture for it says so in its own letters, `256 Color Super VGA`.
It is kept as display 3 is, a palette of 256 colors, three bytes each, and a byte a pixel.
The reader took its palette for one of 16, and so started unpacking in the middle of the
colors; told it is 256, all 347 unpack to exactly the bytes their sizes need. Those looked
at are right: the title, filling the screen, and a man in a green tunic.

**Display 4** is in *Spellcasting 301* and *Gateway* too, the same 24 pictures in each: the
compass and its pieces, and `LEGEND ENTERTAINMENT COMPANY presents`, which is 713 across.
So it is for a screen wider than 640, in 16 colors. That would be the Super VGA's 800 by
600, which hasn't been confirmed from the program.

## The flags

There are 40, kept as *Gateway* keeps them: a table says, for each flag, which bit it is
and which types have it. They are *Gateway*'s flags under other numbers, and 36 have names:

| Flag | What it says | Has it at the start | By what |
|---|---|---|---|
| 1 | takes `some`, and is still `it` | 21 | rum, milk, water, sand |
| 2 | its name takes `an` | 12 | an umbrella, an eyepatch, an egg |
| 3 | not known | 1 |  |
| 4 | its name takes no article | 32 | Giovanni, Lilliput, you |
| 5 | touched, or for a place visited | 0 | taking a thing sets it, and going into a room |
| 6 | a mark its own routine keeps, of something that has happened | 0 | tested 163 times and set 72, nearly all by an object's own routine |
| 7 | plural: `some`, and `they` | 67 | gnolls, manacles, knights |
| 8 | seen by the player | 16 | set 328 times, nearly all by objects' routines |
| 9 | not known | 131 |  |
| 10 | can be taken, and hasn't been: taking it can be special | 163 | taking a thing clears it; with it, a thing worn says `You'll have to take off` first |
| 11 | not listed among a room's contents | 350 | taking a thing clears it |
| 12 | alive: a person or an animal | 68 | `attack`, `bite`, `feed`, `greet` and `kick` test it |
| 13 | lit, or for a place has light | 76 | every room, and the torch |
| 14 | open | 52 | `close`, `empty` and `examine` test it |
| 15 | can be opened and closed | 12 | a window, a lid, a mailbox |
| 16 | things can be on it | 72 | tables, beds, counters |
| 17 | what's dropped in it lands outside it | 5 | `drop` tests it: a chair, a tree's branch, a bench |
| 18 | female | 10 | the witch, Lorealle, the goddess |
| 19 | can be eaten or drunk | 14 | `eat` and `bite` test it |
| 20 | a door | 4 | the four doors |
| 21 | a liquid | 7 | rum, water, the duck pond |
| 22 | a container | 60 | `empty`, `pour`, `look in` and `open` test it |
| 23 | burns | 7 | `burn` tests it: kindling, a newspaper |
| 24 | cuts | 3 | `cut` tests it of what is cut with: a pickaxe |
| 25 | can't answer: asleep, or dead | 0 | `greet` says it `isn't exactly in the best condition to exchange pleasantries` |
| 26 | can be taken | 69 | taking a thing sets it |
| 27 | cuts | 0 | `cut` tests it beside 24; nothing starts with it |
| 28 | can be read | 36 | `examine` tests it: notes, books, signs |
| 29 | locked | 9 | `open`, `close` and `unlock` test it |
| 30 | not known | 0 |  |
| 31 | a key | 4 | the four keys |
| 32 | got out of before you go anywhere | 2 | going says `[Getting out of it first]`: the raft, the captain's chair |
| 33 | can be seen through | 7 | cases, a cage, the hourglass |
| 34 | outdoors | 35 | 35 rooms: islands, the village green, the forest |
| 35 | turned on, or alight | 0 | `activate` sets it and `extinguish` clears it |
| 36 | can be turned on and off, or lit | 0 | `light`, `extinguish` and `activate` test it |
| 37 | can be got in or on | 9 | `climb in` and `sit` test it |
| 38 | not known | 1 |  |
| 39 | can be worn | 17 | a cloak, earmuffs, gloves |
| 40 | can be spoken to | 54 | `greet` tests it first: 54 of the 58 people and creatures |

Each name is from the objects that have the flag as the game starts and from the actions
that test it, with what those actions say when they do. Three were settled by the code.
The root's routine for taking a thing, at `05E3:0EC2`, clears 11 and 10 of it and sets 5,
26 and 8: no longer left out of a room's list, no longer untaken, touched, takeable and
seen. And a routine that runs before three of the actions says `You'll have to take off` of a
thing worn that has 10 or lacks 26.

Four aren't named. Flag 9 is on 70 of the 74 rooms and on scenery, and is set and cleared
by routines as things come and go: the dragon's parts, a sign, a bean in a cloak. Flag 3
is on the berries alone, 38 on the nectar machine alone, and 30 on nothing as the game
starts, tested 11 times by objects' routines in two overlays.

## How a string is named

As [*Spellcasting 301*](spellcasting-301.md) and *Gateway* name theirs, with nothing
changed: by a far pointer that is no pointer, whose segment is `F000` and whose offset is
the string's block times 1,024 and its place in the block. The routine most are handed
to, which prints, is at `1C8A:10F7`. The tools' `loads` finds them as it is.

| What | How many |
|---|---|
| Places that name a string | 4,984: 164 in the root and 4,820 in the overlays |
| Handed to the routine that prints | 3,445 |
| Handed to another routine | 159 |
| Kept in data, as an object's text is | 1,380 |
| Strings named, of the file's 4,923 | 4,870 |

Of the 53 no place names, 25 are all of block 22, which is the help: the keys and what
the mouse does, shown a screen at a time, so by its block and not string by string. The
other 28 are 17 in block 17 and a few in six others. *Spellcasting 301* has strings named
by a number worked out as the game runs, and these may be the same; they haven't been
looked at.

## The timers

There are 72, numbered, and found as in *Gateway*: the tools list each with the routine it
runs and the places that start it, stop it and ask about it. 71 are started somewhere the
tools read; the other, 60, is one of the few calls they don't.

### What each timer does

Each is named from what its routine prints when it runs:

| Timer | What it does | Timer | What it does |
|---|---|---|---|
| 1 | the farmer fires you | 37 | Morty's door opens |
| 2 | the farmer wants an answer | 38 | the receptionist speaks into her headset |
| 3 | a word forgotten | 39 | Clio comes back, and Morty finishes |
| 4 | Ed McDwarf gives back the sweepstakes notice | 40 | Morty shows you out |
| 5 | you tire of walking on your knees | 41 | the candles are put out |
| 6 | the beard begins to itch | 42 | the librarian fusses |
| 7 | the beard itches more | 43 | the library book disappears |
| 8 | you rip off the beard | 44 | the library book is missed |
| 9 | the wheel carries you round | 45 | the slimewig wriggles away |
| 10 | a gnu lumbers through | 46 | the slimewig kicks |
| 11 | the tavern's patrons and their Mead Lite | 47 | a salty burp |
| 12 | Bruce the waiter | 48 | the raft's voyage, turn by turn |
| 13 | the oaf's heads argue | 49 | the whirlpool |
| 14 | a raspberry at Blicester Castle | 50 | Gnoll Island's own |
| 15 | the turtles close in | 51 | the castaways of Milligan's Island talk |
| 16 | the man in the stocks, turn by turn | 52 | a bottle floats in |
| 17 | a boy and his father at the fair | 53 | the hourglass's sand runs |
| 18 | the storyteller's show | 54 | the wedding in the Great Hall, turn by turn |
| 19 | Lily's show | 55 | the makeup eats through the chain |
| 20 | the jugglers come on | 56 | the witch turns you inside out |
| 21 | the jugglers finish | 57 | the castle falls |
| 22 | Sir Pectoral's thugs catch you | 58 | the small voice hurries you |
| 23 | the elf's shell game | 59 | your squire calls you out |
| 24 | the musician wanders off | 60 | the Sergeant-At-Arms |
| 25 | the dragon flames, turn by turn | 61 | the knights' meeting |
| 26 | the hanky dries out | 62 | the bard, for a copper penny |
| 27 | the virgin stops crying | 63 | the bard, for a silver penny |
| 28 | a unicorn comes for you | 64 | the bard finishes his song |
| 29 | the priestess searches, and the sacrifice begins | 65 | the bard's song, which `listen` asks about |
| 30 | a scream from the sanctuary | 66 | the feasters' food fight |
| 31 | Molochi smokes and burns, turn by turn | 67 | the drunken knights fight |
| 32 | words from the sanctuary | 68 | the vanilla melts |
| 33 | the dragon's flame goes out | 69 | the delivery boy arrives |
| 34 | the escalator carries you up | 70 | the Shooting Gallery's own |
| 35 | a god greets you | 71 | something is tossed to the jugglers |
| 36 | the cork pops | 72 | the clock strikes noon, and the quest is over |

Timer 3 prints nothing: it clears one word of the data, and 21 places start it, most of them actions, with
a count of 1, so the word lasts a turn. Timers 50 and 70 print nothing either, and are
named for whose routines start and stop them. Timer 14 prints what someone says and plays
`RASP1.RS`. Timer 65's routine wasn't read to its end; `listen` asks whether it is going,
in the Village Square, with the bard's timers around it.

The last timer is a clock: a routine of the root's starts 72 with a count of 180, and when
it runs a small voice says the clock has struck noon and the quest is over. The same
routine starts four others, so it looks to be what begins a part of the game; that
hasn't been confirmed.

## A saved game

`RESTART.DAT` is 16,395 bytes, and is laid out as *Spellcasting 301*'s is:

| Bytes | What |
|---|---|
| 34 | That start it |
| 15,516 | The records of the 537 objects |
| 835 | 84 saved variables |
| 8 | Four words more |
| 2 | A count of timers, 0 as the game starts, and four bytes for each |

## Still to do

- Which screen display 4 is.
- The 28 strings no place names, outside the help.
- Flags 3, 9, 30 and 38.
- What timers 3, 50, 65 and 70 do, which their names only point at.
- Where a command is parsed and performed, for the player's `--watch`.
- The game hasn't been run on the PC.

# Timequest

Legend Entertainment, 1991. Bob Bates's game, and Legend's second, between
[*Spellcasting 101*](spellcasting-101.md) and
[*Spellcasting 201*](spellcasting-201.md) on the same system.

This is what the first look found: how much of it is the first game's, how much is already
the third's, and what is its own. What is known for certain is said plainly; what is
inferred says so. Addresses are `segment:offset` in hex, with segments relative to where the
program loads. Every one of them can be looked at with the [tools](../tools/README.md).

## How the program is built

As the first game is: **Microsoft C** in the large model, linked by **PLINK86**. `TQ.EXE` is
what the player runs, and `TIMEMAIN.EXE` is the game, with its overlays in `TIMEMAIN.OVL`.

| What | Where |
|---|---|
| The root | `0000` to `3646`: 222,304 bytes, with 6,761 relocations |
| The loader | `333D`, and the program starts in it, at `333D:04C0` |
| Its table | `33F0`, with 768 thunks after it |
| Overlays 1 to 18 | all loaded at `3646`, one at a time, from `TIMEMAIN.OVL` |
| Overlay 19 | `3C2B` to `51E7`, from `TIMEMAIN.EXE` itself, loaded at the start: the data |
| The data segment | `4333`, where Microsoft's notice is |

There is one area for the overlays' code where the first game had more, so only one
overlay of code is ever in memory.

## What is the first game's

These are read by what was written for *Spellcasting 101*, with nothing changed:

- **The text.** `TIMESTR.DAT` has 120 blocks and 1,475 strings, 202,598 bytes packed into
  101,330, by words and then by Huffman's codes, with 128 words and a tree of 210 nodes. A
  block is loaded into an array and a string is named by its place there. Most of the calls
  that load a block take its number from a variable here, where the first game's had
  constants.
- **The vocabulary.** `VOCAB.DAT` is plain, as the first game's is: 1,296 words.
- **The grammar.** 354 forms that make 165 actions, in the first game's narrower table.
- **The parser's own grammar**, with its rules still named.
- **The routines.** The switch that calls an action's routine and the eight that call an
  object's are found the same way: 165 actions, and 249 of the objects have a routine.
- **The names.** An object's two names are far pointers into the program, not places in a
  file. There is no `OBJECT.DAT`.
- **The flags.** A flag's number is its bit: there is no table of them. The numbers are
  [its own](#the-flags).
- **The pictures, their regions, and the sounds.**

## What is already the third game's

- **The exits.** A room no longer has a far pointer to its own rows. Every room's exits are
  in one table, and the room has the number of its first row and how many.
- **The AdLib music.** A song for the AdLib is a `.MDI`, a MIDI file that carries its own
  instruments, though the table at the start of a `.MUS` file is still the first game's ten
  bytes to an entry. A file's table has 120 entries, where the first game's has 30.

- **The timers.** They are numbers, in a table of four bytes each, where the first game's
  are far pointers to their routines. How they are called is [its own](#the-timers).

## The objects

518 of them, in the first game's eight types: 79 rooms, 127 things, 73 that are only words,
and so on. The table of types is at `29E5:0000`, three bytes to a type; the table of objects
at `29E5:0018`, six bytes to an object; their count at `4333:05DA`; and the records from
`2EE7:0000`.

Four types' records are two bytes bigger than the first game's, and a room's is the same
size with different things in it:

| Type | Bytes | Bytes of flags | What follows the flags and the two lists of words |
|---|---|---|---|
| 1, a room | 28 | 4 | `10` its list, `12` the points for coming to it, `14` what's also here, `16` what's seen from it, `18` how many exits, `1A` the first of them |
| 2, a thing | 36 | 6 | `12` its place, `14` relation, `16` next, `18` two lists, `1C` not known, `22` the points for taking it |
| 3 | 14 | 2 | nothing |
| 4 | 22 | 2 | `0E` two lists |
| 5 | 24 | 4 | `10` two lists |
| 6, a door | 28 | 4 | `10` two places, `14` two lists |
| 7, a person | 34 | 4 | `10` its place, `12` relation, `14` next, `16` three lists, `1C` not known |
| 8 | 24 | 4 | `10` its place, `12` relation, `14` next, `16` not known |

So the two bytes are flags: every type that had 16 of them has 32, but for the two that are
only words. A thing keeps its 48. The room paid for its own two by giving up the far pointer
to its exits for a word.

This is from the routines that read and write each field, which are in segment `01B9` from
`1B01` and switch on the object's type as the first game's do: the nouns at `1C24`, the
adjectives at `1CA8`, a place at `1D2C`, the relation at `1E71`, a list at `1F4D`, the next
at `2116`, and a room's four at `254F`, `25A6`, `25FD` and `2654`.

The words an object is called by are in two lists, the nouns at `29E5:0C3C` and the
adjectives at `29E5:18F8`. What's also in a room is at `29E5:1EE6`, and what's seen from it
at `4333:05DC`, both kept as the first game keeps them.

## The exits

294 rows of four bytes at `29E5:2352`: a direction, a kind, and a word. The routine that
follows one is at `0AAD:0442`, and it switches on the kind:

| Kind | Rows | The word is |
|---|---|---|
| 1 | 125 | the room it leads to |
| 2 | 5 | a door's row at `4333:05EC`: the room, and the door, which is passable when it has flag 11 |
| 3 | 16 | a row of far pointers at `29E5:28AF`: the text the exit says, and it leads nowhere |
| 4 | 121 | a routine's number, from 0 to 52: `0743:0006` is called with it, switches on it, and answers with a room |
| 5 | 27 | a row of eight bytes at `29E5:28EF`: a room, a variable in the data segment, and a far pointer to text |

Kind 5 is new, and *Spellcasting 201* doesn't have it. The exit leads to its room when the
byte the variable names isn't zero. When it is zero, the exit says its text, or `You can't go
that way.` if it has none. So a passage that opens during the game needs no routine: the
game sets a byte.

Kinds 1 to 4 are the first game's, with the far pointer each row had there replaced by a
number, which is how *Spellcasting 201* has them. That game dropped kind 3 as well, and
gives the text to a routine.

## A place and a time

The game is six places, each visited in up to nine times. Two bytes of the game's data say
where you are: the place at `006C`, from 0 to 5, or 6 for home, and the time at `006D`, from
0 to 8.

A table at `4333:07C2` has a word for each place in each time, six rows of nine: the number of
the block of `TIMESTR.DAT` with that visit's text, or `FFFF` where the place can't be visited
then. Each place's overlays load their block by it, into an array of their own, when you
arrive.

| Row | Place | Its overlays | Its blocks, by time |
|---|---|---|---|
| 0 | Baghdad | 17, 18 | 45, 45, 45, 42, 45, 43, 44, 45, 45 |
| 1 | Peking | 15, 16 | 19, 20, 21, 22, 23, 24, none, 25, 26 |
| 2 | Cairo | 13, 14 | 12, 13, none, 14, none, none, none, 15, 16 |
| 3 | Dover | 4, 5, 6 | 28, none, 30, none, 31, 32, 32, 33, 34 |
| 4 | Mexico | 12 | 36, 37, none, none, 38, 39, none, none, none |
| 5 | Rome | 7 to 11 | 49, 50, 51, 52, 53, 54, 55, 56, 57 |

The places' names here are from the text of their blocks and the game's own words for them.
Overlay 3 is home, and loads blocks 1, 7 and 8 by number. Overlay 11 also has the end of the
game, with block 9.

This is why most calls that load a block take its number from a variable, and why the tools
can't say which string a line of code prints without knowing the time.

## The timers

124 of them, by number. The table is at `3C2B:388A`, sixteen rows of four bytes: a timer's
number in a byte, and its count in a word two bytes on. How many are in it is at
`4333:527E`, which one is being run at `4333:5280`, and at `4333:527C` is a byte that makes
the next run skip the counting, as the first game has.

The routines are in segment `1101`, in the first game's order:

| Routine | What it does |
|---|---|
| `1101:0006` | empties the table |
| `1101:002B` | starts a timer, given its number and a count, or gives one that's going a new count |
| `1101:00A2` | stops one |
| `1101:0114` | says whether one is going |
| `1101:0151` | runs them, after a turn |

*Spellcasting 201* has a sixth, which gives a timer's count. It isn't here.

What is this game's own is how a timer's routine is called. The first game calls the far
pointer in the table, and the third has a table of far pointers, one for each number. Here
the routine that runs them calls `0C77:000C` with the number, and that is a `switch` of 124
cases, each a call to the timer's routine. All but four of the routines are in overlays.
Timers 46, 114 and 117 all call `1134:0AAF` in the root, and nothing starts them.

Of the 124, 119 are started, stopped or asked about by number in 315 calls, which is how the
tool finds who uses each. One is started to run every turn, timer 84. Two calls give a
number that is worked out.

### What each timer does

A timer's routine mostly prints, and what it prints names it. Few of the strings are in the
code: a routine prints the string at a place in an array, and the array is the block of text
[its place and time](#a-place-and-a-time) loaded. So a routine in the overlays of Rome that
prints string 31 prints one thing in 44 BC and another in 1798, and which it is has to be
judged: by who starts the timer, by the timers it starts, and by which blocks have a string
31 at all. A run of timers that print strings 28 to 33, one after another, is one scene, and
only one of Rome's blocks has those six strings as a scene.

So these names are less sure than the other games'. Those with a turn count are from the
count the timer gives itself again. Timers 24, 55, 113, 115 and 121 print nothing that was
found and are named from the objects they move. No call
starts timers 1, 12, 24, 25, 30, 55, 59, 62, 70, 98, 105, 112, 113 and 123 by number.
[Watching the game start](#the-game-as-it-runs) shows why for most of them: they are what
each visit opens with, and the game starts them itself, by a number it works out, before the
first turn.

| Timer | What it is | Timer | What it is |
|---|---|---|---|
| 1 | the sultan presents you to Yahya | 63 | the shout for Charlemagne |
| 2 | the sultan gives up on you | 64 | the pope sets out |
| 3 | the wives leave off | 65 | the pope asks for Attila |
| 4 | the wives go to bathe | 66 | a barbarian strides into the tent |
| 5 | the stolen veil is missed | 67 | Attila comes out of the tent |
| 6 | a rustling behind the wall | 68 | war cries outside the tent |
| 7 | the sultan is heard outside | 69 | Attila's patience ends |
| 8 | the sultan finds you under the bed | 70 | Pope Leo comes out of the Vatican |
| 9 | the sultan spares you | 71 | Pope Leo comes down the road |
| 10 | a soldier grabs you in the bazaar | 72 | the pope is shoved into the tent |
| 11 | the mule lowers his ears | 73 | Hitler and Mussolini at the radio, turn by turn |
| 12 | the soldiers search the House | 74 | Hitler gets the radio going |
| 13 | Vettenmyer bursts in | 75 | Hitler says to hop to it |
| 14 | Vettenmyer tells the key's secret | 76 | Hitler throws you out |
| 15 | footsteps on the stairs | 77 | Mussolini asks the word |
| 16 | another you comes in | 78 | Vettenmyer sneers |
| 17 | Vettenmyer grabs the laser | 79 | the old man stares |
| 18 | Vettenmyer fires at you | 80 | Vettenmyer grows agitated |
| 19 | Vettenmyer fires at the other you | 81 | Vettenmyer's look of hatred |
| 20 | Vettenmyer laughs last | 82 | Vettenmyer dies |
| 21 | Vettenmyer waits for the other you | 83 | the old man's last breath |
| 22 | the other you materializes | 84 | the ether evaporates |
| 23 | the other you stops the Doomsday Device | 85 | the surroundings melt away |
| 24 | Vettenmyer is met in Rome | 86 | the bracelet glows and goes |
| 25 | the riddle's word was never said | 87 | Vettenmyer steps out of the wall |
| 26 | Drake goes up to the queen | 88 | the general's orders |
| 27 | the strumpet is thrown out | 89 | the general says to stop wasting time |
| 28 | Churchill's broadcast | 90 | the Olmec sneaks a look |
| 29 | what's seen at the Tavern | 91 | the Olmec knocks you out |
| 30 | the courier calls for a horse | 92 | the Olmec's knife is sharp |
| 31 | the courier gallops past | 93 | the Olmec raises his knife |
| 32 | the courier is ambushed | 94 | the eclipse |
| 33 | the courier rides by | 95 | the eclipse ends |
| 34 | the outlaws melt into the forest | 96 | the Olmec throws his spear |
| 35 | the courier stirs | 97 | the spear hits |
| 36 | the courier comes to | 98 | a messenger to Montezuma |
| 37 | the cleric examines the letter | 99 | Montezuma and the mural |
| 38 | the courier reaches Runnymede | 100 | Montezuma's messenger returns |
| 39 | the cleric reads the letter to King John | 101 | the priest's sacrifice |
| 40 | an arrow from the trees | 102 | the priest sees an imposter |
| 41 | the Shed, turn by turn | 103 | the priest's knife |
| 42 | a hue and cry past the shed | 104 | the sacrificial priest comes in |
| 43 | the sky brightens | 105 | the mourners file into the cave |
| 44 | the sun comes up | 106 | the mourners tire of waiting |
| 45 | the pope's book, every ten turns | 107 | the mourners drink the poison |
| 46 | nothing | 108 | the Mongol leader leaves the tent |
| 47 | a man on a bench speaks | 109 | Genghis storms back |
| 48 | the focaccia man comes by | 110 | the women come back |
| 49 | the focaccia man wanders off | 111 | the funeral procession, every ten turns |
| 50 | the parade of lions begins | 112 | the barbarian army |
| 51 | the lion breaks away | 113 | the fortune cookie |
| 52 | the lion charges | 114 | nothing |
| 53 | the lion kills | 115 | the girl |
| 54 | the chariot race | 116 | Cleopatra's sedan chair, every hour |
| 55 | the Circus empties | 117 | nothing |
| 56 | Napoleon's aide argues | 118 | a word cleared |
| 57 | Napoleon is overheard | 119 | Caesar congratulates you |
| 58 | Napoleon dismisses the aide | 120 | a rock from the window |
| 59 | Napoleon decides | 121 | from the Royal Palace to the Royal compound |
| 60 | the hanky dries | 122 | Vettenmyer's welcome |
| 61 | the priest misses the crown | 123 | the vizier overthrows the sultan |
| 62 | the pope comes for the crown | 124 | Napoleon has you taken away |

Timer 29 prints the first string of the block when you're in the Tavern, which is a different
arrival in each time. Several can end the game, 41, 111 and 112 among them: they call the routine
at `0AAD:0C09`, which says `You have failed in your mission.`

### The clock

After the timers, the same routine counts the move, at `4333:0092`, and adds a
minute to a clock of four bytes: a day in a word, an hour, and a minute, which carry at 60
and at 24. Which clock depends on the two bytes at `4333:006C` and `006D`. When the
first is 6 it is the one at `4333:0082`. Otherwise it is in a table at `27B0:010C`, where the
first byte picks a row of 36 bytes and the second a clock of the nine in it. It is a clock for each
place in each of its times, with the one apart for home: the byte at `006C` is
[the place and the one at `006D` the time](#a-place-and-a-time).

A turn is a minute here, where it is five in both *Spellcasting* games.

## The flags

Three routines, one after another, do everything with them, as in the first game:
`01B9:1415` tests a flag of an object, `01B9:146E` sets it and `01B9:14C0` clears it. A flag
is the bit of its number, less one, counted from the start of the record's flags.

The same things are flags as in the two *Spellcasting* games, with numbers of their own.
Each name is from the code that tests the flag, or says what else it's from.

| Flag | What it is | From |
|---|---|---|
| 1 | takes `some`, and is still `it` | what has it: water, oil, blood, powder |
| 2 | touched; a place, visited | set by the routine that takes a thing, and by `open` |
| 3 | seen by the player | set by the routine that prints an object's name |
| 4 | left out of `all` | the routine that says what `all` can take turns it away first |
| 5 | its name takes no article | the routine that prints `the` |
| 6 | its name takes `an` | what has it |
| 7 | plural | what has it |
| 8 | not listed among a room's contents; a place, outdoors | cleared when a thing is taken; and the places that have it |
| 10 | a person, female | `turn on` of a person asks it |
| 11 | open; a person, someone other than you | `open` sets it and an exit by a door asks it; and everyone has it but you |
| 12 | can be taken, and hasn't been; a person, can't answer | see below |
| 14 | can be opened and closed | `open` and `close` |
| 15 | a container | `open` says what's found in one |
| 16 | burns | `burn` |
| 17 | a door | what has it: five doors and a gate |
| 18 | what's dropped in it lands outside it | `drop` asks it of where you are |
| 19 | can be eaten | `eat` |
| 21 | a liquid | what has it; nothing asks |
| 22, 25 | cuts | `cut` asks both |
| 23 | lit; a place, has light | set and cleared of a lighter and a cigar; six places lack it: a maze, a pit and passages |
| 24 | things can be on it | `drop on`, `pour`, `empty` |
| 26 | can be read | `examine` |
| 28 | a key | `open` and `unlock` ask it of what's used |
| 29 | can be turned on and off | `turn on` and `blow out` |
| 30 | can be taken | see below |
| 32 | can be seen into | `open` doesn't say what's found in one |
| 33 | an animal | the mule alone has it; `greet` and `feed` ask |
| 34 | turned on | `turn on` sets it, `blow out` clears it |
| 35 | can be got in or on | `climb in` |
| 36 | locked | `open` says it appears to be locked |
| 37 | can be worn | `put on` and `doff` |
| 38 | worn | the wreath has it; cleared of the costume |

Flags 33 and up are a thing's alone: no other type's record has room for them. Nothing
starts locked, and no call that names flag 36 sets it.

Two flags say a thing can be taken. Before `get`, the game asks for flag 30 or flag 12, and
refuses when the object has neither. The routine that takes a thing, at `01B9:15D7`, clears
flag 12 if it's there and sets flag 30. So 12 is a thing that hasn't been taken yet. 41
things start with it, an altar and a statue among them, and their own routines answer the
taking: it is the first game's "taking it is special", which the first taking ends.

The same routine gives the points for taking a thing, from the word at `22` in its record,
and zeroes the word so they are given once. A room's word at `12` is read by the same
field routine, at `01B9:22C8`, and is taken to be the points for coming to it. The things'
come to 170 points and the rooms' to 140, of the 1,000 the game has.

For a person, flag 12 is asked by `ask`, `tell`, `show`, `attack`, `call` and `wake`, and no
person starts with it. It is taken to be one who can't answer.

There is also a routine, at `0000:1797`, that picks a verb for an object: `Take` for a thing
with flag 30 or one of type 8 with flag 12, `Open` or `Close` for a container or a door that
can be, `Read`, `Eat`, `Greet` for a person, `Go to` for a place, and `Look at` for the rest.
It is what the mouse does with a click on a picture, by the look of it.

## Where a command is parsed and performed

The main loop, the parser's steps and the routine that performs an action are the first
game's, with everything in the root. These are the places the player watches:

| What | Where |
|---|---|
| The routine that performs an action, given the action and its two objects | `1134:0D40` |
| How many are being performed, one inside another | `4333:529E` |
| The action, a byte, and the direction after it | `3C2B:0054`, `0055` |
| Its object and its second object | `3C2B:3312`, `3C2B:0000` |
| The room, the actor, and the player | `4333:008A`, `008C`, `0094` |
| Where the main loop's `setjmp` returns | `1134:2466` |
| The grammar's forms, and the ways on to a second object | `2C82:00A5`, `2C82:0E79` |
| A token taken: the next, after a full stop, after `and` | `1408:00F7`, `0188`, `0206` |
| The word a token is | `3C2B:3314` |
| The form found, which one, and the action it makes | `1134:1EDD`, `3C2B:6458`, `3C2B:0052` |
| The objects found for a lot of phrases | `1134:0759` |
| The two lots, twelve bytes each, and how many | `3C2B:003A`, `3C2B:0036` |

The routine that performs an action asks the same ten routines in the same order as the
first game's: the room's, what the actor is in and the actor's, the one before the action's,
the second object's and the first's with what each is in, and the action's own. The ten
places where one has answered are at `1134:0F00`, `0F32`, `0F6D`, `0F8C`, `0FA9`, `0FE5`,
`1004`, `103D`, `1059` and `1073`. Going somewhere is action 144.

They were found by the shape of the code around each, which is the first game's nearly
instruction for instruction, and checked by playing.

## The game as it runs

The game starts and plays on [the PC](the-pc.md), and the player's `--watch` and `--panel`
follow it as they do the other two: the place and time, the clock, each command's words,
form and objects, each action and who answered it, the timers, and what changed.

```
world: home, 12:04 pm, turn 4, in Vettenmyer's quarters (482)
world:   words: open [verb] drawer [noun]
world:   form 183: open OBJECT, which is action 94
world:   object "drawer", looked for anywhere near: drawer (491)
world:   action 94: open drawer (491)
world:   drawer (491): flag 2, touched, set
world:   drawer (491): flag 3, seen, set
world:   drawer (491): flag 11, open, set
world:   card (498): flag 3, seen, set
world:     answered by the action's own routine: 1
```

Watching it showed two things reading it hadn't.

Before the first turn, the game goes through every place and time and sets each up. About a
hundred objects are moved in and out as it does, since a thing or a person can be in a place
in one time and not another: the table and the fire come to the Tavern and go again. It is
taken to be what flag 13 marks, an object that is put in its place when its time is set up,
which the routine at `0A12:000A` does for each that has it.

As it does that, it starts the timers each visit opens with, 24 of them: Churchill's
broadcast in 13 turns, the chariot race in 60, the vizier's overthrow of the sultan in 630.
That is more than the table of sixteen holds, so each place and time must keep its own, put
away and brought back with the visit. Where they are kept isn't found yet.

The names of the flags held up: `open` set flag 11, taking and opening set flag 2, going
somewhere set it for the place, and what was looked at had flag 3 set.

## Still to do

- Flags 9 and 20, what flag 10 is for a thing, and flag 13 for certain. The Tavern alone has
  9 and nothing asks about it. Nothing starts with 20, and the routine that matches a noun to
  an object asks it.
- Where each place and time keeps its timers while the player is somewhere else.
- A visit to another place and time hasn't been watched: the watch's place, year and clock
  there are checked only against the program's tables.
- A thing's word at `1C`, a person's at `1C`, and type 8's at `16`.
- The saved game's layout.

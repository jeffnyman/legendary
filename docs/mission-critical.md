# Mission Critical

Legend Entertainment, 1995. Written by Mike Verdu. It is another kind of game from the
ones before it: seen through the player's own eyes, walked through picture by picture, with
filmed scenes, and with a battle game inside it.

This is the start of the first look: what is in the game's folder and on its three CDs, what
of it the tools already read, how its program is built, and what it took to run. What is known for certain is said
plainly; what is inferred says so. The program is a 32-bit one, so an address is one number
in hex and not a `segment:offset`. Every one of them can be looked at with the
[tools](../tools/README.md).

## What the folder has

| File | What it is |
|---|---|
| `MISSION.EXE` | The game, with DOS/4GW bound in front of it |
| `DOS4GW.EXE` | DOS/4GW by itself, for `SETSOUND.EXE`, a 32-bit program too, which has only a small stub in front of it |
| `MCSTR.DAT` | The text |
| `OBJECT.DAT`, `RESTART.DAT` | The objects' names, and the state the game starts from |
| `MC001.HLP` | The game's guided tour, in [*Companions of Xanth*'s form](companions-of-xanth.md#the-guided-tours) |
| `MCnnn.PIC`, `.RGN` and `.FNT` files | 40 files of pictures, 15 of their regions, and 13 fonts |
| `FM.XMI`, `GM.XMI`, `SF.XMI` | The music, as the Miles drivers' extended MIDI, once each for three kinds of card |
| `.MDI` and `.DIG` files, `MDI.INI`, `DIG.INI` | The Miles drivers for music and for digital sound, and which of each is chosen |
| `BATTLE.DAT`, `CRUISE1.DAT`, `DRONE1.DAT` and eleven more | [The ships of the battle game](#the-battle-game), by the names the program opens them by |
| `FTLSTARS.DAT`, `FTLLINKS.DAT` | [The stars of the map](#the-battle-game), and which are joined |
| `CSP.DAT` | [Paths that read as the reactor's coolant](#the-coolants-paths) |
| `LEGEND.INI` | Where the game looks: `PICDATA` and `SAVEDATA` are `C:\MCRITIC\`, `VOCDATA` is `D:\MISSION\` and `QDATA` is `D:\MISSION\Q\` |

`OBJECT.DAT` and `RESTART.DAT` are here as in every game before, so the world is still
objects with records; and there is no `VOCAB.DAT`, as *Companions of Xanth* has none.

## The three CDs

The game came on three CDs, and plain ISO 9660 images of them are beside the game, in `cd`.
Each has a folder `MISSION`, and in it a folder `Q`.

| CD | In `MISSION` | Its speech | In `MISSION\Q` |
|---|---|---|---|
| 1 | 127 files | `MC001.VOC`, 7,569,408 bytes | 60 files, 354 megabytes |
| 2 | 86 files | `MC002.VOC`, 176,207,872 bytes | 354 files, 368 megabytes |
| 3 | 86 files | `MC003.VOC`, 133,937,152 bytes | 133 files, 273 megabytes |

The first CD's `MISSION` is the game's folder: the folder has all of it but the speech and
three lists of the installer's. The other two repeat its pictures, fonts, music and drivers,
and none of its data. Each CD has a speech file of its own, and each starts as
[*Companions of Xanth*'s](companions-of-xanth.md#the-speech) does, with a count of 1,297.
The `.Q` files are the filmed scenes: each has a short header and then a `RIFF` `WAVE`
sound. 89 of them are on all three CDs. Neither the speech nor the scenes are read yet.
The tools' `disc` lists what each CD has that the game's folder hasn't, and with `--copy`
puts it there, a file that's on more than one CD once; `LEGEND.INI` has no `GAMEDATA`, so
it takes the CD's folder from `VOCDATA`. Why that's enough is [below](#running-it).

The first CD also has the installer for Windows, a test of the video card, SciTech's
UniVBE 5.1, and a demo of *Shannara*.

## What the tools read as it is

| What | How much |
|---|---|
| The text | 38 blocks, 2,788 strings, 333,525 bytes packed into 167,191 |
| The regions | 15 files, 384 pictures, 2,296 regions, every one an outline |
| The guided tour | 2,391 events, 10 clicks |
| [The fonts](#the-fonts) | 13 |
| [The pictures](#the-pictures) | 40 files, 2,984 pictures |
| [The speech](#the-speech) | 3 files, 1,077 lines, two hours |
| [The objects](#the-objects) | 872, in six types |
| [The verbs](#the-verbs) | 58, each making an action of its own |
| [The music](#the-music) | 3 files, 38 songs in each and one more in one, 78 minutes |
| [The filmed scenes](#the-filmed-scenes) | 529 files, 41,068 frames, 70 minutes |
| [The battle game's ships](#the-battle-game) | 14, and 166 stars |

That is every file the game has.

## How the program is built

`MISSION.EXE` is 1,238,417 bytes, and the game is the last thing in it.

| At | What |
|---|---|
| `0x00000` | DOS/4GW's loader, a 16-bit MZ program of 62,580 bytes |
| `0x0f474` | Two more parts of DOS/4GW, each headed `BW` and saying at 1Ch where the next thing is |
| `0x352a4` | A small MZ stub, whose long at 3Ch says how far on the game's header is |
| `0x37cf4` | The header of a linear executable, `LE` |
| `0x69aa4` | The game's pages |

The linear executable is the form OS/2 and Windows' drivers used, and DOS/4GW loads it
into 32-bit protected mode, where there are no segments to speak of: one address reaches
everything. The header gives the program as objects, each a run of 4,096-byte pages that
expects to load at an address of its own. There are two.

| Object | Where | Size | In the file |
|---|---|---|---|
| 1, the code | `00010000` to `000b100d` | 659,469 bytes | 162 pages |
| 2, the data | `000c0000` to `00100a10` | 264,720 bytes | 35 pages, and zeros after them |

It starts at `0008811c`, with its stack at the data's end. There are no overlays: the whole
program is in memory at once, and that is the end of the loader, thunks and segment tables
that every earlier game needed read first.

After the header is a table of the 23,645 places in the program that hold an address,
4,602 of them pointing into the code and 19,043 into the data. Each says what it points to,
and the bytes in the file at that place are not the address: the table alone says what
goes there, so the tools put each one in as DOS/4GW does. The table is what the
relocations were to the 16-bit games, and better: where a relocation says only "a segment
is here", this says exactly which routine or which piece of data is meant. `refs` lists
the places that point to an address. One of them, at `0001fc6d`, points 51,768 bytes before
the data starts: an array the program counts from some number other than nothing.

The text just past the start says what made it: `WATCOM C/C++32 Run-Time system`, 1988 to
1994. Watcom's compiler passes a routine its first arguments in registers, EAX first, then
EDX, EBX and ECX, as this shows:

```
00016d62  mov eax, 0xc0594  ; 'RESTART.DAT'
00016d67  call 0x91550
```

The sound is the Miles Audio Interface Library, version 3.03 of June 1995, by `DIG.INI`'s
own first lines.

The data starts with names that no earlier game had in it: `Debris Cut`, `Bomb Armed`,
`Fixed Hull Leak`, `Spoke With Admiral`, then `Global Variables`, which reads as a list of
the story's variables under the names its writers gave them, kept for a debugging screen.
After them are the verbs, `Activate`, `Analyze`, `Ascend`, as *Companions of Xanth* has
them. Neither has been followed yet.

## The objects

The world is the earlier games' world of objects, and is kept as
[*Companions of Xanth*](companions-of-xanth.md) keeps its own. Its tables are not in the
program's data but in its code object, where Watcom puts what never changes; the records
they point to are in the data. Three tables stand one after another, at `0001a620`:

| Table | An entry | What |
|---|---|---|
| The types | 3 bytes, 6 of them | How many places an object of the type can be in, how many lists of other objects it can hold, and its record's size |
| The flags | 3 bytes, 20 of them | Which bit of a record's two bytes of flags, and a word with a bit for each type that has it |
| The objects | 5 bytes, 872 of them | An object's type, and the address of its record |

A word after them, at `0001b778`, is the count of objects, and they are numbered from 1.

| Type | Size | Places | Lists | How many | What it reads as |
|---|---|---|---|---|---|
| 1 | 31 | 0 | 1 | 200 | A room |
| 2 | 25 | 1 | 1 | 213 | A thing |
| 3 | 29 | 1 | 2 | 111 | A thing that holds others |
| 4 | 14 | 0 | 1 | 270 | A fixture, which is nowhere and so everywhere it's wanted |
| 5 | 27 | 1 | 3 | 2 | A person: you, and Tran |
| 6 | 2 | 0 | 0 | 76 | A routine's number and nothing else |

The names of the types are a reading of what has them, not the program's.

A record of types 1 to 5 starts with two words, where the object's name and its second
name start in `OBJECT.DAT`, counted from 1, and its two bytes of flags. What follows is the
type's own, and comes from the routines at `00013890` to `00014430`, one to get and one to
set each field, each with a table of where the field is for each type:

| Field | Room | Thing | Holder | Fixture | Person |
|---|---|---|---|---|---|
| The object it's in, then which of that one's lists, a byte, then the next on the list | | 6 | 6 | | 6 |
| The first object on each of its lists | 6 | 0Bh | 0Bh | 6 | 0Bh |
| The action a click does | | 13h | 17h | 8 | 15h |
| Where its list of actions starts | | 15h | 19h | 0Ah | 17h |
| Its routine's number | 18h | 17h | 1Bh | 0Ch | 19h |
| A room's four views | 8 | | | | |
| How many exits, a byte, and the first's row | 1Ch | | | | |

Some words between those are read and written by routines too, and what they are for
isn't known: at 0Dh and 0Fh in a thing, 0Fh, 11h and 13h in a holder, 11h in a person,
and one at 1Ah, 11h, 15h and 13h in a room, a thing, a holder and a person.

So object 1 is `you`, also `me`, a person in room 147, a Corridor, carrying the Captain's
note. A click on you is Talk to. The objects' routines are numbered to 734, as each
came to be written: yours is 1.

### The flags

A flag is a number from 1 to 20, and the table says which bit of a record it is and which
types have it, so that one bit serves two flags: bit 10 is flag 11 in a room or a thing and
flag 12 in a holder or a fixture. Three routines take an object and a flag's number, at
`00013a74` to test it, `00013aa4` to set it and `00013ad0` to clear it, and what the
program does with each flag is where its name comes from.

| Flag | What it is | How it's known |
|---|---|---|
| 1 | Takes `some` | The routine that writes a name asks it, with 2, 3 and 4: `power and data cables` |
| 2 | Its name takes no article | `you`, `Tran`, `Your Stateroom` |
| 3 | Plural | `consoles`, `system controls` |
| 4 | Its name takes `an` | Asked with those, though nothing starts with it |
| 5 | Touched; for a room, visited | Take and Open set it, and it is set on a room as you come into it |
| 6 | Seen | Set on a room with 5 as you come into it; the name is the earlier game's for the flag after `touched` |
| 7 | Not yet taken | The routine that puts a thing in your hands clears it, and sets 8, 5 and 6; 31 things start with it |
| 8 | Can be taken | Take asks it, and says `You can't take` a thing without it. Nothing starts with it: a thing is first taken by its own routine, and by Take after |
| 10 | Its routine's own mark | Tested 133 times, set 30 and cleared 8, each time on one object named by its number: the objects' routines keep their state in it, as [*Companions of Xanth*'s flag 10](companions-of-xanth.md#the-flags) is kept |
| 11 | Somewhere the player can be | The search for where you are goes up from you and stops at the first thing with it: every room, and two computer terminals |
| 12 | Can be opened and closed | Open asks it before it opens anything; cabinets, drawers, compartments |
| 13 | Can be seen through | Look in asks it of what isn't open |
| 14 | Unlocked | A stateroom door's routine asks for its code while the door hasn't it, and sets it once the code is given; a click on the safe is Open ... with until the safe has it |
| 15 | Things can be put in it | Put ... on wants what is put to have 7 or 8, and where it goes to have this or 18: a slot, a hole, a crater |
| 16 | Open | Open says `already open` when it's set, and sets it; Close clears it. Tested 371 times, most of them an object's routine asking about a door |
| 17 | A room | Every room starts with it, and nothing else can have it |
| 18 | Things can be on it | `You don't see anything on` it is said of what has it: chairs, beds, desks |
| 19 | Female | Picks `herself` from `himself`; Tran |
| 20 | A person | You and Tran: `Closing` one `would be more difficult than it sounds` |

One isn't known, flag 9. It starts on 96 objects, you and Tran and the ship's consoles
and systems among them. Give asks it of a person, and says of one who has it that they
`doesn't seem at all interested`; and the story clears it on Tran, when the autodoc's
override code is entered behind her back. Nothing else in the program names it. It may
be that a person with it is awake to what goes on, but the consoles have it too.

The first six and the last few are the flags *Companions of Xanth* has, some with the same
numbers. `flags` lists them, each with its bit and types, what starts with it, and how
often the program tests, sets and clears it.

### The rooms

The game is seen through your eyes, and a room is what is seen from one place, facing one
way or another. A room has up to four views, four bytes each: the direction faced, and a
picture, as the number of its `.PIC` file and its number in that. The Bridge Entrance
faces south with picture 2 of `MC002.PIC`, the door, and north with picture 4. There are
444 views in the 200 rooms.

A room's exits are rows of one table, ten bytes each, at `0001b77c`, the room holding how
many it has and its first row. A row is the direction, a kind, a word, and for some a
second word that reads as the direction faced on arriving. Kind 1 is a room, and 313 of the
439 exits are that; kind 4 is a routine, the other 126, which the routine at `00019370`
calls to say whether the way is open and where it goes. Northeast from the Bridge Entrance
is the Bridge. The directions are the earlier games' twelve: the compass's eight, up, down,
in and out.

The room you are in is the word at `000cc624`, and the actor the word at `000cc626`.

### The state it starts from

The routine at `0001dc7c` writes the state and reads it, and its list of what to write
accounts for `RESTART.DAT` to the byte:

| Bytes | What |
|---|---|
| 32 | That start it |
| 18,730 | The records of the 872 objects |
| 3,066 | 77 saved variables, a table of each one's address and size at `00016880` |
| 4 | A long at `000cc58c` |
| 48 | At `000e2b88` |

21,880 in all.

### The variables' names

Thirteen of the 77 variables have names that are the program's own. The data starts with
them, `Debris Cut`, `Bomb Armed`, `Spoke With Admiral`, and a table at `000cc518` pairs
fourteen addresses with fourteen names, for a screen headed `Global Variables` with `Set`
and `Reset` under it: what the game's writers used to put the story where they wanted
it. The screen is still in the program.

| Variable | Bytes | The program's name |
|---|---|---|
| `000d86bc` | 1 | Debris Cut |
| `000d91a8` | 1 | Transversed Reactor Maze |
| `000d9c06` | 2 | Bomb Armed |
| `000d7a98` | 1 | Fixed Hull Leak |
| `000d7a99` | 1 | Elevator Operational |
| `000d7a9a` | 1 | Sprayed Hull Puncture |
| `000d7a9b` | 1 | Glued Patched |
| `000d8340` | 1 | Pulled VR Cables Out |
| `000d88f6` | 1 | Wearing Fixed Space Suit |
| `000d9210` | 1 | Solved Reactor Coolant |
| `000d7965` | 1 | Passed Hype Checklist |
| `000d7aa0` | 1 | Spoke With Admiral |
| `000d7aa1` | 1 | Contact With Jericho |

The fourteenth, `Panning On` at `000cc704`, is a setting and isn't saved. Read in order,
the thirteen are the game's story: the debris cut away, the hull patched, glued and
sprayed, the elevator working, the reactor's maze and its coolant, the Admiral, the
*Jericho*.

Five more are known by what reads them:

| Variable | Bytes | What |
|---|---|---|
| `000cc624` | 2 | The room the player is in |
| `000d374a` | 545 | A byte of state for each [thing said back](#what-a-line-says) |
| `000d4a7e` | 524 | And for each thing the player can say |
| `000d564e` | 113 | And for each set of those |
| `000cc86c` | 1,524 | [The objects' lists of actions](#the-verbs), which being saved says the game changes what an object offers |

That is 18 of the 77; the other 59, most of them a byte or two, aren't known.
`objects --saved` lists them all.

`objects` lists them, shows one in full, and with `--tree` shows each room with what is in
it.

## The verbs

The verbs are a table of eleven bytes each at `000171e0`, 58 of them: the address of the
verb's name, of the word that goes between its two objects, or nothing, how many objects
it takes, and the number of the action it makes.

```
  1  Activate               takes an object    action 30
  2  Analyze ... with       takes two objects  action 54
 26  Look at                takes an object    action  1
 51  Take                   takes an object    action  2
 58  Wait                   takes nothing      action 11
```

After them are the actions' routines, six bytes each, an action's number and the address
of its routine: 61 of them. Every action a verb makes has one but Pour ... on, and four
have a routine and no verb: 10, 12, 14 and 15.

An object names actions, not verbs. It has the one a click on it does, which for 512 of
the objects is Look at, for 52 Open, and for 14 Use. And it has where its own list starts
in one table of words for every object, at `000cc86c`, each list ending in a zero: the
actions it offers when it's clicked. 343 objects have a list. This is the list the
[guided tour](#what-the-tools-read-as-it-is) shows: click the Emergency Stores Cabinet,
and its verbs appear.

`verbs` lists them, with each one's action and its routine, and how many objects have it
as a click's action or on their list.

## The pictures

The 40 `.PIC` files are none of the three kinds the earlier games have. Each starts with a
directory of 256 entries, twenty bytes each:

| At | What |
|---|---|
| 0 | Where the picture starts in the file, or 0 for none |
| 4 | How many bytes its pixels are in the file |
| 8 | Its kind: 0 a picture, 3 a change to one, 4 not yet understood |
| 9 | FFh when the picture starts with colors, 0 when it has none |
| 10 | Where it is drawn, across and down, when it is a piece of another |
| 14 | Its width and its height |
| 18 | A word not yet understood: 0 for most, and 2 or 3 for most of the changes |

2,984 entries have a picture, and 35 of those have no size. 528 start with colors: two
words, where among the 256 colors its own start and how many there are, both counted in
bytes at three a color, and then the colors, red, green and blue from 0 to 63. All but four
have the whole 256. A room's picture is 640 by 288, the letter-box the game looks through.

The pixels are a byte each. 36 pictures have them as they are; the rest are packed, and by
nothing of Legend's or PKWARE's this time: it is LHA's method `-lh4-`, Lempel and Ziv's
copying over a window of 4,096 bytes with the lengths and distances in Huffman's codes,
sent anew for each block. The program's routine for it is at `0009a524`, which hands out
4,096 bytes at a time from the decoder at `000a88f0`; that asks `000af860` for a byte or
a length and `000af914` for a distance. What gave it away is in those two: 510 codes in the
one table and 13 in the other, and a length that is the code less 253. Every packed
picture in the game unpacks to exactly its width times its height, using exactly the bytes
its entry says.

A picture of kind 3, and 1,959 are, is not pixels but a change to pixels. Each of its
bytes is exclusive-ored with the pixel already on the screen, so a 0 leaves the pixel as
it was and anything else turns one color into another. The second picture of
`MC002.PIC` is 222 by 210, at 209,78, and looks like noise; put over the first, the door
at the corridor's end, it is the door open and the bridge beyond it. A change is smaller
packed than what it changes to would be, wherever most of the picture stays.

Which picture a change is to, the file doesn't say: the program knows, in the routines
that draw them. The first guess here was the picture just before it in the file, and that
is wrong as often as right: in `MC003.PIC` five views come one after another, and the
eight changes after them belong to four of the five, not to the last.

But a change put over the wrong picture makes noise, and over the right one makes a
picture, and that can be measured: how little each pixel the change changes differs from
the one beside it, once changed. Over the right picture that comes to 10 to 50; over a
wrong one, 100 and more. Only the changed pixels are looked at, because the rest are the
picture's own and smooth whichever picture it is: counting them let noise in a corner
hide in a calm whole, and half of what that found was wrong.

So the tools go through a file's changes in order, keeping the picture as it stands,
since changes come in runs, each to what the last left. A change that makes noise of
that is tried over every whole picture of the game, its own file's and the 400 views of
the rest, and started again from the smoothest, if that one is under 60 and twice as
smooth as the next. The measure is the worst of eight bands down the change. A change too
small to judge, under 600 pixels, is taken to be to the picture in hand.

That finds a picture for 728 of the 1,942 changes that have pixels: 300 in their own
file, and 428 in another. The eight of `MC003.PIC` come out as clean pictures of a room
with a long table, from four sides. `MC012.PIC`, which is nothing but changes, 242 of
them, has 164 found, those looked at over views of `MC011.PIC`: lightning over a plain of standing
stones, each frame a change to the last. Its entries have a number in their last word
that reads as how long each is shown.

The other 1,214 are left as they are. For some no picture stands out; some are the later
frames of a run whose first frame wasn't found, so there was nothing to go on from; and
the measure asks a good deal, to keep from being wrong. Fourteen of the 728, picked by
chance, were all clean pictures.

The method is a reading of the pictures and not of the program, which has the answer in
the routines that draw them. It is slow, six minutes for the game, so `pictures` does it
when it writes the pictures and, for the listing, when asked with `--changes`: it then
says of each change which picture it was taken to be to, and in which file.

Kind 4 is 171 pictures, among them the mouse's pointers, the crew's manifest, and
a control panel with a border of plain blue. That reads as pictures with a color that
isn't drawn, but it hasn't been followed in the program.

## The fonts

The thirteen `.FNT` files are [*Spellcasting 301*'s kind](spellcasting-301.md#the-fonts),
with two things that game's don't have, and the reader stopped at both.

The first is in eight of them. A font whose letters differ in width had, in every earlier
file looked at, two tables of 128 bytes before its letters: the widths, and a second not
yet understood. These eight have the widths alone. The header says which: its bytes at 7
and 8 were read as one word, `FFFF` for letters that differ, and are two bytes. The one at
7 is the letters' width, or FFh when they differ; the one at 8 is FFh when the second
table is there. In the fonts without it that byte is 0, or 1 in two of them, `MC030.FNT`
and `MC031.FNT`, whose letters touch when set with nothing between; so the 1 may be the
gap, but that is a guess. Two of *Companions of Xanth*'s six fonts are of this kind too,
and are read now.

The second is `MC016.FNT`, which is in colors. The header's second byte, 1 everywhere
else, is 8: the bits in a pixel. A letter's row is 24 bytes, a byte a pixel, a color's
number, and 0 where there is none. After the letters are 32 bytes with a bit for each of
the 256 colors, set for five of them, 219 to 223, and then those five colors, grays from
dark to darker. The letters are drawn in those on a slab of color 254, which the font
doesn't bring: it is whatever the screen has there.

## The music

The music is no longer in `.MUS` files, and no longer in a form of Legend's at all. The
three `.XMI` files are the extended MIDI of the Miles drivers the game plays its sound
through: the same songs three times, arranged for three kinds of card. By their names
those are a card with an FM chip, `FM.XMI`; a General MIDI one, `GM.XMI`; and one that
plays SoundFonts, the Sound Blaster AWE32, `SF.XMI`. The program opens one of the three, in the
routine that sets up the music driver `MDI.INI` names; how it chooses isn't followed. Each has 38 songs, 78 minutes of them, and `GM.XMI` has a 39th.

A file is chunks as Electronic Arts' IFF has them, four letters and a length with its big
end first. `FORM XDIR` holds `INFO`, the count of songs, and `CAT XMID` holds a `FORM XMID`
for each. A song has `TIMB`, the instruments it wants loaded, a patch and its bank each,
and `EVNT`, its events.

The events are a MIDI track's, changed three ways:

- The time between events isn't a number before each one: it is bytes below 80h, added
  together, and none at all when events fall together.
- A note is one event and not two. After its key and how hard, it has how long it lasts,
  and nothing ever turns it off.
- Time isn't in beats. A tick is one 120th of a second, whatever tempo the song sets.

`music` lists the songs, each with its length and how many instruments it wants, and with
`--out` writes each as a Standard MIDI File: one track, every note turned off when its
time is up, at 120 ticks a second, with the song's own tempos left out as the drivers
leave them. Every song of the three files comes out a whole MIDI file, with no note left
on and none turned off that wasn't on.

33 of the 38 songs have the marks Miles added for a part to be played over, controllers
116 and 117. A file written here keeps the marks and plays the part once.

## The filmed scenes

The 529 `.Q` files in `Q` are the game's moving pictures, and the format is Legend's own:
the opening film, the people who speak to you on screens, and, the most of them, the walk
from one room to the next, a second or two each. 267 are forty frames or fewer. The
longest is `MC001.Q`, the opening, 5,955 frames, ten minutes.

A file starts with 26 bytes: `39 68`, a version, the picture's size in blocks, 160 by 72,
a block's size, 4 by 4, which makes 640 by 288, the count of frames, and at 16 how many
frames a second, 10. The files here are of six versions, 4.1 to 5.3, and what differs
between them is which kinds of chunk they use.

Then chunks to the end, each a word for its kind and a long for its size:

| Kind | What |
|---|---|
| 0 | Sound, a frame's worth, 4,410 bytes |
| 1 | The 256 colors, red, green and blue from 0 to 63 |
| 2 | A frame |
| 5 | A table of patterns, two bytes each, for the frames after it |
| 6 | A mark, with nothing after it whatever its size says: keep the picture that is there, for the frames to draw over |
| 7 | A mark, the same: from here a run is of whole blocks |
| 8 | The 44 bytes that start a WAVE file: 16 bits, one channel, 22,050 a second |
| 9 | A frame that is whole |
| 10 | Nothing: filling, so that every frame takes as long to read from a CD |
| 11 | A frame of changes |
| FFFFh | The end |

136 of the files have sound; the walks have none.

### A frame

The program's routine for a frame is at `0009ca50`, and it is not a compiler's work:
some six kilobytes of assembly written by hand, the loop unrolled into a copy for each
thing that can come before the next, so that it never has to ask. Read back into one loop,
it is this.

A frame is its blocks from the top left, a row at a time, each drawn over the frame
before. A block starts with a byte. Below F8h it is a color, and the next byte is a second:

- The same color twice is a block of that color, two bytes.
- Else a third byte is a pattern. With its top bit clear it is the number of a pattern in
  the table of chunk 5; with it set, it and a fourth byte are the pattern itself. A pattern
  is two bytes, the lower two rows and then the upper two, a bit a pixel from the top bit:
  the first color where the bit is clear, the second where it's set.

From F8h it is a code:

| Code | Then | What |
|---|---|---|
| F8 | | Over the kept picture, three ways: F8 again is its block here; FB and sixteen bytes are pixels, with F8 for one where the kept picture shows; any other byte is a color and two bytes of pattern, the kept picture showing where a bit is set |
| F9 | A count | Again, that many times, what was just done |
| FA | A word | The block of that number, as it is now |
| FB | A count | A run of that many blocks, of the kind just done |
| FC | A byte | The block some way off, by a table of 256 ways in the program, at `000e17b0` |
| FD | A byte | The block that many and one more after this one |
| FE | A byte | The block that many and one more before |
| FF | | Nothing: the block stays as it was |

The blocks after this one still hold the frame before, so FD copies from that frame and FE
from this one: movement costs two bytes a block wherever it lands.

What F9 and FB do is by what came before them, which is why the routine is so many
copies of itself. After a copy, F9 is more blocks copied the same way, and FB is that
many more, each with its own byte, or word after FA, for where from. After a block of one
color, FB is that many colors. After a block of two, FB is that many patterns in the same
two colors. Otherwise F9 is the block before, again, or after FF more blocks left alone;
and FB is whole blocks, sixteen bytes each. In a frame of changes, and in any frame after
a mark of kind 7, FB is whole blocks whatever came before.

And a whole block isn't always sixteen pixels. One that starts with a byte from F9h to FEh
is a block of three to eight colors: the bits that say which color each pixel is, two each
in four bytes for three or four colors, three each in six bytes for five to eight, and
then the colors. So a block is two, three, four, eight, nine, twelve to fifteen or
sixteen bytes, as its picture needs.

The kept picture is not in the file. A scene is played over what the game has on the
screen, the room's picture from a `.PIC` file, and nine scenes keep that to draw over: in
`MC651.Q` a sky moves behind a tower and the ground it stands on, and the file has only
the sky. The tools have no picture to keep, so there the tower is black.

The tools' `scenes` lists them, and with `--out` writes each as an animated PNG, which a
browser plays, and its sound as a WAV file. Every frame of every file is drawn from
exactly the bytes its chunk has, with one exception that is the game's own: a frame of
`MC124.Q` stops two blocks short, and the program draws the last two from the start of
the chunk after it, as the tools do.

## The battle game

Inside the game is another, a battle between ships, drawn as lines. Each kind of ship has
a file, fourteen of them: a battleship, three cruisers, a destroyer, a frigate, a corvette,
a science ship, five drones and a missile.

A file is the ship as lines in space, twice: once in full, and once with fewer lines, for
when it is far off. It starts with four words, how many points and how many lines the
first has, and how many the second. Then the first's points, three signed words each with
the ship's middle at 0; its lines, two words each, the numbers of the two points a line
joins; and the second's the same way. Every one of the fourteen is exactly as long as its
four counts make it, and no line joins a point its ship hasn't got.

| File | In full | Far off | Its size |
|---|---|---|---|
| `BATTLE.DAT` | 414 points, 614 lines | 150 and 203 | 375 long, 114 wide, 130 high |
| `CRUISE2.DAT` | 366, 570 | 82 and 91 | 300 by 40 by 102 |
| `FRIGATE.DAT` | 206, 266 | 46 and 59 | 122 by 48 by 50 |
| `DRONE1.DAT` | 66, 77 | 18 and 21 | 52 by 34 by 14 |
| `MISSILE.DAT` | 17, 24 | 17 and 12 | 35 by 8 by 8 |

`ships` lists them all, and with `--out` draws each as a PNG file: the ship in full and
the ship far off, each from above, from the side and from ahead.

`FTLSTARS.DAT` is the map a ship is flown across: 166 stars, a hundred bytes each. A star
has its name, its right ascension and declination as text, the same two as floats in
degrees with its distance in light years, what kind of star it is as astronomers write
it, the distance and the two angles again in radians, and a long that goes by the kind,
176 for a G and 272 for an M. They are real stars, Sol first, then Alpha Centauri at 4.3
light years and Barnard's Star at 6, out to a hundred light years. `stars` lists them.

`FTLLINKS.DAT` is which of them a ship can go between: 59 rows of 47 bytes, the first
not a star. A row is eleven longs and three bytes of nothing: a number of the star's own,
from 10 to 978; which star it is, by its place in `FTLSTARS.DAT`; a number from 24 to 28;
a 0, 1 or 2; and up to seven rows it is joined to. 58 of the 166 stars are joined, by 80
links, and every link is in both of the rows it joins. Sol is joined to Epsilon Indi
alone, and Barnard's Star to five. Two rows are places that aren't among the stars, -2
joined to Deneb Kaitos and -3 to 70 Ophiuchi. What the other three numbers are isn't
known; the 0, 1 or 2 may be whose the star is. `stars` lists the joins under each star.

### The coolant's paths

`CSP.DAT` is 77 paths across a picture. It starts with 77 longs, where each path starts,
and a path is steps, a byte and its words:

| Step | Then | What |
|---|---|---|
| 2 | A word | Go on to that path. Path 74 is this and nothing else, to itself |
| 3 | Two words | Be at a place, across and down |
| 4 | Two words | Go to a place in a straight line |
| 5 | Two words | Not known: 13 and 13 for most |
| 6 | A word | Wait that long |
| 7 | Two words | Where something is set a way, 0 to 6, go on to that path |
| 0, 1 | | Not known |

Every line whose start its own path gives, 176 of the 252, goes straight up, down or
across, and some paths go back and forth a dozen
times in a narrow place, as a coil does. The routine that loads the file, at `00082b50`,
is in the part of the program that has `WARNING: System breach` and `- Recalculating -`,
beside the names `Engine Coolant Grid` and `Automatic Valve`, and among the story's
variables is `Solved Reactor Coolant`. So this reads as the pipes of the reactor's coolant,
with step 7 a valve and which way it is turned: a reading, not followed in the program.
`coolant` lists each path's steps.

## The speech

Each CD has a file of speech, and `disc --copy` puts all three in the game's folder. They
start as [*Companions of Xanth*'s file](companions-of-xanth.md#the-speech) does: a count,
1,297 in each, and that many offsets, the last the file's length; so 1,296 numbers for
lines, the same numbers in all three files. A file has the lines its CD's part of the game
speaks, and the rest are empty there.

| File | Lines | How long |
|---|---|---|
| `MC001.VOC` | 66 | 3 minutes |
| `MC002.VOC` | 724 | 66 minutes |
| `MC003.VOC` | 408 | 50 minutes |

Thirteen lines are in the first file and in each of the others, and 108 in both of those;
a line in two files is the same size in each. Between them the three have 1,077 of the
1,296, and 219 numbers have no line on any CD.

What a line is has changed. In *Companions of Xanth* it is samples and nothing else, with
nothing to say how fast they go. Here a line is a whole RIFF WAVE file, the kind any
program of the day could play, and says for itself: one channel, 16 bits, 22,050 samples a
second. A line starts at a multiple of 2,048, a CD's sector, and zeros fill it out after
its WAVE ends. The tools' `sounds` counts them and, with `--out`, writes each as it is.

### What a line says

Which text a line speaks is in the program, not the file, as it is in *Companions of
Xanth*, and the two games keep their talk much the same way. The routine at `000847d0`
speaks a line by its number, and `00084200` looks the number up in the file's offsets.

*Mission Critical* speaks both sides of a conversation. What is said back to the player is
a table at `000d396c`, 545 records of eight bytes: the second word is a text's number and
the third leads on to what can be said next. A record's place in the table is the number
of its sound, so lines 0 to 544 are these, and 363 of them have a sound.

What the player can say is in sets. A set is five bytes at `000d56c0`: how many things,
where the first is in a table of four bytes a thing at `000d4c8a`, and a routine's number.
A thing is a text's number and where it leads: to a record of the first table, or, from
8000h, to another set. There are 113 sets. The player's own lines are spoken, which
*Companions of Xanth*'s are not, and a table at `000d6302` says which sound each is: 412
entries of five bytes, the set as 8000h and its number, which thing of the set, and the
line. Those are lines 567 to 977, and 385 have a sound.

A text's number is a place in a table of four bytes at `000d2688`. An entry there is F0h in
its top byte with a block of `MCSTR.DAT` and a string of it below, here block 0;
or, for a few, the address of text in the program. From 8000h a number is a routine that
makes the text, as it is in the earlier game.

So line 0 is the door of a stateroom that isn't yours, `This stateroom is protected by
the Privacy Laws`; line 577 is the player asking `What is Damage Control Condition Zebra?`;
and line 17 is the answer, `Condition Zebra is the most serious of the four damage
containment and control procedures`. How long each text is goes with how long its line
lasts: over the 363 replies the two correlate at 0.99.

The other lines, 545 to 566 and 978 up, are named in the code, and they are not all
speech. The files of speech hold the game's sounds too, an alarm, a door, a hum, and the
program starts those by other routines of the same part of it, `00084a50` to `00084e80`,
which take a line's number as `000847d0` does. So what the tools can say of one of these
lines is what the program does with it:

- **A sound.** 56 lines are handed to the routines that start, stop and ask after a sound.
  Forty of them are under three seconds.
- **Spoken, with what the game says as it speaks.** Where the program shows a text and
  speaks a line straight after, the text's number is in the code just before the line's.
  Five lines have one. It is what is on the screen while the line plays, which is the
  line's own words for `Deck Two is under Damage Control Condition Zebra`, and only the
  story around them for `You manipulate the controls on the transmitter until you can hear
  the message`: the message itself is 42 seconds of speech, and that text isn't it.
- **Spoken, with nothing shown.** 20 lines are handed to `000847d0` by their numbers and
  no text is near.
- **The ship's briefing.** A table at `000dcc54` has eight bytes a part: the line that
  tells it, a picture, and the address of the part's title. 13 lines, `Propulsion`,
  `Pulsedrive Engines`, `Nuclear Reactor`, `Habitat Module`: the tour of the *Lexington*.

| Lines | What | Have a sound | Placed |
|---|---|---|---|
| 0 to 544 | What is said back | 363 | 363, with their text |
| 545 to 566 | Named in the code | 17 | 1 |
| 567 to 977 | What the player says | 385 | 385, with their text |
| 978 to 1,295 | Named in the code | 312 | 105 |

One more kind is a run the program readies one after another, to play one of them at
random: lines 1,098 to 1,109 are the clicks of the Geiger counter, object 365, and which
is played goes by a number the game keeps that reads as how strong the radiation is.

So 748 of the 1,077 have their text, and 106 more are known for what they are. The 223
left aren't handed over as a number the code spells out: they come from tables the game
fills as it goes, and reading those in the program is slow work for what it gives. The
watch says them instead, as the game plays them: each line the routine at `000847d0`
speaks and each sound the routine at `00084b30` starts, by its number. In the first
corridor, before anything is clicked, the game plays four, lines 1,160, 1,068, 1,075 and
1,037, three of which nothing in the code names: the corridor's own sounds, by when they come. 147 of them are under three seconds, which reads as
more sounds; the other 88 are 22 minutes between them. They come in runs, 996 to 1,032 and
1,234 to 1,270 the longest. `speech` lists every line with what is known of it, and
`--watch` says each as it is played.

## Running it

[The PC](the-pc.md) came with a 386's protected mode and a DPMI host that none of the
earlier games use, and DOS/4GW runs on them as it is: it prints its banner, version 1.97,
asks the host for memory, and loads the game's two objects into it. The game then asked
six things the PC didn't have.

**The 8087.** Watcom's startup code, at `000ad3e0`, finds no coprocessor and does this:

```
000ad3e0  mov eax, cr0
000ad3e3  or al, 4
000ad3e7  mov cr0, eax
```

That turns on the processor's flag for emulating one, so that each floating-point
instruction comes to the library's own routines as exception 7. A program under a DPMI host
hasn't the privilege to touch that register, and the host gave it a general protection
fault, which DOS/4GW printed with its registers before ending. Now the host does the two
moves for the program, letting it change only the two bits about a coprocessor, and the
processor raises exception 7 at a coprocessor's instruction when the flag is set. The first
one comes at once, at `0009f203`, and the library's handler takes it.

**The BIOS's data.** At `0009fc97` the game loads 40h into DS and reads the long at 63h: the
video card's port, from the BIOS's variables. In real mode 40h is the segment they're in,
and DOS extenders gave their programs a selector 40h that reaches the same place. The host
has one now.

**The CD.** The routine at `0008e580` looks for `MC001.VOC`, then `MC002.VOC`, then
`MC003.VOC`, where `VOCDATA` says, and takes the first it finds as the CD that's in the
drive; given a CD's number, it looks for that one's file first. That is all a CD is to the
game, so a folder with all three files in it is all three CDs at once, and nothing will ever
ask for another. The tools' `disc --copy` makes that folder: it copies what each CD has
that the game's folder hasn't, 535 files, three of speech and 529 scenes in `Q`, about
1.3 gigabytes. The PC then serves the game's folder as `D:\MISSION`, as it serves
*Gateway II*'s as `D:\GW2`.

**The video mode.** With those, the game reads `LEGEND.INI`, `DIG.INI` and `MDI.INI`,
loads the Miles drivers they name, which are 16-bit code it calls through INT 66h, starts
the Sound Blaster at 22,050 samples a second in stereo, prints `init_music()` and a row of
dots, and asks the video BIOS for VESA's mode 101h: 640 by 480 in 256 colors, which a VGA
hasn't the memory for. Without it the game said so and ended:

```
ERROR: Unable to establish VESA graphics mode 101h.  Please load the VESA
       driver for your graphics card or try installing UniVBE 5.1
```

It asks three things, by INT 10h function 4Fh: what the card is (00h), for the mode to be
set (02h), and what the mode is like (01h). The PC's card now has a megabyte of memory
past the VGA's, the modes 100h, 101h, 103h and 105h, and VESA 1.2's functions for them. A
picture of 640 by 480 is 300 KB, and the processor's window on video memory at `A0000h`
is 64: a program moves the window, by function 05h or by calling a routine the mode's
description names, and the card has both.

**The bus.** That was at eight million instructions a second. At the thirty million the
player first gave it, the game ended before any of it, with
`ERROR: Music driver installation failed.` and exit code 20. Miles's driver for the FM
chip, `SBPRO2.MDI`, finds the chip the usual way: it starts the chip's timer, which runs out
in 80 microseconds, waits, and looks for the timer's bit in the status. Its wait is a count
of reads of the status port. On a real card each read took a microsecond or so, because
the card is on the ISA bus, which goes at its own pace whatever the processor's; on the PC
a read took one instruction's time, and past sixteen million a second the reads were over
before the timer was. Now a use of the FM chip's ports takes a microsecond at the least.
The player now runs the game at sixteen million, for a reason of its own: at thirty the PC
only just keeps up with the clock on the machine it was tried on, and the sound crackled
when it fell behind. It also gives this game a disk of 1.5 megabytes a second, where the older games
have 200 KB: at that, the program alone took six seconds to load.

**A held sample.** While the game printed `init_music()` and its dots, the speakers made
odd sounds. Miles's driver for the Sound Blaster finds the card's interrupt by playing one
sample, 16 bits, of whatever is in memory where its buffer will be, and waiting for the
interrupt that ends it. The sample it happened on was 7F80h, nearly the top of the swing.
A real card's converter holds its last sample too, but its output goes through a
capacitor, which passes no steady level: there the sample is a click. The PC's card held
it, full over, for the second and a half until the film's sound began: a thump going
in and another coming out, with nothing else playing between. Now a block's last sample is held for a
fiftieth of a second, long enough for a program that starts its next block from the
interrupt, and then dies away.

With that the game runs. It plays its opening film, the planet and the ship and Legend's
name, with sound; Escape goes past each part; it writes `RESTART.DAT`, tells how the
Captain left you on the deck, and then:

```
You regain consciousness...
```

over the first corridor of the *Lexington*, with the row of `Help`, `Volume`, `Undo`,
`Save`, `Restore`, `Restart`, `Quit` and `Map` above the picture and the inventory below
it. That is as far as it has been taken: nothing has been clicked.

## The game as it runs

The player's `--watch` and `--panel` show this game's world as they show the others',
read from the game's memory as it plays. Two things about a 32-bit program made that
new.

The earlier games are found in memory by Microsoft C's notice at the start of their data
segment, below 640 KB, and everything else is so many segments from there. This program
is loaded above the first megabyte, its two objects wherever DOS/4GW found room, a page at
a time. So the watch looks for the table of types, eighteen bytes that never change, at
each page of memory, as far into the page as the table is into one of the program's own.
That places the code. The table of objects holds the address of each record, already
memory's own, and the first of them places the data.

And the routine that performs an action, at `00015210`, isn't called with its arguments on
the stack: Watcom's compiler passes them in registers, the action in EAX and its two
objects in EDX and EBX. It is otherwise the routine every game before has, down to the
warning it keeps for an action that performs itself four times over. It counts how deep it
is at `000cc5c0`, keeps the action at `000e37aa` and its objects at `000e37b2` and
`000e37b0`, and asks the room's routine and the actor's before the action's own.

As the game starts, after its film:

```
world: the game is in memory, its tables at 00208620h, with 872 objects
world: in Corridor (147)
world:   here: you (1) [Captain's note (148)]
world:   exits: north to Corridor (136); south to Corridor (145); east to Corridor (149)
```

and with a click on the corridor ahead, as the game's own tour starts:

```
world:   action 15: Walk on a routine alone (65)
world:   you (1) moved: from Corridor (147) to Corridor (136)
world: in Corridor (136)
world:   here: you (1) [Captain's note (148)], Emergency Stores Cabinet (137) [molecular glue (142), ...
```

Action 15 is one of the four that no verb makes, and this is what it is seen to do: it is
named `Walk on` for that and nothing else. Its object is one of the 76 that are a routine's
number alone. Coming into a room sets two of [its flags](#the-flags): it is visited, and
seen.

### A click, and whose routine answers

A click comes to the routine at `00011df0`, which reads the pointer from `000e2b50`, across
and then down, and the button from `000e2b54`, and hands a command to the routine at
`00017690`: the verb in EAX, or 0 for the action the object gives itself, and the two
objects in EDX and EBX. Within sixteen pixels of either side of the picture the command
is action 14 with -1 or 1, which by its place reads as turning. That routine puts the
command's words together and sets it, at `000e37a8`, `000e37a6` and `000e37a4`, for the
turn to perform. The watch says each command a click makes there,
once, though the game takes the same click on every pass of its loop while the button is
down.

The routine that performs an action asks other routines first, in the order every game
before asks them: the room's; the player's own, when someone else is acting; what the
actor is in; the actor's; the routine before the action's, at `000175dc`; what the second
object is in, and the second object; what the object is in, and the object. Each is called
at `0001c8f4` with the object in EAX and why it is asked in EDX, numbered as *Companions of
Xanth*'s are, and the first to answer with something other than 0 ends it. Last is the
action's own, which `000175f8` finds in a table at `00017460`. The watch is told at the
instruction after each call, where the answer is in EAX; the object asked about isn't on
the stack, as it is in the 16-bit games, so it is read from the word the call took it from.

So the tour's first click, and a click on the ceiling of the room it leads to:

```
world:   a click at 318,212 makes a command: its own action, on a routine alone (65)
world:   action 15: Walk on a routine alone (65)
world:   you (1) moved: from Corridor (147) to Corridor (136)
world:     answered by a routine alone (65), asked as the object: 1
world:   a click at 303,55 makes a command: its own action, on Corridor (136)
world:   a click at 31,38 makes a command: its own action, on Corridor (136)
world:   action 1: Look at Corridor (136)
world:     answered by the action's own routine: 1
```

The walk is the object's own routine's work: the routine alone, number 65, answers for
itself. The click on the ceiling makes the room the command's
object, and the next click performs it.

What the watch doesn't have for this game: a count of turns or a time of day, and the
timers.

## Still to do

Everything but the above: the picture each of 1,214 changes is to, and kind 4; what
223 of the lines in the files of speech are, which the watch says as they're played; what an action's routine and an object's do, and the
fields of a record not yet named; one of the flags, and 59 of the saved variables; the battle
game's rules; and playing it through.

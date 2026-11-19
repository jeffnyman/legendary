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
| `BATTLE.DAT`, `CRUISE1.DAT`, `DRONE1.DAT` and fourteen more | The ships of the battle game, by the names the program opens them by |
| `FTLSTARS.DAT`, `FTLLINKS.DAT`, `CSP.DAT` | Stars and the links between them, and one more not looked at |
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
| The fonts | 5 of the 13 |

And what they don't: the pictures, whose files don't start as the earlier games' do; eight
of the fonts; the music, which is no longer in `.MUS` files; the speech and the filmed
scenes; and the objects, whose table has to be found in the program first.

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

## Running it

[The PC](the-pc.md) came with a 386's protected mode and a DPMI host that none of the
earlier games use, and DOS/4GW runs on them as it is: it prints its banner, version 1.97,
asks the host for memory, and loads the game's two objects into it. The game then asked
five things the PC didn't have.

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
player gives a game of 1995, the game ended before any of it, with
`ERROR: Music driver installation failed.` and exit code 20. Miles's driver for the FM
chip, `SBPRO2.MDI`, finds the chip the usual way: it starts the chip's timer, which runs out
in 80 microseconds, waits, and looks for the timer's bit in the status. Its wait is a count
of reads of the status port. On a real card each read took a microsecond or so, because
the card is on the ISA bus, which goes at its own pace whatever the processor's; on the PC
a read took one instruction's time, and past sixteen million a second the reads were over
before the timer was. Now a use of the FM chip's ports takes a microsecond at the least.
The player also gives this game a disk of 1.5 megabytes a second, where the older games
have 200 KB: at that, the program alone took six seconds to load.

With that the game runs. It plays its opening film, the planet and the ship and Legend's
name, with sound; Escape goes past each part; it writes `RESTART.DAT`, tells how the
Captain left you on the deck, and then:

```
You regain consciousness...
```

over the first corridor of the *Lexington*, with the row of `Help`, `Volume`, `Undo`,
`Save`, `Restore`, `Restart`, `Quit` and `Map` above the picture and the inventory below
it. That is as far as it has been taken: nothing has been clicked.

## Still to do

Everything but the above: the pictures and the other fonts, the music, the speech and its
text, the filmed scenes, the objects and the verbs, the variables' names, the battle game,
and playing it past its first picture.

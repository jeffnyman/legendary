# The PC

The games are PC programs, and Legendary has a PC for them to run on: `src/Legendary.Pc`, an
x86 processor, the EGA, VGA and CGA, the timer and interrupt hardware, the speaker, FM and
MT-32 sound, and a BIOS and DOS answered in C#. It has no window of its own:
`src/Legendary.Player` gives it one, with the keyboard, the mouse and sound, and the tests run
it with none. Besides playing the games, it is a place to run one where it can be watched, so
that what these notes say a program does can be checked against what it does.

It began as a copy of the core of [Excavate](https://github.com/jeffnyman/excavate), which
plays other DOS games, and Excavate's began as a copy of Confederation's. It is copied whole.
Most of it is more than these games need, the 386's protected mode for one, but it is one
machine and comes apart badly. Where it came from before that is in [NOTICE.md](../NOTICE.md).

A machine of this kind answers what its games ask and stops at anything else, so each new game
finds what the others didn't need. *Spellcasting 101* found four:

| What the game did | What the PC now does |
|---|---|
| Asked the video BIOS for the VGA's state, INT 10h function 1Bh, to see whether there is a VGA and what it can do | Gives the state as a VGA has it for the mode it's in, with the table of the modes its BIOS can set |
| Asked whether Microsoft's Hercules driver is loaded, INT 10h function EFh | Leaves the registers as they came, which says no |
| Had DOS parse a file's name into a file control block, INT 21h function 29h, as Microsoft C does for a program it starts | Parses it |
| Needed AdLib's driver in memory before it, for its music | Loads `.COM` programs, lets a program stay resident, and runs one program after another |
| Counted, without knowing it, on a file's taking time to read | Can give its disk a speed |
| Freed its memory by hand, through the list of lists, and had DOS load the game over it as an overlay, INT 21h function 4Bh with 3 | Gives the first memory block's segment before the list of lists, joins free blocks that are side by side before it resizes one, and loads overlays |

Music asked for more. The game's FM music is played by AdLib's own driver, `SOUND.COM`, a
program that is run before the game, takes an interrupt, and stays in memory when it ends.
For that the PC learned three things: to load a `.COM` program, which is a file with no header
loaded as it is; to let a program end and stay, by INT 27h or INT 21h function 31h, and to
load the next one above it and not over it; and to run a list of commands one after another,
as a batch file would, without there being a file. The player gives it the list: the driver,
when the game's folder has it, and then the game.

With those, the launcher draws its title, the same pixel for pixel as Legendary's own reader
of the picture file makes it, and the game starts, asks whether to restore a saved game, and
plays. The tests in `tests/Legendary.Pc.Tests` do all of it.

## A laugh and a disk

Joey Rottenwood laughs in the game's first scene, and on this PC he didn't. The file with the
laugh was opened, read, and played for less than a tenth of a second.

The game stops a recorded sound [at any interrupt from the keyboard](spellcasting-101.md#the-sounds),
a key let go as well as a key pressed, and the laugh comes straight after a key: the scene
stops at a prompt for more, the player presses a key, and the game reads `ROTTEN3.RS` and
plays it. On a PC of 1990 the file was a fraction of a second coming off the disk. By then the
key was up, its interrupt had come and gone while DOS waited for the disk, and the laugh
played to its end. Here a file took no time at all to read, so the laugh started while the key
was still down, and letting go of the key ended it.

Nothing in the game's code says it depends on a slow disk, and its writers surely never knew
it did. So the PC's disk can now be given a speed, `DiskBytesPerSecond`: opening a file and
reading from one then take the machine's time, with the program waiting in DOS and interrupts
on, as they were. The player gives it 200 KB a second, a hard disk of the day as DOS got bytes
off one. The tests play the scene both ways: with the speed, 2.8 seconds of laugh; without,
none.

## The notes, checked

A game running where its memory can be read is a way to check what was worked out by taking
it apart, and the first three commands typed into *Spellcasting 101* were chosen for that.
The test reads the game's own variables, at the addresses
[its notes](spellcasting-101.md) give, after each:

| Typed | What the notes said | What the game's memory had |
|---|---|---|
| (nothing yet) | The game starts in the Bedchamber, object 1, with you, object 153, in it | Room 1, actor 153, and 1 where your record says what you're in |
| `examine rat` | `examine OBJECT` is action 60, and the rat is object 352 | Action 60, object 352 |
| `climb chimney` | `climb OBJECT` is action 25, but the chimney's routine, with the fire in the fireplace, starts over with action 27 on the fireplace, object 62 | Action 27, object 62; and the game said `It would be a shame to see your young, healthy body reduced to a pile of charred bones!` |
| `n` | North from the Bedchamber is the Potty Chamber, object 2; going is action 197 | Action 197, room 2, and 2 in your record |

After the third, the picture on the screen is picture 51 of the first picture file as
Legendary unpacks it, to the pixel. The count of moves went 0, 1, 2, 3.

The run also showed what hadn't been found. The word after the count of moves, at
`4517:0048`, is the clock: 187 as the game starts, when the screen says 3:35p, and one more
for each move, which the screen shows as five minutes. And the list of objects the game puts
beside the list of verbs, for the mouse, is the objects its parser would find: the Bedchamber's
has the rat and the fire from the tree, and the chimney, the cobweb and `my house` from the
room's row of what is also there.

One thing didn't match. The Bedchamber's picture on the screen differs from picture 45 in
3,585 pixels of 63,672. Something is drawn over it, and what hasn't been looked at.

The first of those took two tries, and the second try is worth telling. With the state given
as 64 bytes that were mostly zero, the launcher drew its title for the EGA and the game it
loaded then drew for the CGA. Both ask Microsoft's graphics library for the EGA's 640 by 350;
the game [takes the CGA's mode when the library won't set that one](spellcasting-101.md#what-the-launcher-is-told),
and the library won't when the state says the card can't. A real VGA says how much memory it
has, how many lines and colors its mode has, and, by a pointer, which modes its BIOS knows.
With those filled in the game draws for the EGA. A program doesn't only ask whether a thing is
there; the answer has to be one it can believe.

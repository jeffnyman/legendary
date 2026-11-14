# The PC

The games are PC programs, and Legendary has a PC for them to run on: `src/Legendary.Pc`, an
x86 processor, the EGA, VGA and CGA, the timer and interrupt hardware, the speaker, FM and
MT-32 sound, and a BIOS and DOS answered in C#. It has no window of its own yet. What it's for
so far is to run a game where it can be watched, so that what these notes say a program does
can be checked against what it does.

It began as a copy of the core of [Excavate](https://github.com/jeffnyman/excavate), which
plays other DOS games, and Excavate's began as a copy of Confederation's. It is copied whole.
Most of it is more than these games need, the 386's protected mode for one, but it is one
machine and comes apart badly. Where it came from before that is in [NOTICE.md](../NOTICE.md).

A machine of this kind answers what its games ask and stops at anything else, so each new game
finds what the others didn't need. *Spellcasting 101* found four:

| What the game did | What the PC now does |
|---|---|
| Asked the video BIOS for the VGA's state, INT 10h function 1Bh, to see whether there is a VGA | Gives the state |
| Asked whether Microsoft's Hercules driver is loaded, INT 10h function EFh | Leaves the registers as they came, which says no |
| Had DOS parse a file's name into a file control block, INT 21h function 29h, as Microsoft C does for a program it starts | Parses it |
| Freed its memory by hand, through the list of lists, and had DOS load the game over it as an overlay, INT 21h function 4Bh with 3 | Gives the first memory block's segment before the list of lists, joins free blocks that are side by side before it resizes one, and loads overlays |

With those, the launcher draws its title, the same pixel for pixel as Legendary's own reader
of the picture file makes it, and the game starts and asks whether to restore a saved game.
The tests in `tests/Legendary.Pc.Tests` do both.

One thing isn't right yet. The launcher draws for the EGA, and the game it loads then draws
for the CGA: it sets the 640 by 200 mode and opens the CGA's pictures. How the game is told
which display to use hasn't been found.

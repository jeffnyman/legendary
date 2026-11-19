"""The guided tours of the games played with the mouse: their .HLP files.

A .HLP file is a recording of someone showing the game, as text, a line an event:

    00050 0008  18   9   0

which is the time since the event before, in thousandths of a second; what the event is;
where the pointer is, across and down; and a button. The events:

    0008  the pointer is here, said every tick of the PC's clock, so 50 or 60 apart
    0004  a click here, with the button, 1
    0002  not understood: it comes among the others, nearly always with no button
    4000  words are shown, which are the line after it
    8000  the end

The line after a 4000 is where the words go, across and down, how wide, and the words:

    20, 16, 280, Welcome to the "guided tour" of the new Legend adventure game system.

The game plays a file back by moving its own pointer and clicking, so the tour is the game
being played, and its clicks say where the things on the screen are.
"""

from __future__ import annotations

from dataclasses import dataclass

MOVE, CLICK, SHOWN, END = 0x0008, 0x0004, 0x4000, 0x8000


@dataclass(frozen=True)
class Event:
    """A line of a tour."""

    after: int  # thousandths of a second since the event before
    kind: int
    x: int
    y: int
    button: int
    words: str = ""  # for words shown: the words
    place: tuple[int, int, int] = (0, 0, 0)  # and where they go: across, down, and how wide


def read_tour(data: bytes) -> list[Event]:
    """A .HLP file's events."""
    lines = data.decode("latin-1").splitlines()
    events = []
    at = 0
    while at < len(lines):
        fields = lines[at].split()
        at += 1
        if not fields:
            continue
        if len(fields) != 5:
            raise ValueError(f"line {at} isn't an event: {lines[at - 1][:40]!r}")
        after, x, y, button = (int(fields[index]) for index in (0, 2, 3, 4))
        kind = int(fields[1], 16)
        if kind == SHOWN and at < len(lines):
            across, down, wide, words = lines[at].split(",", 3)
            at += 1
            place = (int(across), int(down), int(wide))
            events.append(Event(after, kind, x, y, button, words.strip(), place))
        else:
            events.append(Event(after, kind, x, y, button))
    return events

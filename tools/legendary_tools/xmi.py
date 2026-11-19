"""Mission Critical's music: its .XMI files, the extended MIDI of the Miles sound drivers.

The game has three, the same songs arranged for three kinds of card, which by their names are
FM.XMI for a card with an FM chip, GM.XMI for one that is General MIDI, and SF.XMI for one
that plays SoundFonts, a Sound Blaster AWE32. None of the format is Legend's.

A file is chunks as Electronic Arts' IFF has them: four letters, a length with its big end
first, and that many bytes, filled out to an even count. `FORM XDIR` holds `INFO`, a word
that is how many songs there are, and `CAT XMID` holds a `FORM XMID` for each. A song has
`TIMB`, a count and then a patch and its bank for each instrument the song wants loaded, and
`EVNT`, its events; some have `RBRN`, places in it to branch to.

The events are a MIDI track's, changed three ways. The time between events isn't a number of
variable length before each: it is bytes below 80h, added together, and none at all when
events fall together. A note is one event and not two: after its key and how hard, it has how
long it lasts, as a number of variable length, and nothing ever turns it off. And time isn't
in beats: a tick is one 120th of a second whatever tempo the song sets.

A song is given here as a Standard MIDI File of one track, with each note turned off when
its time is up, 60 ticks to a quarter note and a tempo of 120 a minute, which is 120 ticks a
second. The song's own tempos are left out, as the drivers never follow them. The
controllers Miles added, 110 to 120, are kept: 116 and 117 mark a part to play over, which a
file made here doesn't do.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

TICKS_A_SECOND = 120


@dataclass(frozen=True)
class XmiSong:
    """A song of an .XMI file."""

    number: int
    timbres: list[tuple[int, int]]  # each instrument it wants: the patch, and its bank
    events: bytes

    def _notes(self) -> tuple[list[tuple[int, int, bytes]], int]:
        """The events by when they happen, each note's end among them, and when the song ends."""
        data = self.events
        found: list[tuple[int, int, bytes]] = []  # when, the order it came in, and the event
        at = now = end = 0

        def number() -> int:
            nonlocal at
            value = 0
            while True:
                value = value << 7 | data[at] & 0x7F
                at += 1
                if not data[at - 1] & 0x80:
                    return value

        while at < len(data):
            status = data[at]
            if status < 0x80:
                now += status
                at += 1
                continue
            kind = status & 0xF0
            if status == 0xFF:
                meta, start = data[at + 1], at
                at += 2
                size = number()
                at += size
                if meta == 0x2F:
                    break
                # the drivers keep their own time: a tempo is left out
                if meta != 0x51:
                    found.append((now, len(found), data[start:at]))
            elif status in (0xF0, 0xF7):
                start = at
                at += 1
                size = number()
                at += size
                found.append((now, len(found), data[start:at]))
            elif kind == 0x90:
                key, velocity = data[at + 1], data[at + 2]
                at += 3
                lasts = number()
                found.append((now, len(found), bytes([status, key, velocity])))
                # turned off as a note on with nothing behind it, before what else falls then;
                # but a note of no length after it has been turned on
                found.append((now + lasts, -1 if lasts else len(found), bytes([status, key, 0])))
                end = max(end, now + lasts)
            else:
                size = 2 if kind in (0xC0, 0xD0) else 3
                found.append((now, len(found), data[at : at + size]))
                at += size
            end = max(end, now)
        return sorted(found), end

    @property
    def seconds(self) -> float:
        return self._notes()[1] / TICKS_A_SECOND

    def midi(self) -> bytes:
        """The song as a Standard MIDI File."""

        def length(value: int) -> bytes:
            parts = [value & 0x7F]
            while value := value >> 7:
                parts.append(value & 0x7F | 0x80)
            return bytes(reversed(parts))

        events, end = self._notes()
        # 120 quarter notes a minute, at 60 ticks each
        track = bytearray(b"\x00\xff\x51\x03\x07\xa1\x20")
        now = 0
        for when, _, event in events:
            track += length(when - now) + event
            now = when
        track += length(end - now) + b"\xff\x2f\x00"
        return (
            b"MThd"
            + struct.pack(">IHHH", 6, 0, 1, TICKS_A_SECOND // 2)
            + b"MTrk"
            + struct.pack(">I", len(track))
            + track
        )


def _chunks(data: bytes, at: int, end: int) -> list[tuple[bytes, int, int]]:
    """The chunks in a stretch of a file: each one's letters, and where its bytes start and end."""
    found = []
    while at + 8 <= end:
        (size,) = struct.unpack_from(">I", data, at + 4)
        found.append((data[at : at + 4], at + 8, at + 8 + size))
        at += 8 + size + (size & 1)
    return found


def read_xmi(data: bytes) -> list[XmiSong]:
    """The songs of an .XMI file."""
    if data[:4] != b"FORM" or data[8:12] != b"XDIR":
        raise ValueError("not an .XMI file: it doesn't start as one does")
    songs = []
    for letters, start, end in _chunks(data, 0, len(data)):
        if letters != b"CAT " or data[start : start + 4] != b"XMID":
            continue
        for form, inside, form_end in _chunks(data, start + 4, end):
            if form != b"FORM" or data[inside : inside + 4] != b"XMID":
                continue
            timbres: list[tuple[int, int]] = []
            events = b""
            for name, at, after in _chunks(data, inside + 4, form_end):
                if name == b"TIMB":
                    (count,) = struct.unpack_from("<H", data, at)
                    timbres = [(data[at + 2 + 2 * index], data[at + 3 + 2 * index]) for index in range(count)]
                elif name == b"EVNT":
                    events = data[at:after]
            songs.append(XmiSong(len(songs), timbres, events))
    return songs

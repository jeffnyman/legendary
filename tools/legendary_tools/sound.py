"""The game's music and sounds: its .MUS files, and its .RS files.

A .MUS file is songs, each kept twice, in two formats anything else could have written: a
Standard MIDI File, for a Roland MT-32, and an AdLib Visual Composer .ROL file, for an AdLib
card, which is played with the instruments in the game's .BNK file. The file starts with 30
entries of ten bytes, and a byte:

    0  where the song starts in the file, four bytes; 0 for no song
    4  its length
    6  1 for the MIDI file, 0 for the .ROL
    8  a word the two of a pair share, not yet understood

Timequest has 120 entries, and from Spellcasting 201 they are twelve bytes: the same, with a
second word at 10, and both words zero for the AdLib's song. From Timequest the AdLib's song
is no longer a .ROL. It is a MIDI file too, of the kind Ad Lib called .MDI: one track, with
the instruments in it, as events of the sequencer's own that start 00 00 3F. So there is no
.BNK file. A byte follows each song.

A .RS file is a sound for the PC's speaker, recorded: RealSound, which Access Software made and
Legend licensed. It has 32 bytes and then the sound:

    0  "STEVE"
    5  how many times each byte is played, which sets its speed: 1, 2 or 4
    6  72, the length of a pulse
    7  the count of the sound's bytes, four bytes; the rest of the 32 is whatever was in memory

Each byte is how long the speaker is pushed out for in a pulse 72 ticks of the PC's timer long,
so from 0 to 72, and a pulse comes 16,572 times a second. A byte of 0 ends the sound.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

TIMER_HZ = 1193182  # the PC's timer
PULSE_TICKS = 72


@dataclass(frozen=True)
class Song:
    """A song of a .MUS file, in one of its two formats."""

    number: int  # its entry, from 0
    midi: bool  # a Standard MIDI File for the MT-32; otherwise the AdLib's song
    shared: int
    data: bytes
    second: int = 0  # the later files' second word

    @property
    def extension(self) -> str:
        """What a file of it is called: an AdLib song that is a MIDI file is Ad Lib's .MDI."""
        if self.midi:
            return "mid"
        return "mdi" if self.data.startswith(b"MThd") else "rol"

    def names(self) -> list[str]:
        """A MIDI file's track names, which say what plays; none for a .ROL."""
        if not self.midi:
            return []
        found = []
        at = self.data.find(b"\xff\x03")
        while at >= 0:
            length = self.data[at + 2]
            text = self.data[at + 3 : at + 3 + length]
            if text and all(0x20 <= byte < 0x7F for byte in text):
                found.append(text.decode("ascii").strip())
            at = self.data.find(b"\xff\x03", at + 3)
        return found


def read_songs(data: bytes) -> list[Song]:
    """The songs of a .MUS file, in its order."""
    # the first song starts where the table ends, which says which table it is
    (first,) = struct.unpack_from("<I", data, 0)
    tables = {30 * 10 + 1: (30, 10), 120 * 10 + 1: (120, 10), 120 * 12 + 1: (120, 12)}
    if first not in tables:
        raise ValueError("the songs' table isn't one of the three that are known")
    count, size = tables[first]
    songs = []
    for number in range(count):
        offset, length, kind, shared = struct.unpack_from("<IHHH", data, size * number)
        if not offset:
            continue
        if offset + length > len(data):
            raise ValueError(f"song {number} goes past the end of the file")
        second = struct.unpack_from("<H", data, size * number + 10)[0] if size == 12 else 0
        songs.append(Song(number, kind == 1, shared, data[offset : offset + length], second))
    return songs


@dataclass(frozen=True)
class Sound:
    """A RealSound recording."""

    repeat: int  # how many pulses each byte is played for
    widths: bytes  # each from 0 to 72: how much of its pulse the speaker is out for

    @property
    def rate(self) -> float:
        """Bytes played a second."""
        return TIMER_HZ / PULSE_TICKS / self.repeat

    @property
    def seconds(self) -> float:
        return len(self.widths) / self.rate

    def wav(self) -> bytes:
        """The sound as a WAV file's bytes: eight bits, one channel, a pulse's width as a level."""
        rate = round(self.rate)
        levels = bytes(min(width, PULSE_TICKS) * 255 // PULSE_TICKS for width in self.widths)
        header = struct.pack(
            "<4sI4s4sIHHIIHH4sI", b"RIFF", 36 + len(levels), b"WAVE", b"fmt ", 16, 1, 1, rate, rate, 1, 8,
            b"data", len(levels),
        )  # fmt: skip
        return header + levels


def read_sound(data: bytes) -> Sound:
    """A .RS file's sound."""
    if data[:5] != b"STEVE" or len(data) < 32:
        raise ValueError("not a RealSound file: it doesn't start with STEVE")
    repeat = data[5]
    (length,) = struct.unpack_from("<I", data, 7)
    widths = data[32 : 32 + length]
    # the player stops at a zero
    end = widths.find(b"\0")
    return Sound(repeat, widths if end < 0 else widths[:end])

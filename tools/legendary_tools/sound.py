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

A .VOC file is the speech of a game on a CD, and isn't Creative's file of that name. It
starts with a word, a count, and that many offsets of four bytes, the last of them the
file's length; so one fewer lines than the count, and a line can be empty. A line is from
its offset to the next, and is the sound and nothing else: a byte a sample, with 128 for
silence, as a Sound Blaster plays them. Every line starts at a multiple of 4,096, the
header being filled out with FF and a line with silence.

Nothing in the file says how fast it is played. The Sound Blaster's driver in the program
has a table of the speeds it can play at, as millionths of a second a sample: the four
RealSound speeds, and 91 and 45, which are 10,989 and 22,222 a second. The speech is played
at the last, 22,222: the game, run, gives the Sound Blaster a line at that speed.

Mission Critical has three such files, one on each of its CDs, each with the same count and
the lines that CD's part of the game speaks. There a line is a whole RIFF WAVE file, which
says its own speed and size: one channel, 16 bits, 22,050 samples a second. A line starts
at a multiple of 2,048, a CD's sector, and what fills it out after its WAVE ends is zeros.
"""

from __future__ import annotations

import os
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


SPEECH_RATE = 1_000_000 // 45  # the Sound Blaster's time for a sample, 45 millionths of a second


@dataclass(frozen=True)
class Speech:
    """A .VOC file's lines, read from the file as they are asked for: it is a big file."""

    path: str
    offsets: tuple[int, ...]  # where each line starts, and after the last where it ends
    waves: bool = False  # whether each line is a RIFF WAVE file, as Mission Critical's are

    def __len__(self) -> int:
        return len(self.offsets) - 1

    def _read(self, line: int, size: int | None = None) -> bytes:
        whole = self.offsets[line + 1] - self.offsets[line]
        with open(self.path, "rb") as file:
            file.seek(self.offsets[line])
            return file.read(whole if size is None else min(size, whole))

    def _wave(self, line: int) -> tuple[int, int, int] | None:
        """A WAVE line's bytes a second, where its samples start in it, and how many bytes they are."""
        header = self._read(line, 44)
        if len(header) < 44 or header[:4] != b"RIFF" or header[8:16] != b"WAVEfmt ":
            return None
        if header[16:20] != b"\x10\0\0\0" or header[36:40] != b"data":
            raise ValueError(f"line {line} is a WAVE file of a kind that isn't read")
        return struct.unpack_from("<I", header, 28)[0], 44, struct.unpack_from("<I", header, 40)[0]

    @property
    def rate(self) -> int:
        """How many samples a second the speech is played at."""
        if self.waves:
            for line in range(len(self)):
                if self.offsets[line + 1] > self.offsets[line]:
                    return struct.unpack_from("<I", self._read(line, 28), 24)[0]
        return SPEECH_RATE

    def seconds(self, line: int) -> float:
        """How long a line is: with the silence it is filled out with, where it doesn't say its size."""
        if self.offsets[line + 1] == self.offsets[line]:
            return 0.0
        if self.waves and (wave := self._wave(line)):
            return wave[2] / wave[0]
        return (self.offsets[line + 1] - self.offsets[line]) / SPEECH_RATE

    def samples(self, line: int) -> bytes:
        """A line's samples, without the silence at its end, which fills it out."""
        if self.waves and (wave := self._wave(line)):
            return self._read(line)[wave[1] : wave[1] + wave[2]]
        return self._read(line).rstrip(b"\x80")

    def wav(self, line: int) -> bytes:
        """A line as a WAV file's bytes: its own, or eight bits and one channel made into one."""
        if self.waves and (wave := self._wave(line)):
            return self._read(line)[: wave[1] + wave[2]]
        samples = self.samples(line)
        header = struct.pack(
            "<4sI4s4sIHHIIHH4sI", b"RIFF", 36 + len(samples), b"WAVE", b"fmt ", 16, 1, 1,
            SPEECH_RATE, SPEECH_RATE, 1, 8, b"data", len(samples),
        )  # fmt: skip
        return header + samples


def read_speech(path: str) -> Speech:
    """A .VOC file's lines."""
    size = os.path.getsize(path)
    with open(path, "rb") as file:
        (count,) = struct.unpack("<H", file.read(2))
        offsets = struct.unpack(f"<{count}I", file.read(4 * count))
    if not offsets or offsets[-1] != size or any(a > b for a, b in zip(offsets, offsets[1:])):
        raise ValueError("not a file of speech: its offsets don't go up to its length")
    # what a line is, by how the first one there starts
    first = next((at for at, after in zip(offsets, offsets[1:]) if after > at), None)
    waves = False
    if first is not None:
        with open(path, "rb") as file:
            file.seek(first)
            waves = file.read(4) == b"RIFF"
    return Speech(path, offsets, waves)

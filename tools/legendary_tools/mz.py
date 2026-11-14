"""The MZ header of a DOS program, and the image and relocations it describes."""

from __future__ import annotations

import struct
from dataclasses import dataclass


@dataclass(frozen=True)
class Relocation:
    """A place in the image that holds a segment, which DOS adds the load segment to."""

    segment: int
    offset: int

    @property
    def linear(self) -> int:
        return self.segment * 16 + self.offset


class MzExecutable:
    """A DOS program as its file has it. Segments are relative to where the image loads."""

    def __init__(self, data: bytes):
        if data[:2] not in (b"MZ", b"ZM"):
            raise ValueError("not an MZ program")
        (
            last_page,
            pages,
            relocation_count,
            header_paragraphs,
            self.min_alloc,
            self.max_alloc,
            self.ss,
            self.sp,
            self.checksum,
            self.ip,
            self.cs,
            relocation_offset,
            self.overlay_number,
        ) = struct.unpack_from("<13H", data, 2)
        self.data = data
        self.header_size = header_paragraphs * 16
        # the last page's count is of the bytes used in it, and 0 means all of it
        self.file_image_end = pages * 512 - ((512 - last_page) % 512)
        self.image = data[self.header_size : self.file_image_end]
        self.relocations = [
            Relocation(segment, offset)
            for offset, segment in struct.iter_unpack(
                "<HH", data[relocation_offset : relocation_offset + 4 * relocation_count]
            )
        ]

    @property
    def trailing(self) -> int:
        """The bytes in the file after the image the header describes."""
        return len(self.data) - self.file_image_end

    @property
    def image_paragraphs(self) -> int:
        return (len(self.image) + 15) // 16

    @property
    def memory_paragraphs(self) -> int:
        """What DOS gives the program at the least: its image, and min_alloc above it."""
        return self.image_paragraphs + self.min_alloc

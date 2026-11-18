"""Finding a game's files in its folder, whatever case their names are in."""

from __future__ import annotations

import os


class GameFolder:
    """A game's folder: the one with its ...MAIN.EXE in it."""

    def __init__(self, path: str):
        if not os.path.isdir(path):
            raise ValueError(f"{path}: not a folder")
        self.path = path
        self._names = {name.lower(): name for name in os.listdir(path)}

    def find(self, name: str) -> str | None:
        """The path of the file with that name, its case aside; None when there is none."""
        actual = self._names.get(name.lower())
        return os.path.join(self.path, actual) if actual else None

    def ending(self, suffix: str) -> list[str]:
        """The paths of the files whose names end that way, its case aside, in order."""
        return [
            os.path.join(self.path, actual)
            for lower, actual in sorted(self._names.items())
            if lower.endswith(suffix.lower())
        ]

    def one(self, suffix: str) -> str:
        """The one file whose name ends that way."""
        found = self.ending(suffix)
        if len(found) != 1:
            what = "no file" if not found else "more than one file"
            raise ValueError(f"{self.path}: {what} ending in {suffix}")
        return found[0]

    def main_program(self) -> str:
        """
        The game itself, S101MAIN.EXE or TIMEMAIN.EXE: what the launcher starts. Companions of
        Xanth has no launcher, and its program is the one that has overlays beside it.
        """
        if self.ending("main.exe"):
            return self.one("main.exe")
        beside = [
            path
            for path in self.ending(".exe")
            if self.find(os.path.splitext(os.path.basename(path))[0] + ".ovl")
        ]
        if len(beside) != 1:
            raise ValueError(f"{self.path}: no file ending in main.exe")
        return beside[0]

    def overlays_file(self) -> str | None:
        """The file of the game's overlays, where it has one of its own."""
        program = os.path.basename(self.main_program())
        return self.find(os.path.splitext(program)[0] + ".ovl")

    def read(self, path: str) -> bytes:
        with open(path, "rb") as file:
            return file.read()

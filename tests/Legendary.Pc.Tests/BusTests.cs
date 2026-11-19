namespace Legendary.Pc.Tests;

/// <summary>What a port costs in time, where a program counts on it.</summary>
public sealed class BusTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("legendary-game").FullName;
    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-saves").FullName;

    public void Dispose()
    {
        Directory.Delete(_game, recursive: true);
        Directory.Delete(_saves, recursive: true);
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(4_000_000)]
    [InlineData(30_000_000)]
    [InlineData(100_000_000)]
    public void A_program_that_waits_by_reading_the_FM_chips_status_finds_the_chip_at_any_speed(int instructionsPerSecond)
    {
        // How a driver finds an AdLib: it starts timer 1, which runs out in 80 microseconds, waits by
        // reading the status port a fixed number of times, and looks for the timer's bits. The reads
        // are the wait, so each has to take what the bus made it take, a microsecond, however many
        // instructions the processor does in one.
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), DosTests.Exe(
        [
            0xBA, 0x88, 0x03,       // mov dx, 0x388
            0xB0, 0x04, 0xEE,       // mov al, 4; out dx, al    (the timers' control)
            0x42,                   // inc dx
            0xB0, 0x60, 0xEE,       // mov al, 0x60; out dx, al    (both timers masked)
            0xB0, 0x80, 0xEE,       // mov al, 0x80; out dx, al    (and their flags cleared)
            0x4A,                   // dec dx
            0xB0, 0x02, 0xEE,       // mov al, 2; out dx, al    (timer 1's count)
            0x42,                   // inc dx
            0xB0, 0xFF, 0xEE,       // mov al, 0xFF; out dx, al    (one step from running out)
            0x4A,                   // dec dx
            0xB0, 0x04, 0xEE,       // mov al, 4; out dx, al
            0x42,                   // inc dx
            0xB0, 0x21, 0xEE,       // mov al, 0x21; out dx, al    (timer 1 started)
            0x4A,                   // dec dx
            0xB9, 0xC8, 0x00,       // mov cx, 200
            // wait:
            0xEC,                   // in al, dx
            0xE2, 0xFD,             // loop wait
            0xEC,                   // in al, dx    (the status)
            0xB4, 0x4C,             // mov ah, 0x4C    (and out with it)
            0xCD, 0x21,             // int 0x21
        ]));
        var machine = new Machine(_game, _saves) { InstructionsPerSecond = instructionsPerSecond };
        machine.Load("TEST.EXE");
        machine.Run(instructionsPerSecond);

        Assert.True(machine.Exited);
        Assert.Equal(0xC0, machine.ExitCode & 0xE0);
    }
}

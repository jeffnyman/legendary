namespace Legendary.Pc.Tests;

/// <summary>
/// Programs that end and stay in memory, with two .COM programs assembled by hand: a driver that
/// takes an interrupt and keeps itself, and a program run after it that calls the interrupt.
/// </summary>
public sealed class ResidentTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("legendary-game").FullName;
    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-saves").FullName;
    private readonly List<string> _log = [];

    public void Dispose()
    {
        Directory.Delete(_game, recursive: true);
        Directory.Delete(_saves, recursive: true);
    }

    private Machine Run(params string[] commands)
    {
        var machine = new Machine(_game, _saves) { Log = _log.Add };
        machine.LoadCommands(commands);
        for (int i = 0; i < 100 && !machine.Exited; i++)
        {
            machine.Run(10_000);
        }

        Assert.True(machine.Exited, "The programs didn't end.");
        return machine;
    }

    /// <summary>The driver, a .COM program, which starts at 100h with every segment on its PSP.</summary>
    /// <param name="keep27">How it stays: by INT 27h, or else by INT 21h function 31h.</param>
    private void WriteDriver(bool keep27)
    {
        //   100: mov dx, 110h; mov ax, 2560h; int 21h      INT 60h is the handler's
        //   108: (INT 27h)  mov dx, 113h; int 27h; nop; nop; nop     keep to the handler's end
        //        (function 31h)  mov dx, 12h; mov ax, 3100h; int 21h   keep 12h paragraphs
        //   110: mov al, 2Ah; iret                         the handler
        byte[] stay = keep27 ? [0xBA, 0x13, 0x01, 0xCD, 0x27, 0x90, 0x90, 0x90] : [0xBA, 0x12, 0x00, 0xB8, 0x00, 0x31, 0xCD, 0x21];
        File.WriteAllBytes(Path.Combine(_game, "DRIVER.COM"), [0xBA, 0x10, 0x01, 0xB8, 0x60, 0x25, 0xCD, 0x21, .. stay, 0xB0, 0x2A, 0xCF]);

        // the program that uses it: int 60h; mov ah, 4Ch; int 21h, so it ends with what the handler gave
        File.WriteAllBytes(Path.Combine(_game, "USER.COM"), [0xCD, 0x60, 0xB4, 0x4C, 0xCD, 0x21]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_driver_stays_and_the_next_program_calls_it(bool keep27)
    {
        WriteDriver(keep27);

        var machine = Run("DRIVER.COM", "USER.COM");

        Assert.Contains(_log, line => line.StartsWith("stays resident", StringComparison.Ordinal));
        Assert.Equal(0x2A, machine.ExitCode);

        // the second program was loaded above the first, not over it
        string[] loaded = [.. _log.Where(line => line.StartsWith("loaded ", StringComparison.Ordinal))];
        Assert.Equal(2, loaded.Length);
        Assert.NotEqual(loaded[0].Split(" at ")[1], loaded[1].Split(" at ")[1]);
    }

    /// <summary>A program that only ends is gone, and the next gets its memory: the same PSP.</summary>
    [Fact]
    public void A_program_that_ends_leaves_its_memory_to_the_next()
    {
        File.WriteAllBytes(Path.Combine(_game, "ONE.COM"), [0xB8, 0x00, 0x4C, 0xCD, 0x21]);
        File.WriteAllBytes(Path.Combine(_game, "TWO.COM"), [0xB8, 0x05, 0x4C, 0xCD, 0x21]);

        var machine = Run("ONE.COM", "TWO.COM");

        Assert.Equal(5, machine.ExitCode);
        string[] loaded = [.. _log.Where(line => line.StartsWith("loaded ", StringComparison.Ordinal))];
        Assert.Equal(loaded[0].Split(" at ")[1], loaded[1].Split(" at ")[1]);
    }

    /// <summary>
    /// A disk with a speed takes the machine's time: opening a file, and then each byte read.
    /// A file of 2,000 bytes read from a disk of 10,000 a second is a fifth of a second, and the
    /// opening a little more; from a disk with no speed, it is no time to speak of.
    /// </summary>
    [Theory]
    [InlineData(10_000, 0.23, 0.29)]
    [InlineData(0, 0, 0.02)]
    public void A_disk_with_a_speed_takes_time(int speed, double least, double most)
    {
        File.WriteAllBytes(Path.Combine(_game, "DATA.BIN"), new byte[2000]);

        //   100: mov ax, 3D00h; mov dx, 121h; int 21h       open DATA.BIN
        //   108: mov bx, ax; mov ah, 3Fh; mov cx, 2000; mov dx, 200h; int 21h
        //   114: cmp ax, 2000; mov al, 1; jne +2; mov al, 0; mov ah, 4Ch; int 21h
        //   121: the name
        File.WriteAllBytes(Path.Combine(_game, "READ.COM"),
        [
            0xB8, 0x00, 0x3D, 0xBA, 0x21, 0x01, 0xCD, 0x21,
            0x8B, 0xD8, 0xB4, 0x3F, 0xB9, 0xD0, 0x07, 0xBA, 0x00, 0x02, 0xCD, 0x21,
            0x3D, 0xD0, 0x07, 0xB0, 0x01, 0x75, 0x02, 0xB0, 0x00, 0xB4, 0x4C, 0xCD, 0x21,
            .. "DATA.BIN\0"u8,
        ]);

        var machine = new Machine(_game, _saves) { Log = _log.Add, InstructionsPerSecond = 1_000_000, DiskBytesPerSecond = speed };
        machine.Load("READ.COM");
        for (int i = 0; i < 100 && !machine.Exited; i++)
        {
            machine.Run(10_000);
        }

        Assert.True(machine.Exited, "The program didn't end.");
        Assert.Equal(0, machine.ExitCode);
        Assert.InRange(machine.Time.TotalSeconds, least, most);
    }

    /// <summary>
    /// The mouse driver draws its pointer in a graphics mode while the program has it shown, and
    /// keeps it within the mode's screen: in the EGA's 640 by 350, all 350 rows of it.
    /// </summary>
    [Fact]
    public void The_mouse_drivers_arrow_is_drawn_where_the_mouse_is()
    {
        //   mov ax, 10h; int 10h      the EGA's 640 by 350
        //   mov ax, 0; int 33h        reset the mouse
        //   mov ax, 1; int 33h        show the pointer
        //   mov ax, 4C00h; int 21h
        File.WriteAllBytes(Path.Combine(_game, "ARROW.COM"),
            [0xB8, 0x10, 0x00, 0xCD, 0x10, 0xB8, 0x00, 0x00, 0xCD, 0x33, 0xB8, 0x01, 0x00, 0xCD, 0x33, 0xB8, 0x00, 0x4C, 0xCD, 0x21]);
        var machine = new Machine(_game, _saves) { Log = _log.Add, Mouse = true };
        machine.Load("ARROW.COM");
        for (int i = 0; i < 100 && !machine.Exited; i++)
        {
            machine.Run(10_000);
        }

        Assert.Equal((640, 350), (machine.DisplayWidth, machine.DisplayHeight));
        uint[] screen = new uint[640 * 350];
        uint At(int x, int y)
        {
            machine.Render(screen);
            return screen[(y * 640) + x] & 0xFFFFFF;
        }

        // the pointer starts in the middle: the second row of the arrow is black, white, black
        Assert.Equal((0x000000u, 0xFFFFFFu, 0x000000u), (At(320, 176), At(321, 176), At(322, 176)));

        // 16 mickeys are 8 rows: down to the last row, and back up ten
        machine.MoveMouse(0, 10_000);
        machine.MoveMouse(0, -20);
        Assert.Equal(0xFFFFFFu, At(321, 340));
        Assert.Equal(0x000000u, At(321, 176));

        // and across: 8 mickeys are 8 columns
        machine.MoveMouse(-100, 0);
        Assert.Equal(0xFFFFFFu, At(221, 340));
    }

    /// <summary>A pointer the program hasn't asked to be shown isn't drawn.</summary>
    [Fact]
    public void A_hidden_pointer_is_not_drawn()
    {
        //   mov ax, 10h; int 10h; mov ax, 0; int 33h; mov ax, 4C00h; int 21h
        File.WriteAllBytes(Path.Combine(_game, "HIDDEN.COM"),
            [0xB8, 0x10, 0x00, 0xCD, 0x10, 0xB8, 0x00, 0x00, 0xCD, 0x33, 0xB8, 0x00, 0x4C, 0xCD, 0x21]);
        var machine = new Machine(_game, _saves) { Log = _log.Add, Mouse = true };
        machine.Load("HIDDEN.COM");
        for (int i = 0; i < 100 && !machine.Exited; i++)
        {
            machine.Run(10_000);
        }

        uint[] screen = new uint[640 * 350];
        machine.Render(screen);
        Assert.All(screen, pixel => Assert.Equal(0u, pixel & 0xFFFFFF));
    }
}

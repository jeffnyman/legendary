using Legendary.Pc.Devices;

namespace Legendary.Pc.Tests;

/// <summary>
/// VESA's modes of 256 colors, which Mission Critical draws in: the card's memory and the window
/// on it, and the video BIOS's functions that say what the card has, set a mode and move the window.
/// </summary>
public sealed class VesaTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("legendary-game").FullName;
    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-saves").FullName;

    public void Dispose()
    {
        Directory.Delete(_game, recursive: true);
        Directory.Delete(_saves, recursive: true);
    }

    [Theory]
    [InlineData(0x100, 640, 400)]
    [InlineData(0x101, 640, 480)]
    [InlineData(0x103, 800, 600)]
    [InlineData(0x105, 1024, 768)]
    public void Each_mode_has_its_size(int mode, int width, int height)
    {
        var vga = new Vga();
        vga.SetMode(mode, clear: true);
        Assert.Equal((width, height), vga.Size);
    }

    [Fact]
    public void The_window_at_A0000h_is_whichever_64_KB_the_bank_says()
    {
        var vga = new Vga();
        vga.SetMode(0x101, clear: true);

        // the same address, in the first 64 KB and in the fifth: pixel 5 of the first line, and
        // byte 262,149 of the picture, which is line 409, pixel 389
        vga.Write(0xA_0005, 1);
        vga.Bank = 4;
        Assert.Equal(0, vga.Read(0xA_0005));
        vga.Write(0xA_0005, 2);
        vga.Bank = 0;
        Assert.Equal(1, vga.Read(0xA_0005));

        var pixels = new uint[640 * 480];
        vga.Render(pixels);
        Assert.Equal(0xFF00_00AAu, pixels[5]);
        Assert.Equal(0xFF00_AA00u, pixels[(409 * 640) + 389]);
        Assert.Equal(0xFF00_0000u, pixels[6]);
    }

    [Fact]
    public void Setting_the_mode_again_clears_its_memory_and_another_mode_leaves_it()
    {
        var vga = new Vga();
        vga.SetMode(0x101, clear: true);
        vga.Bank = 3;
        vga.Write(0xA_0000, 9);
        vga.SetMode(0x101, clear: false);
        Assert.Equal(0, vga.Bank);
        vga.Bank = 3;
        Assert.Equal(9, vga.Read(0xA_0000));
        vga.SetMode(0x101, clear: true);
        vga.Bank = 3;
        Assert.Equal(0, vga.Read(0xA_0000));

        // and mode 13h is still its own 64 KB
        vga.SetMode(0x13, clear: true);
        Assert.Equal((320, 200), vga.Size);
    }

    [Fact]
    public void A_program_asks_the_bios_for_the_mode_sets_it_and_moves_the_window()
    {
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), DosTests.Exe(
        [
            0x0E, 0x07,                         // push cs; pop es
            0x0E, 0x1F,                         // push cs; pop ds
            0xBF, 0x00, 0x02,                   // mov di, 0x200
            0xB8, 0x00, 0x4F,                   // mov ax, 0x4F00    (what the card is, to ES:DI)
            0xCD, 0x10,                         // int 0x10
            0x3D, 0x4F, 0x00,                   // cmp ax, 0x004F
            0x75, 0x4B,                         // jne fail
            0xBF, 0x00, 0x03,                   // mov di, 0x300
            0xB9, 0x01, 0x01,                   // mov cx, 0x101
            0xB8, 0x01, 0x4F,                   // mov ax, 0x4F01    (what mode 101h is like)
            0xCD, 0x10,                         // int 0x10
            0x3D, 0x4F, 0x00,                   // cmp ax, 0x004F
            0x75, 0x3B,                         // jne fail
            0xBB, 0x01, 0x01,                   // mov bx, 0x101
            0xB8, 0x02, 0x4F,                   // mov ax, 0x4F02    (set it)
            0xCD, 0x10,                         // int 0x10
            0x3D, 0x4F, 0x00,                   // cmp ax, 0x004F
            0x75, 0x2E,                         // jne fail
            0xB8, 0x00, 0xA0,                   // mov ax, 0xA000
            0x8E, 0xC0,                         // mov es, ax
            0x26, 0xC6, 0x06, 0x05, 0x00, 0x01, // mov byte ptr es:[5], 1    (the sixth pixel of the first line)
            0x31, 0xDB,                         // xor bx, bx
            0xBA, 0x04, 0x00,                   // mov dx, 4
            0xB8, 0x05, 0x4F,                   // mov ax, 0x4F05    (the window to the fifth 64 KB)
            0xCD, 0x10,                         // int 0x10
            0x26, 0xC6, 0x06, 0x00, 0x00, 0x02, // mov byte ptr es:[0], 2    (byte 262,144 of the picture: line 409, pixel 384)
            0x31, 0xDB,                         // xor bx, bx
            0x31, 0xD2,                         // xor dx, dx
            0xFF, 0x1E, 0x0C, 0x03,             // call far [0x30C]    (and back, by the routine the mode names)
            0x26, 0xC6, 0x06, 0x06, 0x00, 0x03, // mov byte ptr es:[6], 3
            0xB8, 0x00, 0x4C,                   // mov ax, 0x4C00
            0xCD, 0x21,                         // int 0x21
            // fail:
            0xB8, 0x01, 0x4C,                   // mov ax, 0x4C01
            0xCD, 0x21,                         // int 0x21
        ]));
        var machine = new Machine(_game, _saves);
        machine.Load("TEST.EXE");
        machine.Run(1_000_000);

        Assert.True(machine.Exited);
        Assert.Equal(0, machine.ExitCode);
        Assert.Equal(0x101, machine.VideoMode);
        Assert.Equal((640, 480), (machine.DisplayWidth, machine.DisplayHeight));

        var pixels = new uint[640 * 480];
        machine.Render(pixels);
        Assert.Equal(0xFF00_00AAu, pixels[5]);
        Assert.Equal(0xFF00_AAAAu, pixels[6]);
        Assert.Equal(0xFF00_AA00u, pixels[(409 * 640) + 384]);
    }

    [Fact]
    public void A_CGA_has_no_vesa()
    {
        File.WriteAllBytes(Path.Combine(_game, "TEST.EXE"), DosTests.Exe(
        [
            0x0E, 0x07,                         // push cs; pop es
            0xBF, 0x00, 0x02,                   // mov di, 0x200
            0xB8, 0x00, 0x4F,                   // mov ax, 0x4F00
            0xCD, 0x10,                         // int 0x10
            0xB4, 0x4C,                         // mov ah, 0x4C    (and out, with what came back in AL)
            0xCD, 0x21,                         // int 0x21
        ]));
        var machine = new Machine(_game, _saves, VideoAdapter.Cga);
        machine.Load("TEST.EXE");
        machine.Run(1_000_000);
        Assert.True(machine.Exited);
        Assert.Equal(0x00, machine.ExitCode);
    }
}

namespace Legendary.Pc.Tests;

/// <summary>
/// The processor's watch on an address: the host is told each time the program is about to run
/// the instruction there. With a .COM program assembled by hand, a loop of three turns.
/// </summary>
public sealed class WatchTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("legendary-game").FullName;
    private readonly string _saves = Directory.CreateTempSubdirectory("legendary-saves").FullName;

    public void Dispose()
    {
        Directory.Delete(_game, recursive: true);
        Directory.Delete(_saves, recursive: true);
    }

    private Machine Load()
    {
        //   100: mov cx, 3
        //   103: inc ax
        //   104: loop 103h
        //   106: mov ax, 4C00h; int 21h
        File.WriteAllBytes(Path.Combine(_game, "LOOP.COM"), [0xB9, 0x03, 0x00, 0x40, 0xE2, 0xFD, 0xB8, 0x00, 0x4C, 0xCD, 0x21]);
        var machine = new Machine(_game, _saves);
        machine.Load("LOOP.COM");
        return machine;
    }

    private static void RunToTheEnd(Machine machine)
    {
        for (int i = 0; i < 100 && !machine.Exited; i++)
        {
            machine.Run(10_000);
        }

        Assert.True(machine.Exited, "The program didn't end.");
    }

    [Fact]
    public void The_host_is_told_each_time_the_program_gets_there_before_the_instruction_runs()
    {
        var machine = Load();
        var cpu = machine.Cpu;
        var counts = new List<uint>();
        var elsewhere = new List<uint>();

        // a .COM program starts with every segment on its PSP
        cpu.Watch(((uint)cpu.Cs << 4) + 0x103, () => counts.Add(cpu.Ecx & 0xFFFF));
        cpu.Watch(((uint)cpu.Cs << 4) + 0x106, () => elsewhere.Add(cpu.Eip));
        RunToTheEnd(machine);

        // CX as the loop's body finds it each turn, and the way out once, with nothing run there yet
        Assert.Equal([3u, 2u, 1u], counts);
        Assert.Equal([0x106u], elsewhere);
    }

    [Fact]
    public void An_address_in_the_middle_of_an_instruction_isnt_one_the_program_gets_to()
    {
        var machine = Load();
        int told = 0;
        machine.Cpu.Watch(((uint)machine.Cpu.Cs << 4) + 0x101, () => told++);
        RunToTheEnd(machine);
        Assert.Equal(0, told);
    }

    [Fact]
    public void With_nothing_watched_nobody_is_told()
    {
        var machine = Load();
        int told = 0;
        machine.Cpu.Watch(((uint)machine.Cpu.Cs << 4) + 0x103, () => told++);
        machine.Cpu.WatchNothing();
        RunToTheEnd(machine);
        Assert.Equal(0, told);
    }
}

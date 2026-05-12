var failures = 0;

Run("zero cell stays in origin chunk", () => ExpectChunk(0, 0, 0, 0, 0, 0));
Run("positive edge stays in origin chunk", () => ExpectChunk(15, 15, 15, 0, 0, 0));
Run("positive boundary enters next chunk", () => ExpectChunk(16, 16, 16, 1, 1, 1));
Run("negative one floors into previous chunk", () => ExpectChunk(-1, -1, -1, -1, -1, -1));
Run("negative boundary stays in same negative chunk", () => ExpectChunk(-16, -16, -16, -1, -1, -1));
Run("negative past boundary enters next negative chunk", () => ExpectChunk(-17, -17, -17, -2, -2, -2));

if (failures == 0)
{
    Console.WriteLine("All chunk math tests passed.");
}

Environment.ExitCode = failures;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

static void ExpectChunk(int x, int y, int z, int expectedX, int expectedY, int expectedZ)
{
    var actual = VoxelChunkMath.FromVoxelCell(x, y, z);
    var expected = new VoxelChunkCoordinate(expectedX, expectedY, expectedZ);

    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

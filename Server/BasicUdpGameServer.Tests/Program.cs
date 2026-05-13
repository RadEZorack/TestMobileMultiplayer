var failures = 0;

Run("zero cell stays in origin chunk", () => ExpectChunk(0, 0, 0, 0, 0, 0));
Run("positive edge stays in origin chunk", () => ExpectChunk(15, 15, 15, 0, 0, 0));
Run("positive boundary enters next chunk", () => ExpectChunk(16, 16, 16, 1, 1, 1));
Run("negative one floors into previous chunk", () => ExpectChunk(-1, -1, -1, -1, -1, -1));
Run("negative boundary stays in same negative chunk", () => ExpectChunk(-16, -16, -16, -1, -1, -1));
Run("negative past boundary enters next negative chunk", () => ExpectChunk(-17, -17, -17, -2, -2, -2));
Run("turn credential uses coturn rest auth hmac", ExpectTurnCredential);
Run("turn ice server includes stun and turn urls", ExpectTurnIceServerConfig);

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

static void ExpectTurnCredential()
{
    var actual = RtcSignalingOptions.CreateTurnCredential("1700000000", "secret");
    const string Expected = "WGw37+g43pfwVUmrc9tgArn/juE=";

    if (actual != Expected)
    {
        throw new InvalidOperationException($"Expected {Expected}, got {actual}.");
    }
}

static void ExpectTurnIceServerConfig()
{
    var options = new RtcSignalingOptions("dev.augmego.ca", "dev.augmego.ca", "secret");
    var servers = options.CreateIceServers(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

    if (servers.Length != 1
        || servers[0].Username != "1700014400"
        || servers[0].Credential.Length == 0
        || !servers[0].Urls.Contains("stun:dev.augmego.ca:3478")
        || !servers[0].Urls.Contains("turn:dev.augmego.ca:3478?transport=udp")
        || !servers[0].Urls.Contains("turn:dev.augmego.ca:3478?transport=tcp"))
    {
        throw new InvalidOperationException("TURN ICE server config was not generated as expected.");
    }
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

[assembly: InternalsVisibleTo("BasicUdpGameServer.Tests")]

const string DefaultWorldId = "default";
const string DefaultDatabaseConnectionString =
    "Host=localhost;Port=5432;Database=mobile_multiplayer;Username=game;Password=game_dev_password;Include Error Detail=true";
const int DefaultHttpPort = 8080;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort)
    ? parsedPort
    : 7777;
var httpPort = int.TryParse(Environment.GetEnvironmentVariable("GAME_HTTP_PORT"), out var parsedHttpPort)
    ? parsedHttpPort
    : DefaultHttpPort;

using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var databaseConnectionString = Environment.GetEnvironmentVariable("GAME_DATABASE_URL");

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    databaseConnectionString = DefaultDatabaseConnectionString;
}

await using var voxelEditStore = new PostgresVoxelEditStore(databaseConnectionString, DefaultWorldId);
await voxelEditStore.InitializeAsync(shutdown.Token);
var persistedVoxelEdits = await voxelEditStore.LoadVoxelEditsAsync(shutdown.Token);

using var signalingHub = new RtcSignalingHub(RtcSignalingOptions.FromEnvironment());
using var server = new BasicUdpGameServer(port, voxelEditStore, persistedVoxelEdits, signalingHub);
await using var webApp = CreateWebApplication(httpPort, signalingHub, server);

try
{
    await Task.WhenAll(server.RunAsync(shutdown.Token), webApp.RunAsync(shutdown.Token));
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    // Normal Ctrl+C shutdown path.
}

static WebApplication CreateWebApplication(int httpPort, RtcSignalingHub signalingHub, BasicUdpGameServer gameServer)
{
    var builder = WebApplication.CreateBuilder(Array.Empty<string>());
    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(httpPort));
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    var app = builder.Build();
    app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

    app.MapGet("/health", () => Results.Text("ok\n", "text/plain"));
    app.Map("/rtc", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected websocket request.", context.RequestAborted);
            return;
        }

        await signalingHub.HandleWebSocketAsync(context, gameServer, context.RequestAborted);
    });

    Console.WriteLine($"WebSocket signaling listening on 0.0.0.0:{httpPort}/rtc");
    return app;
}

internal sealed class BasicUdpGameServer : IDisposable
{
    private const float TickRate = 30f;
    private const float PlayerSpeed = 4.5f;
    private const float ArenaHalfWidth = 24f;
    private const float ArenaHalfHeight = 16f;
    private const float MaxTrustedPlayerCoordinate = 4096f;
    private const int MaxVoxelCoordinate = 4096;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(10);

    private readonly UdpClient _udp;
    private readonly PostgresVoxelEditStore _voxelEditStore;
    private readonly RtcSignalingHub _signalingHub;
    private readonly ConcurrentDictionary<string, Player> _playersByClientKey = new();
    private readonly List<VoxelEdit> _voxelEdits = new();
    private readonly object _voxelEditLock = new();
    private readonly SemaphoreSlim _voxelEditSaveLock = new(1, 1);
    private int _nextPlayerId;
    private long _nextVoxelEditSequence;
    private long _tick;

    public BasicUdpGameServer(
        int port,
        PostgresVoxelEditStore voxelEditStore,
        IReadOnlyCollection<VoxelEdit> persistedVoxelEdits,
        RtcSignalingHub signalingHub)
    {
        _voxelEditStore = voxelEditStore;
        _signalingHub = signalingHub;
        _voxelEdits.AddRange(persistedVoxelEdits.OrderBy(edit => edit.Sequence));
        _nextVoxelEditSequence = _voxelEdits.Count == 0 ? 0 : _voxelEdits[^1].Sequence;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));

        Console.WriteLine($"Loaded {_voxelEdits.Count} persisted voxel edit(s).");
        Console.WriteLine($"UDP game server listening on 0.0.0.0:{port}");
        Console.WriteLine("Press Ctrl+C to stop.");
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var receiveTask = ReceiveLoopAsync(cancellationToken);
        var tickTask = TickLoopAsync(cancellationToken);

        try
        {
            await Task.WhenAll(receiveTask, tickTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal Ctrl+C shutdown path.
        }
    }

    public void Dispose()
    {
        _voxelEditSaveLock.Dispose();
        _udp.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await _udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var endpointKey = GetEndpointKey(result.RemoteEndPoint);
            var message = Encoding.UTF8.GetString(result.Buffer).Trim();

            if (message.Length == 0)
            {
                continue;
            }

            await HandleMessageAsync(endpointKey, result.RemoteEndPoint, message, cancellationToken);
        }
    }

    private async Task TickLoopAsync(CancellationToken cancellationToken)
    {
        var tickDelay = TimeSpan.FromSeconds(1f / TickRate);
        var lastTickTime = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(tickDelay, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var deltaTime = (float)(now - lastTickTime).TotalSeconds;
            lastTickTime = now;

            Simulate(deltaTime, now);
            BroadcastSnapshot(now);
        }
    }

    private async Task HandleMessageAsync(string endpointKey, IPEndPoint endpoint, string message, CancellationToken cancellationToken)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return;
        }

        switch (parts[0].ToUpperInvariant())
        {
            case "HELLO":
                var requestedName = parts.Length > 1 ? parts[1] : "Player";
                RegisterOrRefreshPlayer(endpointKey, endpoint, requestedName, sendWelcome: true);
                break;

            case "HELLO2":
                if (parts.Length < 2)
                {
                    return;
                }

                var helloClientKey = GetSessionKey(parts[1], endpointKey);
                var requestedProtocolName = parts.Length > 2 ? parts[2] : "Player";
                var helloPlayer = RegisterOrRefreshPlayer(helloClientKey, endpoint, requestedProtocolName, sendWelcome: true);
                SendPendingVoxelEdits(helloPlayer, ParseAcknowledgedVoxelEditSequence(parts, 3));
                break;

            case "INPUT":
                HandleInput(endpointKey, endpoint, parts, xIndex: 2, yIndex: 3);
                break;

            case "INPUT2":
                if (parts.Length < 2)
                {
                    return;
                }

                var inputClientKey = GetSessionKey(parts[1], endpointKey);
                var inputPlayer = HandleInput(inputClientKey, endpoint, parts, xIndex: 3, yIndex: 4, positionXIndex: 6, positionYIndex: 7);
                SendPendingVoxelEdits(inputPlayer, ParseAcknowledgedVoxelEditSequence(parts, 5));
                break;

            case "EDIT2":
                if (parts.Length < 8)
                {
                    return;
                }

                var editClientKey = GetSessionKey(parts[1], endpointKey);
                await HandleVoxelEditAsync(editClientKey, endpoint, parts, cancellationToken);
                break;

            case "PING":
                Send(endpoint, "PONG");
                break;
        }
    }

    private Player RegisterOrRefreshPlayer(string clientKey, IPEndPoint endpoint, string requestedName, bool sendWelcome)
    {
        var now = DateTimeOffset.UtcNow;
        var isNewPlayer = !_playersByClientKey.ContainsKey(clientKey);
        var player = _playersByClientKey.GetOrAdd(clientKey, _ => CreatePlayer(endpoint, requestedName));
        var previousEndpoint = player.Endpoint;

        player.Endpoint = endpoint;
        player.LastSeen = now;

        if (sendWelcome || isNewPlayer)
        {
            Send(player.Endpoint, string.Format(
                CultureInfo.InvariantCulture,
                "WELCOME {0} {1:0} {2:0.###} {3:0.###}",
                player.Id,
                TickRate,
                player.X,
                player.Y));
        }

        if (isNewPlayer)
        {
            Console.WriteLine($"Player {player.Id} joined from {endpoint}");
        }
        else if (!Equals(previousEndpoint, endpoint))
        {
            Console.WriteLine($"Player {player.Id} endpoint changed from {previousEndpoint} to {endpoint}");
        }

        return player;
    }

    private Player HandleInput(
        string clientKey,
        IPEndPoint endpoint,
        string[] parts,
        int xIndex,
        int yIndex,
        int positionXIndex = -1,
        int positionYIndex = -1)
    {
        var existingPlayer = RegisterOrRefreshPlayer(clientKey, endpoint, "Player", sendWelcome: false);

        if (parts.Length > yIndex
            && float.TryParse(parts[xIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var inputX)
            && float.TryParse(parts[yIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var inputY))
        {
            var length = MathF.Sqrt(inputX * inputX + inputY * inputY);

            if (length > 1f)
            {
                inputX /= length;
                inputY /= length;
            }

            existingPlayer.InputX = inputX;
            existingPlayer.InputY = inputY;
        }

        if (positionXIndex >= 0
            && positionYIndex >= 0
            && parts.Length > positionYIndex
            && float.TryParse(parts[positionXIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var positionX)
            && float.TryParse(parts[positionYIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var positionY))
        {
            existingPlayer.X = ClampTrustedPlayerCoordinate(positionX);
            existingPlayer.Y = ClampTrustedPlayerCoordinate(positionY);
            existingPlayer.UsesTrustedClientPosition = true;
        }

        return existingPlayer;
    }

    private async Task HandleVoxelEditAsync(string clientKey, IPEndPoint endpoint, string[] parts, CancellationToken cancellationToken)
    {
        var player = RegisterOrRefreshPlayer(clientKey, endpoint, "Player", sendWelcome: false);
        SendPendingVoxelEdits(player, ParseAcknowledgedVoxelEditSequence(parts, 8));

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clientEditSequence)
            || !TryParseVoxelEditAction(parts[3], out var action)
            || !TryParseVoxelCoordinate(parts[4], out var x)
            || !TryParseVoxelCoordinate(parts[5], out var y)
            || !TryParseVoxelCoordinate(parts[6], out var z))
        {
            return;
        }

        if (player.SeenVoxelEditClientSequences.Contains(clientEditSequence))
        {
            return;
        }

        VoxelEdit edit;

        try
        {
            edit = await AddVoxelEditAsync(player.Id, action, x, y, z, SanitizeVoxelType(parts[7]), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Failed to persist voxel edit from player {player.Id}: {exception.Message}");
            Send(endpoint, "ERROR EDIT_SAVE_FAILED");
            return;
        }

        player.SeenVoxelEditClientSequences.Add(clientEditSequence);
        BroadcastVoxelEdit(edit);
    }

    private Player CreatePlayer(IPEndPoint endpoint, string requestedName)
    {
        var id = Interlocked.Increment(ref _nextPlayerId);
        var spawnAngle = id * MathF.PI * 0.65f;

        return new Player
        {
            Id = id,
            Endpoint = endpoint,
            Name = SanitizeName(requestedName),
            X = MathF.Cos(spawnAngle) * 2f,
            Y = MathF.Sin(spawnAngle) * 2f,
            LastSeen = DateTimeOffset.UtcNow
        };
    }

    private void Simulate(float deltaTime, DateTimeOffset now)
    {
        foreach (var (clientKey, player) in _playersByClientKey)
        {
            if (now - player.LastSeen > ClientTimeout)
            {
                _playersByClientKey.TryRemove(clientKey, out _);
                _signalingHub.RemovePlayer(player.Id);
                Console.WriteLine($"Player {player.Id} timed out.");
                continue;
            }

            if (!player.UsesTrustedClientPosition)
            {
                player.X = Math.Clamp(player.X + player.InputX * PlayerSpeed * deltaTime, -ArenaHalfWidth, ArenaHalfWidth);
                player.Y = Math.Clamp(player.Y + player.InputY * PlayerSpeed * deltaTime, -ArenaHalfHeight, ArenaHalfHeight);
            }
        }
    }

    private async Task<VoxelEdit> AddVoxelEditAsync(int playerId, string action, int x, int y, int z, string voxelType, CancellationToken cancellationToken)
    {
        await _voxelEditSaveLock.WaitAsync(cancellationToken);

        try
        {
            VoxelEdit edit;

            lock (_voxelEditLock)
            {
                edit = new VoxelEdit(
                    _nextVoxelEditSequence + 1,
                    playerId,
                    action,
                    x,
                    y,
                    z,
                    voxelType);
            }

            await _voxelEditStore.SaveVoxelEditAsync(edit, cancellationToken);

            lock (_voxelEditLock)
            {
                _voxelEdits.Add(edit);
                _nextVoxelEditSequence = Math.Max(_nextVoxelEditSequence, edit.Sequence);
            }

            return edit;
        }
        finally
        {
            _voxelEditSaveLock.Release();
        }
    }

    private void BroadcastVoxelEdit(VoxelEdit edit)
    {
        var message = FormatVoxelEdit(edit);
        var players = _playersByClientKey.Values.ToArray();

        foreach (var player in players)
        {
            Send(player.Endpoint, message);
        }
    }

    private void SendPendingVoxelEdits(Player player, long acknowledgedSequence)
    {
        player.LastVoxelEditAcknowledged = Math.Max(player.LastVoxelEditAcknowledged, acknowledgedSequence);

        VoxelEdit[] pendingEdits;

        lock (_voxelEditLock)
        {
            if (player.LastVoxelEditAcknowledged >= _nextVoxelEditSequence)
            {
                return;
            }

            pendingEdits = _voxelEdits
                .Where(edit => edit.Sequence > player.LastVoxelEditAcknowledged)
                .ToArray();
        }

        foreach (var edit in pendingEdits)
        {
            Send(player.Endpoint, FormatVoxelEdit(edit));
        }
    }

    private static string FormatVoxelEdit(VoxelEdit edit)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "EDIT {0} {1} {2} {3} {4} {5} {6}",
            edit.Sequence,
            edit.PlayerId,
            edit.Action,
            edit.X,
            edit.Y,
            edit.Z,
            edit.VoxelType);
    }

    private void BroadcastSnapshot(DateTimeOffset now)
    {
        var players = _playersByClientKey.Values
            .OrderBy(player => player.Id)
            .ToArray();

        if (players.Length == 0)
        {
            return;
        }

        var tick = Interlocked.Increment(ref _tick);

        foreach (var recipient in players)
        {
            Send(recipient.Endpoint, FormatSnapshotForRecipient(players, recipient, tick, now));
        }
    }

    private static string FormatSnapshotForRecipient(Player[] players, Player recipient, long tick, DateTimeOffset now)
    {
        var builder = new StringBuilder();
        builder
            .Append("STATE ")
            .Append(tick)
            .Append(' ')
            .Append(now.ToUnixTimeMilliseconds());

        foreach (var player in players)
        {
            if (player.Id == recipient.Id)
            {
                continue;
            }

            builder
                .Append(' ')
                .Append(player.Id)
                .Append(' ')
                .Append(player.X.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(player.Y.ToString("0.###", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private void Send(IPEndPoint endpoint, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        _udp.Send(bytes, bytes.Length, endpoint);
    }

    public bool TryGetPlayerIdForSession(string sessionId, int claimedPlayerId, out int playerId)
    {
        playerId = 0;
        var sessionKey = GetSessionKey(sessionId, fallbackKey: string.Empty);

        if (sessionKey.Length == 0
            || !_playersByClientKey.TryGetValue(sessionKey, out var player)
            || claimedPlayerId != player.Id)
        {
            return false;
        }

        playerId = player.Id;
        player.LastSeen = DateTimeOffset.UtcNow;
        return true;
    }

    private static string GetEndpointKey(IPEndPoint endpoint)
    {
        return $"endpoint:{endpoint.Address}:{endpoint.Port}";
    }

    private static string GetSessionKey(string sessionId, string fallbackKey)
    {
        var sanitizedSessionId = SanitizeSessionId(sessionId);
        return sanitizedSessionId.Length == 0 ? fallbackKey : $"session:{sanitizedSessionId}";
    }

    private static string SanitizeSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(64);

        foreach (var character in sessionId.Trim())
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                continue;
            }

            builder.Append(character);

            if (builder.Length >= 64)
            {
                break;
            }
        }

        return builder.ToString();
    }

    private static string SanitizeName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            return "Player";
        }

        return trimmed.Length > 16 ? trimmed[..16] : trimmed;
    }

    private static long ParseAcknowledgedVoxelEditSequence(string[] parts, int index)
    {
        return parts.Length > index
            && long.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence)
            ? Math.Max(0, sequence)
            : 0;
    }

    private static float ClampTrustedPlayerCoordinate(float coordinate)
    {
        if (float.IsNaN(coordinate) || float.IsInfinity(coordinate))
        {
            return 0f;
        }

        return Math.Clamp(coordinate, -MaxTrustedPlayerCoordinate, MaxTrustedPlayerCoordinate);
    }

    private static bool TryParseVoxelCoordinate(string value, out int coordinate)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out coordinate)
            && Math.Abs(coordinate) <= MaxVoxelCoordinate)
        {
            return true;
        }

        coordinate = 0;
        return false;
    }

    private static bool TryParseVoxelEditAction(string value, out string action)
    {
        switch (value.ToUpperInvariant())
        {
            case "PLACE":
                action = "PLACE";
                return true;

            case "REMOVE":
                action = "REMOVE";
                return true;

            default:
                action = string.Empty;
                return false;
        }
    }

    private static string SanitizeVoxelType(string voxelType)
    {
        if (string.IsNullOrWhiteSpace(voxelType))
        {
            return "marker";
        }

        var builder = new StringBuilder(32);

        foreach (var character in voxelType.Trim())
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                continue;
            }

            builder.Append(character);

            if (builder.Length >= 32)
            {
                break;
            }
        }

        return builder.Length == 0 ? "marker" : builder.ToString();
    }

    private sealed class Player
    {
        public required int Id { get; init; }
        public required IPEndPoint Endpoint { get; set; }
        public required string Name { get; init; }
        public HashSet<int> SeenVoxelEditClientSequences { get; } = new();
        public float X { get; set; }
        public float Y { get; set; }
        public float InputX { get; set; }
        public float InputY { get; set; }
        public bool UsesTrustedClientPosition { get; set; }
        public long LastVoxelEditAcknowledged { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}

internal sealed class RtcSignalingHub : IDisposable
{
    private const int ReceiveBufferBytes = 4096;
    private const int MaxTextMessageBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RtcSignalingOptions _options;
    private readonly ConcurrentDictionary<int, RtcClientConnection> _clientsByPlayerId = new();
    private readonly ConcurrentDictionary<int, RtcMediaState> _mediaStateByPlayerId = new();

    public RtcSignalingHub(RtcSignalingOptions options)
    {
        _options = options;
    }

    public async Task HandleWebSocketAsync(
        HttpContext context,
        BasicUdpGameServer gameServer,
        CancellationToken cancellationToken)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var helloJson = await ReceiveTextMessageAsync(webSocket, cancellationToken);

        if (helloJson == null
            || !TryParseHello(helloJson, out var sessionId, out var claimedPlayerId)
            || !gameServer.TryGetPlayerIdForSession(sessionId, claimedPlayerId, out var playerId))
        {
            await CloseSocketAsync(webSocket, "Invalid RTC hello.", cancellationToken);
            return;
        }

        var connection = new RtcClientConnection(playerId, webSocket);
        var previousConnection = _clientsByPlayerId.AddOrUpdate(
            playerId,
            connection,
            (playerKey, existing) =>
            {
                _ = existing.CloseAsync("Replaced by a newer RTC connection.");
                return connection;
            });

        if (!ReferenceEquals(previousConnection, connection))
        {
            _ = previousConnection.CloseAsync("Replaced by a newer RTC connection.");
        }

        Console.WriteLine($"RTC signaling connected for player {playerId}.");

        try
        {
            await SendJsonAsync(connection, new
            {
                type = "config",
                playerId,
                iceServers = _options.CreateIceServers(DateTimeOffset.UtcNow)
            }, cancellationToken);
            await SendMediaStateAsync(connection, cancellationToken);
            await BroadcastMediaStateAsync(cancellationToken);

            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveTextMessageAsync(webSocket, cancellationToken);

                if (message == null)
                {
                    break;
                }

                await ProcessClientMessageAsync(connection, message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            Console.WriteLine($"RTC signaling websocket failed for player {playerId}: {exception.Message}");
        }
        finally
        {
            if (_clientsByPlayerId.TryGetValue(playerId, out var current)
                && ReferenceEquals(current, connection))
            {
                _clientsByPlayerId.TryRemove(playerId, out _);
                _mediaStateByPlayerId.TryRemove(playerId, out _);
                await BroadcastMediaStateAsync(CancellationToken.None);
            }

            Console.WriteLine($"RTC signaling disconnected for player {playerId}.");
        }
    }

    public void RemovePlayer(int playerId)
    {
        _mediaStateByPlayerId.TryRemove(playerId, out _);

        if (_clientsByPlayerId.TryRemove(playerId, out var connection))
        {
            _ = connection.CloseAsync("Player timed out.");
        }

        _ = BroadcastMediaStateAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        foreach (var connection in _clientsByPlayerId.Values)
        {
            _ = connection.CloseAsync("Server shutting down.");
        }
    }

    private async Task ProcessClientMessageAsync(
        RtcClientConnection connection,
        string message,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        var type = GetString(root, "type");

        switch (type)
        {
            case "media":
                var enabled = root.TryGetProperty("enabled", out var enabledElement)
                    && enabledElement.ValueKind == JsonValueKind.True;
                var videoRotation = root.TryGetProperty("videoRotation", out var videoRotationElement)
                    && videoRotationElement.TryGetInt32(out var parsedVideoRotation)
                    ? NormalizeVideoRotation(parsedVideoRotation)
                    : 0;
                var videoMirrored = root.TryGetProperty("videoMirrored", out var videoMirroredElement)
                    && videoMirroredElement.ValueKind == JsonValueKind.True;

                if (enabled)
                {
                    _mediaStateByPlayerId[connection.PlayerId] = new RtcMediaState(
                        Enabled: true,
                        VideoRotation: videoRotation,
                        VideoMirrored: videoMirrored);
                }
                else
                {
                    _mediaStateByPlayerId.TryRemove(connection.PlayerId, out _);
                }

                await BroadcastMediaStateAsync(cancellationToken);
                break;

            case "signal":
                if (!root.TryGetProperty("to", out var toElement)
                    || !toElement.TryGetInt32(out var targetPlayerId)
                    || !root.TryGetProperty("payload", out var payload)
                    || !_clientsByPlayerId.TryGetValue(targetPlayerId, out var targetConnection))
                {
                    return;
                }

                await SendJsonAsync(targetConnection, new
                {
                    type = "signal",
                    from = connection.PlayerId,
                    payload
                }, cancellationToken);
                break;
        }
    }

    private async Task BroadcastMediaStateAsync(CancellationToken cancellationToken)
    {
        var clients = _clientsByPlayerId.Values.ToArray();

        foreach (var client in clients)
        {
            await SendMediaStateAsync(client, cancellationToken);
        }
    }

    private Task SendMediaStateAsync(RtcClientConnection connection, CancellationToken cancellationToken)
    {
        var players = _clientsByPlayerId.Keys
            .OrderBy(playerId => playerId)
            .Select(playerId => new
            {
                playerId,
                enabled = _mediaStateByPlayerId.TryGetValue(playerId, out var mediaState) && mediaState.Enabled,
                videoRotation = mediaState?.VideoRotation ?? 0,
                videoMirrored = mediaState?.VideoMirrored ?? false
            })
            .ToArray();

        return SendJsonAsync(connection, new
        {
            type = "media-state",
            players
        }, cancellationToken);
    }

    private static async Task SendJsonAsync(
        RtcClientConnection connection,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await connection.SendLock.WaitAsync(cancellationToken);

        try
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                await connection.Socket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferBytes];
        using var memory = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidOperationException("RTC signaling only accepts text websocket messages.");
            }

            memory.Write(buffer, 0, result.Count);

            if (memory.Length > MaxTextMessageBytes)
            {
                throw new InvalidOperationException("RTC signaling message was too large.");
            }

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }
    }

    private static bool TryParseHello(string message, out string sessionId, out int playerId)
    {
        sessionId = string.Empty;
        playerId = 0;

        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (GetString(root, "type") != "hello"
            || !root.TryGetProperty("sessionId", out var sessionElement)
            || sessionElement.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("playerId", out var playerElement)
            || !playerElement.TryGetInt32(out playerId))
        {
            return false;
        }

        sessionId = sessionElement.GetString() ?? string.Empty;
        return sessionId.Length > 0 && playerId > 0;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int NormalizeVideoRotation(int degrees)
    {
        var normalized = degrees % 360;

        if (normalized < 0)
        {
            normalized += 360;
        }

        return ((normalized + 45) / 90 * 90) % 360;
    }

    private static Task CloseSocketAsync(WebSocket socket, string reason, CancellationToken cancellationToken)
    {
        return socket.State == WebSocketState.Open
            ? socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, cancellationToken)
            : Task.CompletedTask;
    }

    private sealed class RtcClientConnection
    {
        public RtcClientConnection(int playerId, WebSocket socket)
        {
            PlayerId = playerId;
            Socket = socket;
        }

        public int PlayerId { get; }
        public WebSocket Socket { get; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public async Task CloseAsync(string reason)
        {
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
                }
            }
            catch (WebSocketException)
            {
            }
        }
    }

    private sealed record RtcMediaState(bool Enabled, int VideoRotation, bool VideoMirrored);
}

internal sealed class RtcSignalingOptions
{
    private const int DefaultTurnPort = 3478;
    private static readonly TimeSpan DefaultCredentialLifetime = TimeSpan.FromHours(4);

    public RtcSignalingOptions(string turnHost, string turnRealm, string turnStaticAuthSecret)
    {
        TurnHost = turnHost;
        TurnRealm = turnRealm;
        TurnStaticAuthSecret = turnStaticAuthSecret;
    }

    public string TurnHost { get; }
    public string TurnRealm { get; }
    public string TurnStaticAuthSecret { get; }

    public static RtcSignalingOptions FromEnvironment()
    {
        var appDomain = Environment.GetEnvironmentVariable("APP_DOMAIN");
        var realm = FirstNonEmpty(
            Environment.GetEnvironmentVariable("TURN_REALM"),
            appDomain,
            "localhost");
        var host = FirstNonEmpty(
            Environment.GetEnvironmentVariable("TURN_HOST"),
            realm);
        var secret = FirstNonEmpty(
            Environment.GetEnvironmentVariable("TURN_STATIC_AUTH_SECRET"),
            "change_me");

        return new RtcSignalingOptions(host, realm, secret);
    }

    public RtcIceServerDto[] CreateIceServers(DateTimeOffset now)
    {
        var expiresAt = now.Add(DefaultCredentialLifetime).ToUnixTimeSeconds();
        var username = expiresAt.ToString(CultureInfo.InvariantCulture);

        return
        [
            new RtcIceServerDto(
                [
                    $"stun:{TurnHost}:{DefaultTurnPort}",
                    $"turn:{TurnHost}:{DefaultTurnPort}?transport=udp",
                    $"turn:{TurnHost}:{DefaultTurnPort}?transport=tcp"
                ],
                username,
                CreateTurnCredential(username, TurnStaticAuthSecret))
        ];
    }

    internal static string CreateTurnCredential(string username, string staticAuthSecret)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(staticAuthSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}

internal sealed record RtcIceServerDto(string[] Urls, string Username, string Credential);

internal sealed class PostgresVoxelEditStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _worldId;

    public PostgresVoxelEditStore(string connectionString, string worldId)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _worldId = worldId;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS voxel_edits (
                world_id text NOT NULL,
                sequence bigint NOT NULL,
                player_id integer NOT NULL,
                action text NOT NULL CHECK (action IN ('PLACE', 'REMOVE')),
                x integer NOT NULL,
                y integer NOT NULL,
                z integer NOT NULL,
                voxel_type text NOT NULL,
                chunk_x integer NOT NULL,
                chunk_y integer NOT NULL,
                chunk_z integer NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (world_id, sequence)
            );

            CREATE INDEX IF NOT EXISTS voxel_edits_world_chunk_idx
                ON voxel_edits (world_id, chunk_x, chunk_y, chunk_z);

            CREATE TABLE IF NOT EXISTS changed_chunks (
                world_id text NOT NULL,
                chunk_x integer NOT NULL,
                chunk_y integer NOT NULL,
                chunk_z integer NOT NULL,
                latest_sequence bigint NOT NULL,
                updated_at timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (world_id, chunk_x, chunk_y, chunk_z)
            );
            """,
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
        Console.WriteLine("Postgres voxel persistence ready.");
    }

    public async Task<IReadOnlyList<VoxelEdit>> LoadVoxelEditsAsync(CancellationToken cancellationToken)
    {
        var edits = new List<VoxelEdit>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT sequence, player_id, action, x, y, z, voxel_type
            FROM voxel_edits
            WHERE world_id = @world_id
            ORDER BY sequence;
            """,
            connection);

        command.Parameters.AddWithValue("world_id", _worldId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            edits.Add(new VoxelEdit(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetString(6)));
        }

        return edits;
    }

    public async Task SaveVoxelEditAsync(VoxelEdit edit, CancellationToken cancellationToken)
    {
        var chunk = VoxelChunkMath.FromVoxelCell(edit.X, edit.Y, edit.Z);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertCommand = new NpgsqlCommand(
            """
            INSERT INTO voxel_edits (
                world_id,
                sequence,
                player_id,
                action,
                x,
                y,
                z,
                voxel_type,
                chunk_x,
                chunk_y,
                chunk_z
            )
            VALUES (
                @world_id,
                @sequence,
                @player_id,
                @action,
                @x,
                @y,
                @z,
                @voxel_type,
                @chunk_x,
                @chunk_y,
                @chunk_z
            );
            """,
            connection,
            transaction))
        {
            insertCommand.Parameters.AddWithValue("world_id", _worldId);
            insertCommand.Parameters.AddWithValue("sequence", edit.Sequence);
            insertCommand.Parameters.AddWithValue("player_id", edit.PlayerId);
            insertCommand.Parameters.AddWithValue("action", edit.Action);
            insertCommand.Parameters.AddWithValue("x", edit.X);
            insertCommand.Parameters.AddWithValue("y", edit.Y);
            insertCommand.Parameters.AddWithValue("z", edit.Z);
            insertCommand.Parameters.AddWithValue("voxel_type", edit.VoxelType);
            insertCommand.Parameters.AddWithValue("chunk_x", chunk.X);
            insertCommand.Parameters.AddWithValue("chunk_y", chunk.Y);
            insertCommand.Parameters.AddWithValue("chunk_z", chunk.Z);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var upsertCommand = new NpgsqlCommand(
            """
            INSERT INTO changed_chunks (
                world_id,
                chunk_x,
                chunk_y,
                chunk_z,
                latest_sequence,
                updated_at
            )
            VALUES (
                @world_id,
                @chunk_x,
                @chunk_y,
                @chunk_z,
                @latest_sequence,
                now()
            )
            ON CONFLICT (world_id, chunk_x, chunk_y, chunk_z)
            DO UPDATE SET
                latest_sequence = GREATEST(changed_chunks.latest_sequence, EXCLUDED.latest_sequence),
                updated_at = now();
            """,
            connection,
            transaction))
        {
            upsertCommand.Parameters.AddWithValue("world_id", _worldId);
            upsertCommand.Parameters.AddWithValue("chunk_x", chunk.X);
            upsertCommand.Parameters.AddWithValue("chunk_y", chunk.Y);
            upsertCommand.Parameters.AddWithValue("chunk_z", chunk.Z);
            upsertCommand.Parameters.AddWithValue("latest_sequence", edit.Sequence);

            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _dataSource.DisposeAsync();
    }
}

internal static class VoxelChunkMath
{
    public const int ChunkSize = 16;

    public static VoxelChunkCoordinate FromVoxelCell(int x, int y, int z)
    {
        return new VoxelChunkCoordinate(
            FloorDiv(x, ChunkSize),
            FloorDiv(y, ChunkSize),
            FloorDiv(z, ChunkSize));
    }

    public static int FloorDiv(int value, int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor), "Divisor must be positive.");
        }

        var quotient = Math.DivRem(value, divisor, out var remainder);
        return remainder < 0 ? quotient - 1 : quotient;
    }
}

internal readonly record struct VoxelChunkCoordinate(int X, int Y, int Z);

internal sealed record VoxelEdit(
    long Sequence,
    int PlayerId,
    string Action,
    int X,
    int Y,
    int Z,
    string VoxelType);

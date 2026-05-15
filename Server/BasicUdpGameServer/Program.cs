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

await using var accountStore = new PostgresAccountStore(databaseConnectionString);
await accountStore.InitializeAsync(shutdown.Token);
using var authService = new GameAuthService(
    accountStore,
    AppleAuthOptions.FromEnvironment(),
    GoogleAuthOptions.FromEnvironment());
using var signalingHub = new RtcSignalingHub(RtcSignalingOptions.FromEnvironment());
using var server = new BasicUdpGameServer(port, voxelEditStore, persistedVoxelEdits, signalingHub, authService);
await using var webApp = CreateWebApplication(httpPort, signalingHub, server, authService);

try
{
    await Task.WhenAll(server.RunAsync(shutdown.Token), webApp.RunAsync(shutdown.Token));
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    // Normal Ctrl+C shutdown path.
}

static WebApplication CreateWebApplication(
    int httpPort,
    RtcSignalingHub signalingHub,
    BasicUdpGameServer gameServer,
    GameAuthService authService)
{
    var builder = WebApplication.CreateBuilder(Array.Empty<string>());
    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(httpPort));
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    var app = builder.Build();
    app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

    app.MapGet("/health", () => Results.Text("ok\n", "text/plain"));
    app.MapPost("/auth/guest", async (HttpContext context) =>
    {
        var request = await ReadJsonBodyAsync<GuestAuthRequest>(context, context.RequestAborted);

        if (request == null || string.IsNullOrWhiteSpace(request.installId))
        {
            return Results.BadRequest(new AuthErrorResponse("invalid_request", "installId is required."));
        }

        var response = await authService.SignInGuestAsync(request, context.RequestAborted);
        return Results.Json(response, JsonDefaults.Options);
    });
    app.MapPost("/auth/refresh", async (HttpContext context) =>
    {
        var request = await ReadJsonBodyAsync<RefreshAuthRequest>(context, context.RequestAborted);

        if (request == null || string.IsNullOrWhiteSpace(request.refreshToken))
        {
            return Results.BadRequest(new AuthErrorResponse("invalid_request", "refreshToken is required."));
        }

        var response = await authService.RefreshAsync(request, context.RequestAborted);
        return response == null
            ? Results.Unauthorized()
            : Results.Json(response, JsonDefaults.Options);
    });
    app.MapPost("/auth/apple", async (HttpContext context) =>
    {
        var request = await ReadJsonBodyAsync<AppleAuthRequest>(context, context.RequestAborted);

        if (request == null || string.IsNullOrWhiteSpace(request.idToken))
        {
            return Results.BadRequest(new AuthErrorResponse("invalid_request", "idToken is required."));
        }

        var currentAccountId = await authService.TryGetAccountIdFromAuthorizationHeaderAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var result = await authService.SignInWithAppleAsync(request, currentAccountId, context.RequestAborted);

        return result.IsSuccess
            ? Results.Json(result.Response, JsonDefaults.Options)
            : Results.BadRequest(new AuthErrorResponse(result.ErrorCode, result.ErrorMessage));
    });
    app.MapPost("/auth/google", async (HttpContext context) =>
    {
        var request = await ReadJsonBodyAsync<GoogleAuthRequest>(context, context.RequestAborted);

        if (request == null || string.IsNullOrWhiteSpace(request.idToken))
        {
            return Results.BadRequest(new AuthErrorResponse("invalid_request", "idToken is required."));
        }

        var currentAccountId = await authService.TryGetAccountIdFromAuthorizationHeaderAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var result = await authService.SignInWithGoogleAsync(request, currentAccountId, context.RequestAborted);

        return result.IsSuccess
            ? Results.Json(result.Response, JsonDefaults.Options)
            : Results.BadRequest(new AuthErrorResponse(result.ErrorCode, result.ErrorMessage));
    });
    app.MapGet("/auth/me", async (HttpContext context) =>
    {
        var accountId = await authService.TryGetAccountIdFromAuthorizationHeaderAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);

        if (accountId == null)
        {
            return Results.Unauthorized();
        }

        var account = await authService.GetAccountAsync(accountId.Value, context.RequestAborted);
        return account == null
            ? Results.Unauthorized()
            : Results.Json(account, JsonDefaults.Options);
    });
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

static async Task<T?> ReadJsonBodyAsync<T>(HttpContext context, CancellationToken cancellationToken)
{
    try
    {
        return await JsonSerializer.DeserializeAsync<T>(
            context.Request.Body,
            JsonDefaults.Options,
            cancellationToken);
    }
    catch (JsonException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return default;
    }
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
    private readonly GameAuthService _authService;
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
        RtcSignalingHub signalingHub,
        GameAuthService authService)
    {
        _voxelEditStore = voxelEditStore;
        _signalingHub = signalingHub;
        _authService = authService;
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

                if (!_authService.IsGameSessionActive(parts[1]))
                {
                    Send(endpoint, "ERROR AUTH_REQUIRED");
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

                if (!_authService.IsGameSessionActive(parts[1]))
                {
                    Send(endpoint, "ERROR AUTH_REQUIRED");
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

                if (!_authService.IsGameSessionActive(parts[1]))
                {
                    Send(endpoint, "ERROR AUTH_REQUIRED");
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
                _ = _signalingHub.BroadcastPlayerNamesAsync(GetPlayerNames(), CancellationToken.None);
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

    public PlayerNameDto[] GetPlayerNames()
    {
        return _playersByClientKey.Values
            .OrderBy(player => player.Id)
            .Select(player => new PlayerNameDto(player.Id, player.Name))
            .ToArray();
    }

    public string GetPlayerDisplayName(int playerId)
    {
        return _playersByClientKey.Values.FirstOrDefault(player => player.Id == playerId)?.Name ?? "Player";
    }

    public NameChangeResult TryChangePlayerDisplayName(int playerId, string requestedName, DateTimeOffset now)
    {
        var player = _playersByClientKey.Values.FirstOrDefault(candidate => candidate.Id == playerId);

        if (player == null)
        {
            return new NameChangeResult(false, "Player", 0);
        }

        var displayName = RealtimeTextPolicy.SanitizeDisplayName(requestedName);

        if (string.Equals(player.Name, displayName, StringComparison.Ordinal))
        {
            return new NameChangeResult(true, player.Name, 0);
        }

        var retryAfterSeconds = RealtimeTextPolicy.GetNameChangeRetryAfterSeconds(player.LastNameChangedAt, now);

        if (retryAfterSeconds > 0)
        {
            return new NameChangeResult(false, player.Name, retryAfterSeconds);
        }

        player.Name = displayName;
        player.LastNameChangedAt = now;
        return new NameChangeResult(true, player.Name, 0);
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

    private static string SanitizeName(string name) => RealtimeTextPolicy.SanitizeDisplayName(name);

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
        public required string Name { get; set; }
        public HashSet<int> SeenVoxelEditClientSequences { get; } = new();
        public float X { get; set; }
        public float Y { get; set; }
        public float InputX { get; set; }
        public float InputY { get; set; }
        public bool UsesTrustedClientPosition { get; set; }
        public long LastVoxelEditAcknowledged { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public DateTimeOffset? LastNameChangedAt { get; set; }
    }
}

internal readonly record struct PlayerNameDto(int PlayerId, string DisplayName);

internal readonly record struct NameChangeResult(bool Ok, string DisplayName, int RetryAfterSeconds);

internal readonly record struct ChatMessageDto(
    long Sequence,
    int PlayerId,
    string DisplayName,
    string Text,
    long SentAtUnixMs);

internal sealed class RtcSignalingHub : IDisposable
{
    private const int ReceiveBufferBytes = 4096;
    private const int MaxTextMessageBytes = 256 * 1024;
    private const int MaxRecentChatMessages = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RtcSignalingOptions _options;
    private readonly ConcurrentDictionary<int, RtcClientConnection> _clientsByPlayerId = new();
    private readonly ConcurrentDictionary<int, RtcMediaState> _mediaStateByPlayerId = new();
    private readonly RecentChatBuffer _chatHistory = new(MaxRecentChatMessages);
    private long _nextChatSequence;

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
            await SendChatHistoryAsync(connection, cancellationToken);
            await SendPlayerNamesAsync(connection, gameServer.GetPlayerNames(), cancellationToken);
            await SendMediaStateAsync(connection, cancellationToken);
            await BroadcastMediaStateAsync(cancellationToken);
            await BroadcastPlayerNamesAsync(gameServer.GetPlayerNames(), cancellationToken);

            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveTextMessageAsync(webSocket, cancellationToken);

                if (message == null)
                {
                    break;
                }

                await ProcessClientMessageAsync(connection, gameServer, message, cancellationToken);
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

    public Task BroadcastPlayerNamesAsync(PlayerNameDto[] players, CancellationToken cancellationToken)
    {
        var clients = _clientsByPlayerId.Values.ToArray();
        var tasks = clients.Select(client => SendPlayerNamesAsync(client, players, cancellationToken));
        return Task.WhenAll(tasks);
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
        BasicUdpGameServer gameServer,
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

            case "name-change":
                var nameResult = gameServer.TryChangePlayerDisplayName(
                    connection.PlayerId,
                    GetString(root, "displayName"),
                    DateTimeOffset.UtcNow);
                await SendJsonAsync(connection, new
                {
                    type = "name-result",
                    ok = nameResult.Ok,
                    displayName = nameResult.DisplayName,
                    retryAfterSeconds = nameResult.RetryAfterSeconds
                }, cancellationToken);

                if (nameResult.Ok)
                {
                    await BroadcastPlayerNamesAsync(gameServer.GetPlayerNames(), cancellationToken);
                }

                break;

            case "chat":
                var text = RealtimeTextPolicy.SanitizeChatText(GetString(root, "text"));

                if (text.Length == 0)
                {
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                var chatMessage = new ChatMessageDto(
                    Interlocked.Increment(ref _nextChatSequence),
                    connection.PlayerId,
                    gameServer.GetPlayerDisplayName(connection.PlayerId),
                    text,
                    now.ToUnixTimeMilliseconds());
                _chatHistory.Add(chatMessage);
                await BroadcastChatAsync(chatMessage, cancellationToken);
                break;
        }
    }

    private async Task BroadcastChatAsync(ChatMessageDto message, CancellationToken cancellationToken)
    {
        var clients = _clientsByPlayerId.Values.ToArray();

        foreach (var client in clients)
        {
            await SendJsonAsync(client, new
            {
                type = "chat",
                message
            }, cancellationToken);
        }
    }

    private Task SendChatHistoryAsync(RtcClientConnection connection, CancellationToken cancellationToken)
    {
        return SendJsonAsync(connection, new
        {
            type = "chat-history",
            messages = _chatHistory.GetSnapshot()
        }, cancellationToken);
    }

    private static Task SendPlayerNamesAsync(
        RtcClientConnection connection,
        PlayerNameDto[] players,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync(connection, new
        {
            type = "player-names",
            players
        }, cancellationToken);
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

internal static class RealtimeTextPolicy
{
    public const int MaxDisplayNameLength = 16;
    public const int MaxChatTextLength = 160;
    public static readonly TimeSpan NameChangeCooldown = TimeSpan.FromSeconds(60);

    public static string SanitizeDisplayName(string? value)
    {
        var sanitized = SanitizeHumanText(value, MaxDisplayNameLength);
        return sanitized.Length == 0 ? "Player" : sanitized;
    }

    public static string SanitizeChatText(string? value)
    {
        return SanitizeHumanText(value, MaxChatTextLength);
    }

    public static int GetNameChangeRetryAfterSeconds(DateTimeOffset? lastChangedAt, DateTimeOffset now)
    {
        if (lastChangedAt == null)
        {
            return 0;
        }

        var elapsed = now - lastChangedAt.Value;

        if (elapsed >= NameChangeCooldown)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling((NameChangeCooldown - elapsed).TotalSeconds));
    }

    private static string SanitizeHumanText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(maxLength);
        var previousWasWhitespace = true;

        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && builder.Length < maxLength)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            if (char.IsControl(character))
            {
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;

            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }
}

internal sealed class RecentChatBuffer
{
    private readonly int _capacity;
    private readonly Queue<ChatMessageDto> _messages = new();
    private readonly object _lock = new();

    public RecentChatBuffer(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public void Add(ChatMessageDto message)
    {
        lock (_lock)
        {
            _messages.Enqueue(message);

            while (_messages.Count > _capacity)
            {
                _messages.Dequeue();
            }
        }
    }

    public ChatMessageDto[] GetSnapshot()
    {
        lock (_lock)
        {
            return _messages.ToArray();
        }
    }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class GameAuthService : IDisposable
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(90);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(6);
    private readonly PostgresAccountStore _store;
    private readonly AppleTokenVerifier _appleTokenVerifier;
    private readonly GoogleTokenVerifier _googleTokenVerifier;
    private readonly ConcurrentDictionary<string, AuthSession> _activeGameSessionsById = new();
    private readonly ConcurrentDictionary<string, AuthSession> _activeAccessTokens = new();

    public GameAuthService(
        PostgresAccountStore store,
        AppleAuthOptions appleAuthOptions,
        GoogleAuthOptions googleAuthOptions)
    {
        _store = store;
        _appleTokenVerifier = new AppleTokenVerifier(appleAuthOptions);
        _googleTokenVerifier = new GoogleTokenVerifier(googleAuthOptions);
    }

    public async Task<AuthResponse> SignInGuestAsync(GuestAuthRequest request, CancellationToken cancellationToken)
    {
        var installId = SanitizeSubject(request.installId);
        var displayName = SanitizeDisplayName(request.displayName, "Guest");
        var accountId = await _store.GetIdentityAccountAsync("guest", installId, cancellationToken);

        if (accountId == null)
        {
            accountId = await _store.CreateAccountAsync(displayName, isGuest: true, cancellationToken);
        }

        await _store.UpsertIdentityAsync(
            accountId.Value,
            "guest",
            installId,
            email: null,
            emailVerified: false,
            displayName,
            cancellationToken);
        return await IssueTokensAsync(accountId.Value, request.platform, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshAsync(RefreshAuthRequest request, CancellationToken cancellationToken)
    {
        var accountId = await _store.ConsumeRefreshTokenAsync(HashToken(request.refreshToken), cancellationToken);
        return accountId == null
            ? null
            : await IssueTokensAsync(accountId.Value, request.platform, cancellationToken);
    }

    public async Task<AuthResult> SignInWithAppleAsync(
        AppleAuthRequest request,
        Guid? currentAccountId,
        CancellationToken cancellationToken)
    {
        AppleIdentity appleIdentity;

        try
        {
            appleIdentity = await _appleTokenVerifier.VerifyAsync(request.idToken, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return AuthResult.Failed("apple_not_configured", exception.Message);
        }
        catch (Exception exception) when (exception is JsonException || exception is CryptographicException || exception is FormatException)
        {
            return AuthResult.Failed("invalid_apple_token", "Apple identity token could not be verified.");
        }

        var existingAccountId = await _store.GetIdentityAccountAsync("apple", appleIdentity.Subject, cancellationToken);
        var accountId = existingAccountId
            ?? currentAccountId
            ?? await _store.CreateAccountAsync(
                SanitizeDisplayName(request.displayName, "Player"),
                isGuest: false,
                cancellationToken);

        var appleDisplayName = SanitizeDisplayName(request.displayName, null);
        await _store.MarkAccountSavedAsync(accountId, appleDisplayName, cancellationToken);
        await _store.UpsertIdentityAsync(
            accountId,
            "apple",
            appleIdentity.Subject,
            appleIdentity.Email,
            appleIdentity.EmailVerified,
            appleDisplayName,
            cancellationToken);

        return AuthResult.Succeeded(await IssueTokensAsync(accountId, request.platform, cancellationToken));
    }

    public async Task<AuthResult> SignInWithGoogleAsync(
        GoogleAuthRequest request,
        Guid? currentAccountId,
        CancellationToken cancellationToken)
    {
        GoogleIdentity googleIdentity;

        try
        {
            googleIdentity = await _googleTokenVerifier.VerifyAsync(request.idToken, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return AuthResult.Failed("google_not_configured", exception.Message);
        }
        catch (Exception exception) when (exception is JsonException || exception is CryptographicException || exception is FormatException)
        {
            return AuthResult.Failed("invalid_google_token", "Google identity token could not be verified.");
        }

        var googleDisplayName = SanitizeDisplayName(
            FirstNonEmpty(request.displayName, googleIdentity.DisplayName, googleIdentity.Email),
            null);
        var existingAccountId = await _store.GetIdentityAccountAsync("google", googleIdentity.Subject, cancellationToken);
        var accountId = existingAccountId
            ?? currentAccountId
            ?? await _store.CreateAccountAsync(
                googleDisplayName ?? "Player",
                isGuest: false,
                cancellationToken);

        await _store.MarkAccountSavedAsync(accountId, googleDisplayName, cancellationToken);
        await _store.UpsertIdentityAsync(
            accountId,
            "google",
            googleIdentity.Subject,
            googleIdentity.Email,
            googleIdentity.EmailVerified,
            googleDisplayName,
            cancellationToken);

        return AuthResult.Succeeded(await IssueTokensAsync(accountId, request.platform, cancellationToken));
    }

    public bool IsGameSessionActive(string gameSessionId)
    {
        if (string.IsNullOrWhiteSpace(gameSessionId))
        {
            return false;
        }

        if (!_activeGameSessionsById.TryGetValue(gameSessionId, out var session))
        {
            return false;
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _activeGameSessionsById.TryRemove(gameSessionId, out _);
            return false;
        }

        return true;
    }

    public Task<Guid?> TryGetAccountIdFromAuthorizationHeaderAsync(string authorizationHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<Guid?>(null);
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        if (!_activeAccessTokens.TryGetValue(token, out var session) || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _activeAccessTokens.TryRemove(token, out _);
            return Task.FromResult<Guid?>(null);
        }

        return Task.FromResult<Guid?>(session.AccountId);
    }

    public Task<AuthAccountResponse?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return _store.GetAccountAsync(accountId, cancellationToken);
    }

    public void Dispose()
    {
        _appleTokenVerifier.Dispose();
        _googleTokenVerifier.Dispose();
    }

    private async Task<AuthResponse> IssueTokensAsync(Guid accountId, string? platform, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var account = await _store.GetAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {accountId} was not found.");
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();
        var gameSessionId = GenerateToken();
        var accessExpiresAt = now.Add(AccessTokenLifetime);

        await _store.CreateRefreshTokenAsync(
            accountId,
            HashToken(refreshToken),
            SanitizeSubject(platform ?? "unknown"),
            now.Add(RefreshTokenLifetime),
            cancellationToken);
        await _store.CreateGameSessionAsync(
            accountId,
            gameSessionId,
            HashToken(accessToken),
            accessExpiresAt,
            cancellationToken);

        var session = new AuthSession(accountId, accessExpiresAt);
        _activeAccessTokens[accessToken] = session;
        _activeGameSessionsById[gameSessionId] = session;

        return new AuthResponse
        {
            accountId = account.Id.ToString("N"),
            displayName = account.DisplayName,
            isGuest = account.IsGuest,
            accessToken = accessToken,
            refreshToken = refreshToken,
            gameSessionId = gameSessionId,
            expiresInSeconds = (int)AccessTokenLifetime.TotalSeconds
        };
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return TokenEncoding.Base64UrlEncode(bytes);
    }

    private static byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private static string SanitizeSubject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GenerateToken();
        }

        var builder = new StringBuilder(128);

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')
            {
                builder.Append(character);
            }

            if (builder.Length >= 128)
            {
                break;
            }
        }

        return builder.Length == 0 ? GenerateToken() : builder.ToString();
    }

    private static string? SanitizeDisplayName(string? value, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 32 ? trimmed[..32] : trimmed;
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

internal sealed record AuthSession(Guid AccountId, DateTimeOffset ExpiresAt);

internal sealed class PostgresAccountStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountStore(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS accounts (
                id uuid PRIMARY KEY,
                display_name text NOT NULL,
                is_guest boolean NOT NULL DEFAULT true,
                merged_into_account_id uuid NULL REFERENCES accounts(id),
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                last_seen_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS account_identities (
                provider text NOT NULL,
                provider_subject text NOT NULL,
                account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                email text NULL,
                email_verified boolean NOT NULL DEFAULT false,
                display_name text NULL,
                linked_at timestamptz NOT NULL DEFAULT now(),
                last_login_at timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (provider, provider_subject)
            );

            CREATE INDEX IF NOT EXISTS account_identities_account_id_idx
                ON account_identities (account_id);

            CREATE TABLE IF NOT EXISTS refresh_tokens (
                id uuid PRIMARY KEY,
                account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                token_hash bytea NOT NULL UNIQUE,
                platform text NOT NULL,
                expires_at timestamptz NOT NULL,
                revoked_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS game_sessions (
                id text PRIMARY KEY,
                account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                access_token_hash bytea NOT NULL UNIQUE,
                expires_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                last_seen_at timestamptz NOT NULL DEFAULT now()
            );
            """,
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
        Console.WriteLine("Postgres account auth ready.");
    }

    public async Task<Guid?> GetIdentityAccountAsync(string provider, string providerSubject, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE account_identities
            SET last_login_at = now()
            WHERE provider = @provider AND provider_subject = @provider_subject
            RETURNING account_id;
            """,
            connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("provider_subject", providerSubject);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid accountId ? accountId : null;
    }

    public async Task<Guid> CreateAccountAsync(string? displayName, bool isGuest, CancellationToken cancellationToken)
    {
        var accountId = Guid.NewGuid();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO accounts (id, display_name, is_guest)
            VALUES (@id, @display_name, @is_guest);
            """,
            connection);
        command.Parameters.AddWithValue("id", accountId);
        command.Parameters.AddWithValue("display_name", string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName);
        command.Parameters.AddWithValue("is_guest", isGuest);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return accountId;
    }

    public async Task MarkAccountSavedAsync(Guid accountId, string? displayName, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE accounts
            SET is_guest = false,
                display_name = CASE
                    WHEN @display_name IS NOT NULL
                        AND (is_guest OR display_name = 'Guest' OR display_name = 'Player')
                    THEN @display_name
                    ELSE display_name
                END,
                updated_at = now()
            WHERE id = @account_id;
            """,
            connection);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("display_name", (object?)displayName ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertIdentityAsync(
        Guid accountId,
        string provider,
        string providerSubject,
        string? email,
        bool emailVerified,
        string? displayName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO account_identities
                (provider, provider_subject, account_id, email, email_verified, display_name)
            VALUES
                (@provider, @provider_subject, @account_id, @email, @email_verified, @display_name)
            ON CONFLICT (provider, provider_subject) DO UPDATE
            SET last_login_at = now(),
                email = COALESCE(EXCLUDED.email, account_identities.email),
                email_verified = EXCLUDED.email_verified OR account_identities.email_verified,
                display_name = COALESCE(EXCLUDED.display_name, account_identities.display_name);
            """,
            connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("provider_subject", providerSubject);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        command.Parameters.AddWithValue("email_verified", emailVerified);
        command.Parameters.AddWithValue("display_name", (object?)displayName ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateRefreshTokenAsync(
        Guid accountId,
        byte[] tokenHash,
        string platform,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO refresh_tokens (id, account_id, token_hash, platform, expires_at)
            VALUES (@id, @account_id, @token_hash, @platform, @expires_at);
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("platform", platform);
        command.Parameters.AddWithValue("expires_at", expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Guid?> ConsumeRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE refresh_tokens
            SET revoked_at = now()
            WHERE token_hash = @token_hash
                AND revoked_at IS NULL
                AND expires_at > now()
            RETURNING account_id;
            """,
            connection);
        command.Parameters.AddWithValue("token_hash", tokenHash);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid accountId ? accountId : null;
    }

    public async Task CreateGameSessionAsync(
        Guid accountId,
        string gameSessionId,
        byte[] accessTokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO game_sessions (id, account_id, access_token_hash, expires_at)
            VALUES (@id, @account_id, @access_token_hash, @expires_at);
            """,
            connection);
        command.Parameters.AddWithValue("id", gameSessionId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("access_token_hash", accessTokenHash);
        command.Parameters.AddWithValue("expires_at", expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AuthAccountResponse?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                accounts.id,
                CASE
                    WHEN accounts.display_name IN ('Guest', 'Player')
                    THEN COALESCE(
                        NULLIF(saved_identity.display_name, ''),
                        NULLIF(saved_identity.email, ''),
                        accounts.display_name)
                    ELSE accounts.display_name
                END AS display_name,
                accounts.is_guest
            FROM accounts
            LEFT JOIN LATERAL (
                SELECT display_name, email
                FROM account_identities
                WHERE account_id = accounts.id AND provider IN ('apple', 'google')
                ORDER BY last_login_at DESC
                LIMIT 1
            ) saved_identity ON true
            WHERE accounts.id = @account_id AND accounts.merged_into_account_id IS NULL;
            """,
            connection);
        command.Parameters.AddWithValue("account_id", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AuthAccountResponse
        {
            Id = reader.GetGuid(0),
            DisplayName = reader.GetString(1),
            IsGuest = reader.GetBoolean(2)
        };
    }

    public ValueTask DisposeAsync()
    {
        return _dataSource.DisposeAsync();
    }
}

internal sealed class AppleTokenVerifier : IDisposable
{
    private readonly AppleAuthOptions _options;
    private readonly HttpClient _httpClient = new();
    private AppleJwkSet? _cachedKeys;
    private DateTimeOffset _cachedKeysUntil;

    public AppleTokenVerifier(AppleAuthOptions options)
    {
        _options = options;
    }

    public async Task<AppleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (_options.AllowedAudiences.Count == 0)
        {
            throw new InvalidOperationException(
                "Apple auth is not configured. Set APPLE_CLIENT_ID to the iOS bundle id used for Sign in with Apple.");
        }

        var parts = idToken.Split('.');

        if (parts.Length != 3)
        {
            throw new FormatException("Apple id token is not a compact JWT.");
        }

        using var headerDocument = JsonDocument.Parse(TokenEncoding.Base64UrlDecode(parts[0]));
        using var payloadDocument = JsonDocument.Parse(TokenEncoding.Base64UrlDecode(parts[1]));
        var header = headerDocument.RootElement;
        var payload = payloadDocument.RootElement;
        var kid = GetJsonString(header, "kid");
        var alg = GetJsonString(header, "alg");

        if (alg != "RS256" || string.IsNullOrWhiteSpace(kid))
        {
            throw new CryptographicException("Apple id token uses an unsupported signing key.");
        }

        var keys = await GetAppleKeysAsync(cancellationToken);
        var key = keys.Keys.FirstOrDefault(candidate => candidate.Kid == kid && candidate.Kty == "RSA");

        if (key == null)
        {
            _cachedKeys = null;
            keys = await GetAppleKeysAsync(cancellationToken);
            key = keys.Keys.FirstOrDefault(candidate => candidate.Kid == kid && candidate.Kty == "RSA")
                ?? throw new CryptographicException("Apple signing key was not found.");
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = TokenEncoding.Base64UrlDecode(key.N),
            Exponent = TokenEncoding.Base64UrlDecode(key.E)
        });

        var signedBytes = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signatureBytes = TokenEncoding.Base64UrlDecode(parts[2]);

        if (!rsa.VerifyData(signedBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new CryptographicException("Apple id token signature is invalid.");
        }

        var issuer = GetJsonString(payload, "iss");
        var subject = GetJsonString(payload, "sub");
        var audience = GetJsonString(payload, "aud");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(GetJsonLong(payload, "exp"));

        if (issuer != "https://appleid.apple.com"
            || string.IsNullOrWhiteSpace(subject)
            || !_options.AllowedAudiences.Contains(audience)
            || expiresAt <= DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            throw new CryptographicException("Apple id token claims are invalid.");
        }

        return new AppleIdentity(
            subject,
            GetJsonString(payload, "email"),
            GetJsonBool(payload, "email_verified"));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<AppleJwkSet> GetAppleKeysAsync(CancellationToken cancellationToken)
    {
        if (_cachedKeys != null && _cachedKeysUntil > DateTimeOffset.UtcNow)
        {
            return _cachedKeys;
        }

        using var response = await _httpClient.GetAsync("https://appleid.apple.com/auth/keys", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        _cachedKeys = await JsonSerializer.DeserializeAsync<AppleJwkSet>(stream, JsonDefaults.Options, cancellationToken)
            ?? throw new JsonException("Apple JWK response was empty.");
        _cachedKeysUntil = DateTimeOffset.UtcNow.AddHours(24);
        return _cachedKeys;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Array => property.EnumerateArray()
                .FirstOrDefault(item => item.ValueKind == JsonValueKind.String)
                .GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private static bool GetJsonBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(property.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static long GetJsonLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out var value)
            ? value
            : 0;
    }
}

internal sealed class AppleAuthOptions
{
    public AppleAuthOptions(IReadOnlySet<string> allowedAudiences)
    {
        AllowedAudiences = allowedAudiences;
    }

    public IReadOnlySet<string> AllowedAudiences { get; }

    public static AppleAuthOptions FromEnvironment()
    {
        var audiences = FirstNonEmpty(
                Environment.GetEnvironmentVariable("APPLE_CLIENT_ID"),
                Environment.GetEnvironmentVariable("APPLE_BUNDLE_ID"))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return new AppleAuthOptions(audiences);
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

internal sealed class AppleJwkSet
{
    public AppleJwk[] Keys { get; set; } = [];
}

internal sealed class AppleJwk
{
    public string Kid { get; set; } = string.Empty;
    public string Kty { get; set; } = string.Empty;
    public string N { get; set; } = string.Empty;
    public string E { get; set; } = string.Empty;
}

internal sealed record AppleIdentity(string Subject, string? Email, bool EmailVerified);

internal sealed class GoogleTokenVerifier : IDisposable
{
    private readonly GoogleAuthOptions _options;
    private readonly HttpClient _httpClient = new();
    private GoogleJwkSet? _cachedKeys;
    private DateTimeOffset _cachedKeysUntil;

    public GoogleTokenVerifier(GoogleAuthOptions options)
    {
        _options = options;
    }

    public async Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (_options.AllowedAudiences.Count == 0)
        {
            throw new InvalidOperationException(
                "Google auth is not configured. Set GOOGLE_CLIENT_ID to the Web OAuth client id used by Android Sign in with Google.");
        }

        var parts = idToken.Split('.');

        if (parts.Length != 3)
        {
            throw new FormatException("Google id token is not a compact JWT.");
        }

        using var headerDocument = JsonDocument.Parse(TokenEncoding.Base64UrlDecode(parts[0]));
        using var payloadDocument = JsonDocument.Parse(TokenEncoding.Base64UrlDecode(parts[1]));
        var header = headerDocument.RootElement;
        var payload = payloadDocument.RootElement;
        var kid = GetJsonString(header, "kid");
        var alg = GetJsonString(header, "alg");

        if (alg != "RS256" || string.IsNullOrWhiteSpace(kid))
        {
            throw new CryptographicException("Google id token uses an unsupported signing key.");
        }

        var keys = await GetGoogleKeysAsync(cancellationToken);
        var key = keys.Keys.FirstOrDefault(candidate => candidate.Kid == kid && candidate.Kty == "RSA");

        if (key == null)
        {
            _cachedKeys = null;
            keys = await GetGoogleKeysAsync(cancellationToken);
            key = keys.Keys.FirstOrDefault(candidate => candidate.Kid == kid && candidate.Kty == "RSA")
                ?? throw new CryptographicException("Google signing key was not found.");
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = TokenEncoding.Base64UrlDecode(key.N),
            Exponent = TokenEncoding.Base64UrlDecode(key.E)
        });

        var signedBytes = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signatureBytes = TokenEncoding.Base64UrlDecode(parts[2]);

        if (!rsa.VerifyData(signedBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new CryptographicException("Google id token signature is invalid.");
        }

        var issuer = GetJsonString(payload, "iss");
        var subject = GetJsonString(payload, "sub");
        var audience = GetJsonString(payload, "aud");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(GetJsonLong(payload, "exp"));

        if (issuer is not ("accounts.google.com" or "https://accounts.google.com")
            || string.IsNullOrWhiteSpace(subject)
            || !_options.AllowedAudiences.Contains(audience)
            || expiresAt <= DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            throw new CryptographicException("Google id token claims are invalid.");
        }

        return new GoogleIdentity(
            subject,
            GetJsonString(payload, "email"),
            GetJsonBool(payload, "email_verified"),
            GetJsonString(payload, "name"));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<GoogleJwkSet> GetGoogleKeysAsync(CancellationToken cancellationToken)
    {
        if (_cachedKeys != null && _cachedKeysUntil > DateTimeOffset.UtcNow)
        {
            return _cachedKeys;
        }

        using var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/certs", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        _cachedKeys = await JsonSerializer.DeserializeAsync<GoogleJwkSet>(stream, JsonDefaults.Options, cancellationToken)
            ?? throw new JsonException("Google JWK response was empty.");

        var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromHours(1);
        _cachedKeysUntil = DateTimeOffset.UtcNow.Add(maxAge);
        return _cachedKeys;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Array => property.EnumerateArray()
                .FirstOrDefault(item => item.ValueKind == JsonValueKind.String)
                .GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private static bool GetJsonBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(property.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static long GetJsonLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out var value)
            ? value
            : 0;
    }
}

internal sealed class GoogleAuthOptions
{
    public GoogleAuthOptions(IReadOnlySet<string> allowedAudiences)
    {
        AllowedAudiences = allowedAudiences;
    }

    public IReadOnlySet<string> AllowedAudiences { get; }

    public static GoogleAuthOptions FromEnvironment()
    {
        var audiences = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
                Environment.GetEnvironmentVariable("GOOGLE_WEB_CLIENT_ID"))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return new GoogleAuthOptions(audiences);
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

internal sealed class GoogleJwkSet
{
    public GoogleJwk[] Keys { get; set; } = [];
}

internal sealed class GoogleJwk
{
    public string Kid { get; set; } = string.Empty;
    public string Kty { get; set; } = string.Empty;
    public string N { get; set; } = string.Empty;
    public string E { get; set; } = string.Empty;
}

internal sealed record GoogleIdentity(string Subject, string? Email, bool EmailVerified, string? DisplayName);

internal sealed class AuthResult
{
    private AuthResult(AuthResponse? response, string errorCode, string errorMessage)
    {
        Response = response;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public AuthResponse? Response { get; }
    public string ErrorCode { get; }
    public string ErrorMessage { get; }
    public bool IsSuccess => Response != null;

    public static AuthResult Succeeded(AuthResponse response)
    {
        return new AuthResult(response, string.Empty, string.Empty);
    }

    public static AuthResult Failed(string code, string message)
    {
        return new AuthResult(null, code, message);
    }
}

internal sealed class GuestAuthRequest
{
    public string installId { get; set; } = string.Empty;
    public string? platform { get; set; }
    public string? displayName { get; set; }
}

internal sealed class RefreshAuthRequest
{
    public string refreshToken { get; set; } = string.Empty;
    public string? platform { get; set; }
}

internal sealed class AppleAuthRequest
{
    public string idToken { get; set; } = string.Empty;
    public string? platform { get; set; }
    public string? displayName { get; set; }
}

internal sealed class GoogleAuthRequest
{
    public string idToken { get; set; } = string.Empty;
    public string? platform { get; set; }
    public string? displayName { get; set; }
}

internal sealed class AuthResponse
{
    public string accountId { get; set; } = string.Empty;
    public string displayName { get; set; } = "Player";
    public bool isGuest { get; set; }
    public string accessToken { get; set; } = string.Empty;
    public string refreshToken { get; set; } = string.Empty;
    public string gameSessionId { get; set; } = string.Empty;
    public int expiresInSeconds { get; set; }
}

internal sealed class AuthErrorResponse
{
    public AuthErrorResponse(string code, string message)
    {
        this.code = code;
        this.message = message;
    }

    public string code { get; }
    public string message { get; }
}

internal sealed class AuthAccountResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "Player";
    public bool IsGuest { get; set; }
}

internal static class TokenEncoding
{
    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}

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

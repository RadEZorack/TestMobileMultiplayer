using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort)
    ? parsedPort
    : 7777;

using var server = new BasicUdpGameServer(port);
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await server.RunAsync(shutdown.Token);

internal sealed class BasicUdpGameServer : IDisposable
{
    private const float TickRate = 30f;
    private const float PlayerSpeed = 4.5f;
    private const float ArenaHalfWidth = 9f;
    private const float ArenaHalfHeight = 5f;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(10);

    private readonly UdpClient _udp;
    private readonly ConcurrentDictionary<string, Player> _playersByClientKey = new();
    private int _nextPlayerId;
    private long _tick;

    public BasicUdpGameServer(int port)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
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

            HandleMessage(endpointKey, result.RemoteEndPoint, message);
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

    private void HandleMessage(string endpointKey, IPEndPoint endpoint, string message)
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
                RegisterOrRefreshPlayer(helloClientKey, endpoint, requestedProtocolName, sendWelcome: true);
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
                HandleInput(inputClientKey, endpoint, parts, xIndex: 3, yIndex: 4);
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
            Send(player.Endpoint, $"WELCOME {player.Id} {TickRate.ToString("0", CultureInfo.InvariantCulture)}");
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

    private void HandleInput(string clientKey, IPEndPoint endpoint, string[] parts, int xIndex, int yIndex)
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
                Console.WriteLine($"Player {player.Id} timed out.");
                continue;
            }

            player.X = Math.Clamp(player.X + player.InputX * PlayerSpeed * deltaTime, -ArenaHalfWidth, ArenaHalfWidth);
            player.Y = Math.Clamp(player.Y + player.InputY * PlayerSpeed * deltaTime, -ArenaHalfHeight, ArenaHalfHeight);
        }
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

        var builder = new StringBuilder();
        builder
            .Append("STATE ")
            .Append(Interlocked.Increment(ref _tick))
            .Append(' ')
            .Append(now.ToUnixTimeMilliseconds());

        foreach (var player in players)
        {
            builder
                .Append(' ')
                .Append(player.Id)
                .Append(' ')
                .Append(player.X.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(player.Y.ToString("0.###", CultureInfo.InvariantCulture));
        }

        var snapshot = builder.ToString();

        foreach (var player in players)
        {
            Send(player.Endpoint, snapshot);
        }
    }

    private void Send(IPEndPoint endpoint, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        _udp.Send(bytes, bytes.Length, endpoint);
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

    private sealed class Player
    {
        public required int Id { get; init; }
        public required IPEndPoint Endpoint { get; set; }
        public required string Name { get; init; }
        public float X { get; set; }
        public float Y { get; set; }
        public float InputX { get; set; }
        public float InputY { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}

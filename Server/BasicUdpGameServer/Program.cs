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
    private readonly ConcurrentDictionary<string, Player> _playersByEndpoint = new();
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
                var isNewPlayer = !_playersByEndpoint.ContainsKey(endpointKey);
                var player = _playersByEndpoint.GetOrAdd(endpointKey, _ => CreatePlayer(endpoint, requestedName));
                player.Endpoint = endpoint;
                player.LastSeen = DateTimeOffset.UtcNow;
                Send(player.Endpoint, $"WELCOME {player.Id} {TickRate.ToString("0", CultureInfo.InvariantCulture)}");

                if (isNewPlayer)
                {
                    Console.WriteLine($"Player {player.Id} joined from {endpoint}");
                }
                break;

            case "INPUT":
                if (!_playersByEndpoint.TryGetValue(endpointKey, out var existingPlayer))
                {
                    var latePlayer = _playersByEndpoint.GetOrAdd(endpointKey, _ => CreatePlayer(endpoint, "Player"));
                    Send(latePlayer.Endpoint, $"WELCOME {latePlayer.Id} {TickRate.ToString("0", CultureInfo.InvariantCulture)}");
                    existingPlayer = latePlayer;
                }

                existingPlayer.Endpoint = endpoint;
                existingPlayer.LastSeen = DateTimeOffset.UtcNow;

                if (parts.Length >= 4
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var inputX)
                    && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var inputY))
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

                break;

            case "PING":
                Send(endpoint, "PONG");
                break;
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
        foreach (var (endpointKey, player) in _playersByEndpoint)
        {
            if (now - player.LastSeen > ClientTimeout)
            {
                _playersByEndpoint.TryRemove(endpointKey, out _);
                Console.WriteLine($"Player {player.Id} timed out.");
                continue;
            }

            player.X = Math.Clamp(player.X + player.InputX * PlayerSpeed * deltaTime, -ArenaHalfWidth, ArenaHalfWidth);
            player.Y = Math.Clamp(player.Y + player.InputY * PlayerSpeed * deltaTime, -ArenaHalfHeight, ArenaHalfHeight);
        }
    }

    private void BroadcastSnapshot(DateTimeOffset now)
    {
        var players = _playersByEndpoint.Values
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
        return $"{endpoint.Address}:{endpoint.Port}";
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

# Basic UDP Multiplayer Setup

This project now has a deliberately small client/server multiplayer loop:

- Server: `Server/BasicUdpGameServer`
- Unity client scripts: `Assets/Scripts/BasicMultiplayer`
- Default UDP port: `7777`

The server is authoritative for player positions. Clients send only movement intent; the server simulates positions and broadcasts snapshots.

## 1. Run the server locally

From the project root:

```sh
dotnet run --project Server/BasicUdpGameServer -- 7777
```

Leave that Terminal window open.

## 2. Test in the Unity Editor

1. Open `Assets/Scenes/SampleScene.unity`.
2. Press Play.
3. Click `Connect` in the top-left overlay.
4. Move with WASD or arrow keys.

To test a second player, run another Unity editor instance, or make a standalone desktop build and connect both to `127.0.0.1`.

## 3. Test on an iPhone over Wi-Fi

1. Start the server on your Mac.
2. Find your Mac LAN IP:

```sh
ipconfig getifaddr en0
```

3. Build the Unity project for iOS.
4. Launch on the phone.
5. In the overlay, set `Host` to your Mac LAN IP, not `127.0.0.1`.
6. Tap `Connect`.

Your iPhone and Mac need to be on the same Wi-Fi network, and your firewall must allow UDP port `7777`.

## 4. What to build next

This is a prototype transport and game loop. Good next steps are:

- Add Relay/Lobby or your own matchmaking so phones do not need raw IP addresses.
- Add sequence numbers and interpolation buffering for smoother remote motion.
- Add authentication before accepting public traffic.
- Package the server in Docker for cloud hosting.
- Move from text packets to compact binary packets when gameplay grows.

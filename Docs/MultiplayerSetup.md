# Basic UDP Multiplayer Setup

This project now has a deliberately small client/server multiplayer loop:

- Server: `Server/BasicUdpGameServer`
- Unity client scripts: `Assets/Scripts/BasicMultiplayer`
- Default UDP port: `7777`

The server is authoritative for player positions. Clients send only movement intent; the server simulates positions and broadcasts snapshots.

When Voxel Play 3 is installed, the sample scene creates a runtime Voxel Play world. It now prefers the included `HQForest` world, enables trees/vegetation/clouds, and places a small multiplayer marker glade on the terrain.

## 1. Run the server locally

From the project root:

```sh
dotnet run --project Server/BasicUdpGameServer -- 7777
```

Leave that Terminal window open.

## 2. Test in the Unity Editor

1. Open `Assets/Scenes/SampleScene.unity`.
2. Press Play.
3. The client auto-connects to `dev.augmego.ca:7777`.
4. Drag the left half of the game view to move and the right half to turn the camera. WASD/arrow keys also work in the editor.

The connection overlay is hidden by default. For manual host/port testing, enable `showConnectionOverlay` on the `UdpGameClient` component in the Inspector.

Tap the `CAM` button, or press `C` in the editor, to cycle the camera from far chase to close chase to first person. Movement is camera-relative, so turning with the right joystick also changes the direction the left joystick considers forward.

To test a second player, run another Unity editor instance, or make a standalone desktop build and connect both to `127.0.0.1`.

## Optional nginx UDP front door

The included `docker-compose.yml` exposes nginx on HTTP port `80` and UDP port `7777`. Because nginx owns public UDP `7777`, run the standalone game server on local UDP `7778` when testing through nginx:

```sh
dotnet run --project Server/BasicUdpGameServer -- 7778
docker compose up -d
```

Then connect clients to `dev.augmego.ca` on port `7777`.

Make sure `dev.augmego.ca` resolves on the device that is running the game. A Mac `/etc/hosts` entry only affects the Mac; an iPhone needs public DNS, router/local DNS, or a direct IP address in the game overlay.

The client includes a session id in its UDP packets, and the server uses that id instead of the client's source port as the player key. This matters when traffic goes through nginx or NAT, because UDP proxy mappings can be recycled even while the game is still running.

## 3. Test on an iPhone over Wi-Fi

1. Start the server on your Mac.
2. Find your Mac LAN IP:

```sh
ipconfig getifaddr en0
```

3. Build the Unity project for iOS.
4. Launch on the phone.
5. The client auto-connects using the default host in `UdpGameClient`.

Your iPhone and Mac need to be on the same Wi-Fi network, and your firewall must allow UDP port `7777`.

## 4. What to build next

This is a prototype transport and game loop. Good next steps are:

- Add Relay/Lobby or your own matchmaking so phones do not need raw IP addresses.
- Add sequence numbers and interpolation buffering for smoother remote motion.
- Add authentication before accepting public traffic.
- Package the server in Docker for cloud hosting.
- Move from text packets to compact binary packets when gameplay grows.

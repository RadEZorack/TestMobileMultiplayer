# Basic UDP Multiplayer Setup

This project now has a deliberately small client/server multiplayer loop:

- Server: `Server/BasicUdpGameServer`
- Unity client scripts: `Assets/Scripts/BasicMultiplayer`
- Default UDP gameplay port: `7777`
- Default WebSocket signaling path: `/rtc`

The server is authoritative for player positions. Clients send only movement intent; the server simulates positions and broadcasts snapshots. Voxel block edits are also relayed through the server so every connected client applies the same place/remove sequence.

When Voxel Play 3 is installed, the sample scene creates a runtime Voxel Play world. It now prefers the included `HQForest` world, enables trees/vegetation/clouds, and places a small multiplayer marker glade on the terrain.

## 1. Run the server locally

For the Docker path that matches a simple droplet deploy, copy `.env.example` to `.env`, adjust the values, then start the full stack:

```sh
docker compose up -d --build
```

This starts Postgres, the UDP game server, nginx, and coturn. Nginx listens on HTTP `80`, HTTPS `443`, and UDP `7777`; gameplay traffic stays on UDP while WebRTC signaling uses `wss://APP_DOMAIN/rtc`.

For quick local server iteration without rebuilding the container, run only Postgres and start the .NET server directly:

```sh
docker compose up -d postgres
dotnet run --project Server/BasicUdpGameServer -- 7777
```

Leave that Terminal window open. If the full compose stack is already running, stop `nginx` and `game-server` first so they do not occupy UDP port `7777`.

The server stores voxel edits in Postgres by default using:

```text
Host=localhost;Port=5432;Database=mobile_multiplayer;Username=game;Password=game_dev_password
```

Set `GAME_DATABASE_URL` before starting the server if you want a different database. Only chunks that receive edits are written to the database. If a chunk has no `changed_chunks` row, it is treated as unmodified and the Voxel Play world seed/generated terrain is used.

For droplet-style deploys, copy `.env.example` to `.env` and set the hostname used by nginx:

```sh
APP_DOMAIN=dev.augmego.ca
POSTGRES_DB=mobile_multiplayer
POSTGRES_USER=game
POSTGRES_PASSWORD=game_dev_password
TURN_REALM=dev.augmego.ca
TURN_STATIC_AUTH_SECRET=change_me
TURN_MIN_PORT=49160
TURN_MAX_PORT=49200
APPLE_CLIENT_ID=com.Unity-Technologies.com.unity.template.urp-blank
```

Use `APP_DOMAIN=prod.augmego.ca` or another host name for a different environment. Set real database and TURN secrets before putting the stack on a public droplet. If `.env` is missing, compose defaults to the dev values in `docker-compose.yml`.

`APPLE_CLIENT_ID` must match the iOS bundle identifier configured for Sign in with Apple. The current project default is still Unity's template bundle id; change it before using real Apple auth in TestFlight/App Store builds.

For WSS, place your certificate files at:

```text
certs/fullchain.pem
certs/privkey.pem
```

For public WebRTC relay, open TCP/UDP `3478` and UDP `49160-49200` on the host firewall. If the droplet needs an explicit public address for coturn, set `TURN_EXTERNAL_IP`.

## 2. Test in the Unity Editor

1. Open `Assets/Scenes/SampleScene.unity`.
2. Press Play.
3. The client auto-connects to `dev.augmego.ca:7777`.
4. Drag the left half of the game view to move and the right half to turn the camera. WASD/arrow keys also work in the editor.

On first launch, `GameAuthClient` signs in as a guest through `/auth/guest`, stores a refresh token locally, then gives `UdpGameClient` an auth-issued game session id. The server rejects unauthenticated `HELLO2`/`INPUT2`/`EDIT2` session ids, so clients now wait for guest auth before UDP gameplay connects.

Guest players see a small `Save` button. On iOS player builds, tapping it starts Sign in with Apple, verifies the Apple identity token on the server, and links that Apple identity to the current guest account. In the Editor and non-iOS builds, the button is a placeholder until Google Play Games and Steam providers are added.

The connection overlay is hidden by default. For manual host/port testing, enable `showConnectionOverlay` on the `UdpGameClient` component in the Inspector.

Tap the `CAM` button, or press `C` in the editor, to cycle the camera from far chase to close chase to first person. Movement is camera-relative, so turning with the right joystick also changes the direction the left joystick considers forward.

Tap the `AV` button to request camera/microphone permission and share WebRTC media with peers. Remote feeds appear as hovering video panels above player capsules; tapping `AV` again closes local media and peer connections.

The publishing client sends its camera rotation metadata through signaling, so iPhone portrait camera feeds render upright on remote billboards.

On iOS, the client configures the audio session for WebRTC playback/recording and prefers speaker output. Remote voice playback is currently 2D so mobile testing is loud and predictable; spatial voice can come back once the AV path is stable.

The editor automatically patches Unity WebRTC's cached `WebRTC.cs` package file on startup/script reload so `WebRTC.Lib` uses `__Internal` only for `UNITY_IOS && !UNITY_EDITOR`. The iOS player still uses `__Internal`; the Editor needs the package's macOS `libwebrtc.dylib`. If Unity regenerates the package cache and you need to patch it manually, use the Unity menu item `Tools > Patch Unity WebRTC iOS Editor Bug` or run `Tools/patch-unity-webrtc-ios-editor.sh`.

When testing multiple desktop clients on one Mac, the physical webcam may only produce frames for one process at a time. If a desktop client cannot start camera frames, it will still join AV signaling/audio, but no blank white video panel is shown for that peer.

Aim with the center crosshair. Use the on-screen `L` button or left mouse button to remove the highlighted voxel, and the `R` button or right mouse button to place a voxel on the highlighted face. These edits are sent over UDP and replayed on other clients.

To test a second player, run another Unity editor instance, or make a standalone desktop build and connect both to `127.0.0.1`.

## Docker nginx front door

The included `docker-compose.yml` exposes nginx on HTTP `80`, HTTPS `443`, and UDP `7777`. In the Docker stack, nginx forwards UDP gameplay to the `game-server` service and proxies WebSocket signaling to the same container:

```sh
docker compose up -d --build
```

Then connect clients to `dev.augmego.ca` on port `7777`; the Unity client derives `wss://dev.augmego.ca/rtc` from the same host for WebRTC signaling.

Make sure `dev.augmego.ca` resolves on the device that is running the game. A Mac `/etc/hosts` entry only affects the Mac; an iPhone needs public DNS, router/local DNS, or a direct IP address in the game overlay.

The client includes a session id in its UDP packets, and the server uses that id instead of the client's source port as the player key. This matters when traffic goes through nginx or NAT, because UDP proxy mappings can be recycled even while the game is still running.

Voxel edits use sequence numbers. Clients include the last applied edit sequence in their regular packets, and the server resends missed edits when needed. Edits are sent as integer voxel cells and reconstructed at voxel centers on the receiving client. This is still a prototype transport, but it avoids the easiest UDP packet-loss desync.

On server startup, persisted edits are loaded from Postgres and replayed through the same sequence system, so block changes survive a server restart.

WebRTC media does not pass through nginx. Peers connect directly where possible and fall back to coturn using short-lived credentials from the game server.

## 3. Test on an iPhone over Wi-Fi

1. Start the server on your Mac.
2. Find your Mac LAN IP:

```sh
ipconfig getifaddr en0
```

3. Build the Unity project for iOS.
4. Launch on the phone.
5. The client auto-connects using the default host in `UdpGameClient`.

Your iPhone and Mac need to be on the same Wi-Fi network for local gameplay testing, and your firewall must allow UDP port `7777`. WebRTC camera/mic testing requires a physical iOS device; Unity WebRTC does not support the iOS Simulator.

## 4. What to build next

This is a prototype transport and game loop. Good next steps are:

- Add Relay/Lobby or your own matchmaking so phones do not need raw IP addresses.
- Add sequence numbers and interpolation buffering for smoother remote motion.
- Add chunk snapshot compaction once edit logs become too large.
- Add authentication before accepting public traffic.
- Add TLS/certbot or a managed load balancer for production HTTP endpoints.
- Move from text packets to compact binary packets when gameplay grows.

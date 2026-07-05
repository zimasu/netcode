Hello World!

- Unity-Development (C#)
- LAN network sync, no online services
- moderation tools (partial)
- Client-Server / Host Authority

Component-based OOP (GameObject + MonoBehaviour/NetworkBehaviour).

input: local player actions (keyboard / VR controller)
output: renders on connected machines
desired behaviour: decision on one machine becomes reality on the other machine

- Transport (Unity Transport)
- NetworkManager
- NetworkObjects
- NetworkVariables

LAN sync + one owner-authoritave object + one server-authoritative flag

Two separate computers have two separate blocks of memory. By default, there is zero relationship between them.

# Netcode Demo - Quick Notes

## What it does

One shared cube, synced over LAN. Anyone connected can click it → moves for
everyone. Server decides the real position — no desync.

## Bugs fixed (know these if asked)

- **Event leak**: forgot to unsubscribe callbacks on disconnect → stacked up
  over repeated host/disconnect cycles. Fixed with matching -=.
- **Cube snapped to 0,0,0**: NetworkVariable defaulted to zero and overwrote
  its placed position. Fixed by seeding it from the cube's actual position
  on host start.
- **Clicks did nothing**: project uses new Input System, which doesn't
  support legacy OnMouseDown. Rewrote using Mouse.current + raycast instead.
- **RPC error before hosting**: clicking before pressing Host tried to call
  a networked function with no network running. Added IsSpawned check +
  local-only movement before hosting.
- **Bad test IP**: use real LAN IP (ipconfig/ifconfig), no leading zeros.

## Setup

- One Cube in scene (NetworkObject + Collider + Cube.cs), sitting visibly
  on the Plane.
- NetworkManager: Player Prefab left empty.
- Left-side UI panel: Host / Join / Disconnect / IP field, stacked. Status
  text separate.

1. Click cube pre-host → moves locally, no network.
2. Host → cube stays put.
3. Join from other machine using host's real IP → cube snaps to host's spot.
4. Click from either machine → both update.

"Position is server-authoritative — clients just request changes, the
server decides what's real, so everyone stays in sync even if two people
interact at once."

- No real physics sync — Rigidbody's kinematic, just for collision.
- Not like Google Docs' merge — more like last-write-wins, server picks.
- Test on two actual machines, not just one editor instance.
- Double check the plane-bounds value and a real LAN IP.

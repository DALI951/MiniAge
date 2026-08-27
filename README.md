# MiniAge

A **real-time strategy (RTS) game** inspired by *Age of Empires III*, built in **Unity** and networked with **Mirror**. This is a personal learning project — a playable prototype full of working systems that Dali is iterating on.

> Unity `2022.3.62f2` · C# · Mirror networking · four-player LAN multiplayer

---

## Overview

MiniAge puts you in command of **villagers, infantry, and cavalry** who gather resources, construct buildings, and fight AI enemies in a top-down 3D RTS world. It's designed as a hands-on way to learn how a real RTS is built — unit movement, resource economies, base building, camera control, fog of war, and multiplayer sync all implemented from scratch.

## Systems / features

**Units & combat**
- `Unit`, `Villager`, `Infantry`, `Cavalry` — distinct unit types with movement, selection, and state behaviour
- Villagers can be issued move commands and can gather (state-guarded so they don't cancel commands)
- `EnemyAI` — CPU-controlled opponents that hunt/build

**Base building & economy**
- `ResourceManager` + `ResourceNode`, `MineNode`, `AnimalNode`, `ResourceSpawner` — gather gold/timber/animal resources
- `Building`, `ResourceBuilding`, `Barracks`, `Builder`, `ConstructionSite` — build placement and construction
- `BuildingPlacer` + `BuildingSpawner` — placing and spawning structures on the map

**RTS controls & camera**
- `SelectionManager`, `UnitSelectionBox`, `UnitSelectionManager` — drag/paint unit selection
- `RTSCamera` + `CameraController` — classic RTS camera controls
- `MinimapSystem` + `MinimapCamera` — clickable minimap
- `MapBoundary`, `MoveFlag`, `FloatingDamageText`

**Environment / presentation**
- `FogOfWar` + `FogRevealer` — fog-of-war reveal around units
- `AudioManager`, `EffectManager`, `ScreenShake`, `GameOverManager`, `WinLoseUI`

**Multiplayer**
- `RTSNetworkManager`, `NetworkedPlayer`, `LobbyPlayer`, `LANDiscovery` — Mirror networking with LAN discovery (UDP 47778), lobby, and up to four players

## Tech stack

| Area | Tech |
|------|------|
| Engine | Unity 2022.3.62f2 |
| Language | C# |
| Networking | Mirror (client–server authoritative), LAN UDP discovery (port 47778) |
| Navigation | Unity NavMesh |

## How to open / build

1. Install **Unity Hub** and add **Unity 2022.3.62f2**.
2. Open the project folder (`Assets/` – the full scene lives under `Assets/Scenes`).
3. Open the main scene, press **Play** — or build a standalone player.
4. For multiplayer, start one instance as host and join from the LAN-discovered list.

## Current status

This is an **in-progress prototype**, not a finished game. Several systems work but need polish and some known issues remain — selection-box placement, resource overlap/disappearance, animals spawning off-map, minimap accuracy, unit spawn ordering, the building panel, and fog of war. These are tracked as part of ongoing development.

## Repository note

This repo (`MiniAge`) shares its history with the full Unity project pushed to `the-full-project`. See that repo for the complete, unpruned Unity project source.

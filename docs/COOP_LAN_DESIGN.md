# Co-op LAN (Photon Fusion) — Design Spec

> **Primary goal:** LAN co-op via Photon Fusion Host/Client on same Wi‑Fi.  
> **Constraint:** Single-player (`GameplayScene`) must remain fully independent and unaffected.  
> **Milestone:** 2 players move + shoot in one map and complete one full mission.

---

## How to use this document

**Đọc file này trước khi làm bất kỳ thay đổi coop nào.**  
**Read this file first before any co-op implementation work.**

This file is the **single source of truth** for co-op LAN scope, architecture, and behavior. When implementing, reviewing, or asking questions about co-op:

1. **Check this document first** — do not guess or invent behavior that is not listed here.
2. **If something is unclear or missing** — add an entry under [Open Questions](#11-open-questions) or ask the project owner before coding.
3. **If a decision conflicts with this doc** — update this doc first, then implement.
4. **Single-player safety** — any shared script change must preserve SP behavior; prefer new files under `Assets/Scripts/Coop/` and `Assets/Scripts/Network/`.
5. **Implementation order** — follow [Implementation Phases](#8-implementation-phases); do not skip Phase 0.

### Quick reference for AI / Cursor agents

| Before doing… | Read section… |
|---------------|---------------|
| Menu / lobby / nickname | §4 Pre-Game Flow |
| Scene load / Fusion setup | §3.1 Shared scene + bootstrap fork, §4.3 Scene load |
| Player spawn / camera / death | §5.1, §5.2 |
| Replace capsule → real Player visual | §8 Phase 1.5, §12 |
| Shooting / damage | §5.4 |
| Enemies / level gen | §5.5, §5.6 |
| Missions / win / lose | §5.8, §5.9 |
| Disconnect handling | §5.10 |
| What NOT to build | §1.2 Out of scope |
| Which files to touch | §10 Files Reference |

---

## 1. Goals & Non-Goals

### 1.1 In scope (v1)

| Area | Decision |
|------|----------|
| Network | Fusion Host/Client, same Wi‑Fi LAN |
| Players | Min 2, max 4 (`CoopNetworkManager.MaxCoopPlayers`, `MinCoopPlayersToStart`) |
| Co-op vs SP | Co-op cannot be played solo; SP unchanged |
| Gameplay scene | **Shared:** `GameplayScene` + runtime bootstrap fork (`IsCoopSession`) |
| Pre-game | Lobby → Mission (host) → Weapon (each player) → Comic → Play |
| Scene load | Fusion `NetworkSceneManager`, host-driven |
| Level | Host picks random seed + map layout; clients follow |
| Control | Each client owns one character |
| Camera | Follow local player only; on death switch to alive teammate |
| Movement / aim | Client prediction + host reconcile |
| Shooting | Shooter client; local VFX; **hit events only** (no bullet network sync) |
| Enemies | Host runs AI; clients receive replicated state |
| Pickups | Shared; first valid claim wins |
| Missions | All SP mission types; **shared team progress** |
| Friendly fire | From menu setting (`GameSessionData.FriendlyFire`) |
| Difficulty | No scaling by player count |
| UI | Nameplates above heads; **no** teammate HP/ammo HUD |
| Pause | Local pause UI only |
| Post-match | Return to **same coop lobby room** (win or lose) |
| Nickname | Input on `CoopModeSelection_Panel`; fallback `Player1`–`Player4` |

### 1.2 Out of scope (v1)

- Online matchmaking / internet play beyond LAN Wi‑Fi
- Co-op save progression
- Cross-platform
- Dedicated server
- Anti-cheat
- Voice / text chat
- Host migration
- Difficulty scaling by player count

---

## 2. Current Baseline

### 2.1 Implemented (menu / lobby)

- `CoopNetworkManager`: host / join / browse, setup sync via ReliableData
- Setup pipeline: Lobby → Mission → Weapon → Comic → Play
- UI branching: `ConfigureForSinglePlayer()` vs `ConfigureForCoop()`
- `UI_CoopMenu.modeSelectionPanel` (Unity hierarchy: `CoopModeSelection_Panel`)

### 2.2 Not implemented (gameplay)

- `UI.HandleCoopPlayGame()` logs only — no gameplay scene load
- `Player`, `Enemy`, `Bullet` are non-networked `MonoBehaviour`
- `GameManager` assumes single `Player` via `FindObjectOfType`
- `Player_Health.Die()` triggers immediate Game Over (wrong for co-op team death)
- `LevelGenerator.ChooseRandomPart()` uses unseeded `Random`
- `CoopNetworkManager.OnInput` is empty

---

## 3. Architecture

### 3.1 Shared scene + bootstrap fork ✅

**One gameplay scene for both modes.** SP and co-op load the same `GameplayScene`; behavior diverges at runtime via `GameSessionData.IsCoopSession` and `CoopGameplayBootstrap`.

```
MenuScene (shared)
  │
  ├─ Single Player
  │    GameSessionData.IsCoopSession = false
  │    → SceneManager.LoadScene("GameplayScene")        [local, no Fusion]
  │    → SP bootstrap: scene Player active, LevelGenerator local, no NetworkRunner gameplay
  │
  └─ Co-op LAN
       Nickname on CoopModeSelection_Panel
       → Lobby → setup → Host Play
       GameSessionData.IsCoopSession = true
       → runner.LoadScene("GameplayScene")               [Fusion NetworkSceneManager]
       → CoopGameplayBootstrap.Initialize(runner)
            ├─ Disable / hide scene-placed SP Player
            ├─ CoopPlayerSpawner → NetworkPlayer at SpawnPoint_0..3
            ├─ Host: seeded LevelGenerator.InitializeGeneration()
            └─ Clients: wait CoopLevelReady (no local generation)
```

| Scene | Used by | Runtime path |
|-------|---------|--------------|
| `MenuScene` | SP + Co-op | Coop UI, mission / weapon / comic |
| `GameplayScene` | **SP + Co-op** | Fork at load: SP path vs `CoopGameplayBootstrap` |

**Why this approach:** One scene to maintain (map, NavMesh, LevelGenerator, UI). Easier to keep SP and co-op in sync. SP stays safe by never setting `IsCoopSession` and never starting Fusion gameplay load from the SP menu path.

**Coop-only objects in `GameplayScene` (inactive during SP):**

- Empty `Coop_Root` (or similar) containing:
  - `CoopGameplayBootstrap`
  - `SpawnPoint_0` … `SpawnPoint_3`
  - Optional: references to network prefabs
- `Coop_Root` stays **disabled** until `CoopGameplayBootstrap` runs; SP never enables it.

### 3.2 Scene maintenance

- **Single gameplay scene** — layout / NavMesh / LevelGenerator changes apply to both modes automatically
- **Coop_Root** — coop-only hierarchy; disabled by default; document any coop-specific spawn layout here
- **Shared assets:** LevelPart prefabs, Enemy prefabs, UI prefabs, Mission ScriptableObjects, weapons
- **Do not** add `[NetworkBehaviour]` to the SP scene-placed `Player` prefab instance

### 3.3 Session data (`GameSessionData` extensions)

```csharp
// Conceptual additions — implement in GameSessionData.cs
// GameplaySceneName already exists ("GameplayScene")

bool IsCoopSession
int LevelGenerationSeed                    // host-generated
string LocalDisplayName                    // from nickname UI or fallback
Dictionary<int, string> DisplayNamesByPlayerId
Dictionary<int, List<Weapon_Data>> WeaponsByPlayerId
// existing: SelectedMission, FriendlyFire, GetSelectedWeapons (SP only)
```

### 3.4 Code layout

```
Assets/Scripts/
  Coop/
    CoopGameplayBootstrap.cs          // scene entry when IsCoopSession
    CoopGameplayBridge.cs             // guarded delegation from shared SP scripts
    CoopPlayerSpawner.cs              // SpawnPoint_0..3 under Coop_Root
    CoopLevelGenerationHost.cs
    CoopMissionSync.cs
    CoopTeamDeathHandler.cs
    CoopNameplate.cs
    CoopPostMatchFlow.cs              // win/lose → lobby
  Network/
    NetworkPlayer.cs
    NetworkPlayerHealth.cs
    NetworkPlayerWeapon.cs
    NetworkEnemy.cs
    NetworkPickup.cs
  Managers/
    CoopNetworkManager.cs             // extend: nickname, scene load, input, RPCs
    GameSessionData.cs
  UI/
    UI_CoopMenu.cs                    // nickname field on modeSelectionPanel
```

### 3.5 Single-player isolation rules

1. SP menu path **never** sets `IsCoopSession = true` and **never** calls Fusion `LoadScene` for gameplay
2. On coop load, `CoopGameplayBootstrap` **disables** the scene-placed SP `Player` (do not destroy SP prefab asset)
3. Co-op uses **network prefabs** (`NetworkPlayer`, etc.), not the disabled SP `Player` instance
4. Prefer **new** `NetworkPlayerHealth` over modifying `Player_Health.cs`
5. Shared scripts (`LevelGenerator`, `MissionManager`, `GameManager`): gate with `if (GameSessionData.IsCoopSession)` or delegate to `CoopGameplayBridge`
6. Minimal SP guard pattern:

```csharp
if (GameSessionData.IsCoopSession)
{
    CoopGameplayBridge.HandleX(...);
    return;
}
// existing SP logic unchanged below
```

---

## 4. Pre-Game Flow

### 4.1 Nickname (`CoopModeSelection_Panel`)

- Add `TMP_InputField` on `modeSelectionPanel` before Host / Join
- On confirm Host or Join:
  - Trim input; if empty → slot fallback: `Player1`, `Player2`, `Player3`, `Player4`
  - Slot index = lobby order (host typically slot 1)
- Store in `GameSessionData.LocalDisplayName`
- Sync via Fusion player properties or ReliableData on join
- Show in lobby slots and in-game nameplate

| Condition | Display name |
|-----------|--------------|
| Custom nickname entered | Trimmed input |
| Empty / whitespace | `Player{N}` where N = lobby slot (1–4) |

### 4.2 Setup steps

| Step | Who acts | Notes |
|------|----------|-------|
| Lobby | All | Ready; min 2 to proceed |
| Mission | Host only | Broadcast selection |
| Weapon | Each player | Own loadout; lock when ready |
| Comic | All | Same timing; host-only Play |
| Play | Host | Fusion load `GameplayScene` (shared) |

### 4.3 Scene load sequence

1. Host sets `IsCoopSession = true`, generates `LevelGenerationSeed`
2. Host: `runner.LoadScene(GameplaySceneName, LoadSceneMode.Single)`
3. Clients follow via Fusion `NetworkSceneManager`
4. `OnSceneLoadDone` → `CoopGameplayBootstrap.Initialize(runner)` on all peers
5. Bootstrap disables SP Player, enables `Coop_Root`, spawns network players, host starts seeded generation

---

## 5. Gameplay Systems

### 5.1 Player spawn

- Fixed transforms: `SpawnPoint_0` … `SpawnPoint_3` under `Coop_Root` in `GameplayScene` (disabled during SP)
- Map `PlayerRef` / lobby order → spawn index
- `CoopPlayerSpawner` spawns `NetworkPlayer` prefab per connected player
- Local client: `CameraManager.ChangeCameraTarget(localPlayer.transform)`

### 5.2 Death & camera

- One player dies → **no** immediate Game Over
- Dead player's client: camera follows **first alive teammate**
- **All players dead** → team Game Over → post-match flow

### 5.3 Movement & aim

- Input collected in `CoopNetworkManager.OnInput`
- Client prediction + host reconciliation (acceptable for LAN co-op)

### 5.4 Shooting

- Shooter spawns bullet locally (pool / VFX) for feedback
- On hit: send `HitEvent` to host (target, damage, point, normal)
- Host validates (range, LOS, friendly fire) and applies damage
- Replicate health / state changes

### 5.5 Enemies

- Host: full AI simulation
- Clients: replicate transform, anim state, HP, alive/dead
- Spawn after host completes seeded `LevelGenerator`

### 5.6 Level generation

- Host: `Random.InitState(LevelGenerationSeed)` → `LevelGenerator.InitializeGeneration()`
- Broadcast seed before generation
- Clients: **do not** run independent generation; wait for `CoopLevelReady`
- **v1:** host generates level + network-spawns enemies

### 5.7 Pickups

- Shared world items
- Client sends pickup request → host atomic first-claim → replicate result

### 5.8 Missions

- All SP mission types supported
- Single shared progress bar for team (`CoopMissionSync`)
- Win condition: same as SP (objective complete)

### 5.9 Win / Game Over & post-match

| Event | Detection | Flow |
|-------|-----------|------|
| Victory | Host detects mission complete | Broadcast → victory UI on all clients |
| Team wipe | Any client may detect all dead | Report to host → host confirms → Game Over |
| After either | All | Return to **same coop lobby** (keep room / session) |

Post-match → lobby:

- Return to coop lobby UI (same room when possible)
- Reset setup step to Lobby
- Clear gameplay-only state (seed, spawned entities); keep nicknames

### 5.10 Disconnect policy

| Event | Behavior |
|-------|----------|
| **Host leaves mid-mission** | Session ends for **everyone** |
| **Client leaves mid-mission** | **Continue** with remaining players; no pause |
| Empty client slot | No backfill in v1 |

### 5.11 Pause

- Local pause UI only; does not freeze network for other players

### 5.12 Nameplates

- Show synced display name above each `NetworkPlayer`
- Source: nickname from `CoopModeSelection_Panel` or `Player{N}` fallback

---

## 6. Authority Model

| System | Authority | Clients |
|--------|-----------|---------|
| Level seed & generation | Host | Wait + receive ready signal |
| Enemy AI | Host | Replicate state |
| Player movement | Input owner + host reconcile | Predict locally |
| Hit / damage | Host validates | Send hit events; local FX |
| Pickups | Host first-claim | Request + mirror state |
| Mission progress | Host | Mirror UI |
| Win / Game Over | Host confirms reports | Local detect → report → show on broadcast |
| Host disconnect | Shutdown for all | — |
| Client disconnect | Host continues session | — |

---

## 7. SP vs Co-op Matrix

| System | Single Player | Co-op LAN |
|--------|---------------|-----------|
| Scene | `GameplayScene` | `GameplayScene` (same) |
| Entry | Local `LoadScene` | Fusion `LoadScene` + bootstrap fork |
| Network | None | Fusion Host/Client |
| Player | Scene-placed SP Player | SP Player disabled; network spawn at `SpawnPoint_N` |
| Weapon slots / reload | Full local SP logic | Owner full; remote visual only |
| IK / aim laser | Full on SP player | Local full IK + laser; remote anim + model only |
| Camera | Follow scene player | Follow local; switch on death |
| Level gen | Local random | Host seed |
| Enemies | Local AI | Host AI + replicate |
| Bullets | Local hit | Local FX + host hit event |
| Death | Immediate Game Over | Camera swap; wipe = Game Over |
| End game | Victory / Game Over UI | Same logic + return to lobby |
| Nickname | N/A | `CoopModeSelection_Panel` |

---

## 8. Implementation Phases

Do phases in order. Mark items done in this file or in project tracking.

### Phase 0 — Foundation

- [x] In `GameplayScene`: add `Coop_Root` (disabled by default) with `CoopGameplayBootstrap` + `SpawnPoint_0..3`
- [x] Register `GameplayScene` in Fusion NetworkProjectConfig (if not already)
- [x] Extend `GameSessionData` (`IsCoopSession`, seed, nicknames, per-player weapons)
- [x] Add `CoopGameplayBridge` for guarded SP/coop delegation
- [x] Nickname UI on `CoopModeSelection_Panel` + sync to lobby / nameplate
- [x] Wire `HandleCoopPlayGame` → `runner.LoadScene(GameplaySceneName)`
- [x] `CoopGameplayBootstrap`: disable SP Player, enable coop path on `OnSceneLoadDone`
- [x] Verify SP: `IsCoopSession = false` → scene Player works, `Coop_Root` never activates

### Phase 1 — Milestone: move + shoot (2 players)

- [x] `NetworkPlayer` prefab + `CoopPlayerSpawner`
- [x] Input → movement / aim prediction
- [x] Per-client camera
- [x] Hit event pipeline (host validation)

### Phase 1.5 — Player Visual Parity (replace capsule)

**Goal:** Coop player looks and controls like SP offline. Replace capsule with real Player hierarchy. SP unchanged.

**Depends on:** Phase 0 + Phase 1 complete.

#### 1.5.1 Scope (locked)

| Area | Phase 1.5 | Notes |
|------|-----------|-------|
| Character model | ✅ | Duplicate SP hierarchy → `NetworkPlayer` prefab |
| Walk / run animation | ✅ | Driven from networked move state |
| Aim body + weapon rotate | ✅ | Owner computes aim; remote mirrors visual |
| Cinemachine camera | ✅ | Local only; embed `cameraTarget` in prefab |
| Shoot + fire animation | ✅ | Local FX + host hit (Phase 1) |
| Lobby loadout (max 2 slots) | ✅ | `GameSessionData.GetWeaponsForPlayer` |
| Equip / switch weapon slots | ✅ | Input local; host does not validate slot switch v1 |
| Full reload | ✅ | Anim + rig + refill magazine (owner authority) |
| Burst / spread / burst fire | ✅ | **Simulate on owner only** |
| Remote weapon visual | ✅ | Anim layer + weapon model; **no** IK / laser / gameplay calc |
| Aim laser | ✅ | **Local player only** |
| IK / left-hand rig | ✅ | **Local full IK**; remote: layer + model only |
| Footstep / weapon SFX | ✅ | **All players**, 3D positional audio |
| `cameraDistance` per weapon | ✅ | Local on equip |
| Friendly fire | ✅ | Keep `GameSessionData.FriendlyFire` |
| Ragdoll / death visual | ❌ | Phase 3 |
| Camera handoff on death | ❌ | Phase 3; interim: **camera frozen at death position** |
| `Player_Interaction` | ❌ | Later phase |
| Debug keys P / L | ❌ | Off in v1.5 |
| HUD | ✅ | Keep existing `UI_InGame`; nameplates → Phase 4 |

#### 1.5.2 Authority principle

```
Owner client (InputAuthority)
  ├── Full weapon sim: fire rate, spread, burst, reload, ammo, equip slots
  ├── Full presentation: IK, rig, laser, camera, anim events
  └── Send to host: HitEvent only (Phase 1)

Remote clients
  ├── Replicate: transform, aim point, move input, running, weapon type, slot index
  ├── Visual only: anim (x/z velocity, isRunning, Fire trigger), weapon model switch
  └── No: IK weight, laser, ammo sim, spread calc, reload logic
```

**Rationale:** Full SP weapon parity for the owning player; remotes only need to look correct with minimal CPU/network cost.

#### 1.5.3 Prefab architecture

```
NetworkPlayer (Fusion spawn root)
├── NetworkObject + NetworkCharacterController
├── NetworkPlayer          ← movement + aim state replicate
├── NetworkPlayerHealth
├── NetworkPlayerWeapon    ← owner: full SP weapon logic (adapted)
├── NetworkPlayerHitbox
├── CoopPlayerPresentation ← visual facade (anim, remote visuals, SFX hooks)
└── PlayerBody (duplicate SP hierarchy, stripped logic scripts)
    ├── Animator, Rig, TwoBoneIK, WeaponModel[], BackupWeaponModel[]
    ├── Aim_Target, GunPoint, weaponHolder, LineRenderer
    ├── cameraTarget (embedded in prefab — NOT scene reference)
    ├── Player_SoundFX
    └── CoopPlayerAnimationEvents (replaces Player_AnimationEvents)
```

**SP scene Player:** still **disabled** on coop load — do not add `NetworkBehaviour`.

#### 1.5.4 Minimum networked state (remote visual)

| Field | Owner sets | Remote uses |
|-------|------------|-------------|
| `NetAimPoint` | ✅ local raycast | Body rotate, weapon aim visual |
| `NetMoveInput` | ✅ | Anim x/z, camera backward zoom |
| `NetIsRunning` | ✅ | `isRunning` anim |
| `NetEquippedWeaponType` | ✅ on equip | Switch weapon model / layer |
| `NetWeaponSlotIndex` | ✅ on slot change | Backup weapon visual |
| `NetFireTick` | ✅ each shot | Remote `anim.SetTrigger("Fire")` |
| `NetReloading` | ✅ optional | Remote reload anim only |

**Do not replicate:** ammo counts (no teammate ammo HUD), IK weights, laser endpoints.

#### 1.5.5 SP → Coop component mapping

| SP | Coop Phase 1.5 |
|----|----------------|
| `Player_Movement` | `NetworkPlayer` + `CoopPlayerPresentation` (anim) |
| `Player_AimController` | Owner: aim + camera lerp; remote: read `NetAimPoint` |
| `Player_WeaponController` | `NetworkPlayerWeapon` (owner full logic) |
| `Player_WeaponVisuals` | `CoopPlayerPresentation` (local full / remote reduced) |
| `Player_Health` | `NetworkPlayerHealth` (no instant Game Over) |
| `Player_AnimationEvents` | `CoopPlayerAnimationEvents` → weapon / presentation |
| `Player` hub | Removed from prefab |
| `Player_Interaction` | Out of scope |

#### 1.5.6 Camera

- Local spawn: `CameraManager.ChangeCameraTarget(cameraTarget)`.
- Lerp logic copied from `Player_AimController` (min/max distance, backward zoom when moving back).
- On equip: `CameraManager.ChangeCameraDistance(currentWeapon.cameraDistance)` — **local only**.
- On death (before Phase 3): **freeze** camera at death position; no ragdoll.

#### 1.5.7 Audio

- Walk / run / fire / reload / weapon ready on **every** `NetworkPlayer` instance.
- Use 3D `AudioSource` on prefab (spatial blend = 1).
- Remote: trigger SFX on `NetFireTick` / anim event — no ammo simulation.

#### 1.5.8 SP isolation (required)

```csharp
// Player.cs — OnDisable
if (GameSessionData.IsCoopSession) return;
controls.Disable();
```

No other SP script changes unless adding the same `IsCoopSession` guard pattern.

#### 1.5.9 Prefab workflow (manual)

1. Duplicate `Player` in `GameplayScene`.
2. Save as `Assets/Resources/Coop/NetworkPlayer.prefab`.
3. Strip SP logic scripts (see §1.5.5).
4. Add network stack + `CoopPlayerPresentation`.
5. Copy serialized values: speeds, `aimLayerMask`, weapon refs, bullet prefab, fallback weapons.
6. Wire `CoopGameplayBootstrap.networkPlayerPrefab`.
7. Remove capsule placeholder.

**Maintenance:** when SP model changes → manual re-duplicate prefab.

#### 1.5.10 Movement / prediction

- Keep `NetworkCharacterController` + Fusion input (Phase 1).
- No extra custom prediction — sufficient for LAN.

#### 1.5.11 Implementation checklist

- [ ] Fix `Player.OnDisable` coop guard
- [ ] Rebuild `NetworkPlayer` prefab from SP hierarchy
- [ ] `CoopPlayerPresentation` (anim, aim visual, camera local, remote reduced)
- [ ] `NetworkPlayerWeapon` — port equip / reload / burst / spread (owner only)
- [ ] Replicate weapon visual state for remotes
- [ ] `CoopPlayerAnimationEvents`
- [ ] Local-only laser; local-only full IK
- [ ] 3D SFX all players
- [ ] Death: stop input + freeze camera (no ragdoll)
- [ ] Verify SP unchanged
- [ ] ParrelSync 2-client acceptance

#### 1.5.12 Acceptance (ParrelSync)

- [ ] 2 clients: real model, walk/run, aim, camera match SP
- [ ] Local: laser + full IK
- [ ] Remote: anim + weapon model look correct; no IK / laser
- [ ] 2 weapon slots, equip, reload, burst/spread work on owner
- [ ] Hear other players' SFX by position
- [ ] Friendly fire follows lobby setting
- [ ] Death: camera frozen (no handoff yet)
- [ ] SP path: Menu → SP → scene Player works normally

### Phase 2 — Level + enemies

- [ ] Host-seeded `LevelGenerator`
- [ ] `NetworkEnemy` replication
- [ ] Host disconnect ends all; client disconnect continues

### Phase 3 — Full mission

- [ ] `CoopMissionSync` (all mission types)
- [ ] Pickups first-claim
- [ ] Team death + camera handoff
- [ ] Win / Game Over host confirm
- [ ] Post-match return to same coop lobby

### Phase 4 — Polish

- [ ] World nameplates
- [ ] ParrelSync / 2-machine LAN test checklist
- [ ] Lobby slots show nickname (not only PlayerId)

---

## 9. Testing

### 9.1 ParrelSync (local)

- Editor A: Host | Editor B: Client
- Test nickname input and fallback `Player1`, `Player2`
- Full flow: lobby → mission → weapons → comic → shared `GameplayScene` (coop bootstrap)
- Verify SP: Menu → SP → `GameplayScene` with no Fusion runner; SP Player active, `Coop_Root` disabled

### 9.2 Acceptance (milestone)

- [ ] 2 LAN clients in same room
- [ ] Same map layout (host seed)
- [ ] Move, aim, shoot with acceptable LAN feel
- [ ] Complete one mission with shared objective UI
- [ ] Win / lose returns to same coop lobby
- [ ] Host quit → all disconnected
- [ ] Client quit → host + remaining clients continue
- [ ] SP path unchanged

---

## 10. Files Reference

### New (preferred)

- `docs/COOP_LAN_DESIGN.md` (this file)
- `Assets/Scripts/Coop/*` (including `CoopGameplayBootstrap`, `CoopGameplayBridge`)
- `Assets/Scripts/Network/*`

### Extend

- `CoopNetworkManager.cs` — nickname sync, scene load, `OnInput`, gameplay RPCs, disconnect
- `GameSessionData.cs` — coop session fields (`IsCoopSession`, etc.)
- `UI.cs` — `HandleCoopPlayGame`
- `UI_CoopMenu.cs` — nickname input on `modeSelectionPanel`
- `GameplayScene` — add disabled `Coop_Root` + spawn points (coop-only, inactive in SP)

### Avoid modifying (SP-critical)

- `Player_Health.cs` — use `NetworkPlayerHealth` on coop prefab instead
- SP scene-placed `Player` prefab / asset — do not add network components

### Shared (minimal guarded changes)

- `GameManager.cs` — `LocalPlayer` / coop bridge when `IsCoopSession`
- `LevelGenerator.cs` — seed injection; host-only when `IsCoopSession`
- `MissionManager.cs` / mission classes — networked progress when `IsCoopSession`

---

## 11. Open Questions

Add new questions here before implementing ambiguous behavior. Remove or resolve when decided.

| # | Question | Status |
|---|----------|--------|
| — | *(none pending — see Resolved decisions log)* | — |

### Resolved decisions log

| Topic | Decision |
|-------|----------|
| Phase 1.5 parity | Model, walk/run, aim, camera, shoot, fire anim, full lobby loadout (2 slots), equip, reload, burst/spread — owner sim; remote visual only |
| Phase 1.5 laser | Local player only |
| Phase 1.5 IK | Local full; remote anim layer + weapon model |
| Phase 1.5 SFX | All players, 3D positional |
| Phase 1.5 prefab workflow | Manual duplicate SP Player → `NetworkPlayer` prefab |
| Phase 1.5 cameraTarget | Embedded in prefab, not scene reference |
| Phase 1.5 death (interim) | Camera frozen at death; ragdoll + handoff → Phase 3 |
| Phase 1.5 debug keys P/L | Off |
| Phase 1.5 HUD | Keep `UI_InGame`; nameplates → Phase 4 |
| Phase 1.5 prediction | LAN + `NetworkCharacterController` sufficient |
| Gameplay scene | **Shared `GameplayScene` + bootstrap fork** (`IsCoopSession`) |
| Host quit mid-mission | End session for everyone |
| Client quit mid-mission | Remaining players continue |
| Spawn | Fixed `SpawnPoint_0..3` under `Coop_Root` in `GameplayScene` |
| After win / lose | Return to same coop lobby |
| Display name | Nickname on `CoopModeSelection_Panel`; fallback `Player1`–`Player4` |
| Mission picker | Host only |
| Latency model | Client prediction + host reconcile; hit events for shooting |
| Doc language | English (for Cursor / tooling readability) |

---

## 12. Appendix — Phase 1.5 Player Visual Parity (detail)

Full spec for replacing capsule `NetworkPlayer` with SP-equivalent visual and feel. See [§8 Phase 1.5](#phase-15--player-visual-parity-replace-capsule) for checklist.

### Appendix A — Remote visual replication

**When owner fires:**

1. Owner: full `FireSingleBullet` + `PlayFireAnimation` + SFX.
2. Owner: increment `[Networked] NetFireTick`.
3. Remote: on change → `anim.SetTrigger("Fire")` + fire SFX at gunPoint (no bullet damage).

**When owner equips slot:**

1. Owner: full equip logic + `SwitchOnCurrentWeaponModel` + camera distance.
2. Replicate `NetEquippedWeaponType` + `NetWeaponSlotIndex`.
3. Remote: `CoopPlayerPresentation.ApplyRemoteWeaponVisual()` — switch model/layer only.

**When owner reloads:**

1. Owner: full reload coroutine + anim events + refill.
2. Remote: optional `NetReloading` → play reload anim; **no** refill logic.

### Appendix B — Files to add / extend

**New:**

- `Assets/Scripts/Coop/CoopPlayerPresentation.cs`
- `Assets/Scripts/Coop/CoopPlayerAnimationEvents.cs`

**Extend:**

- `Assets/Scripts/Network/NetworkPlayer.cs` — extra networked fields
- `Assets/Scripts/Network/NetworkPlayerWeapon.cs` — owner full weapon logic
- `Assets/Scripts/Coop/CoopGameplayBootstrap.cs` — prefab reference
- `Assets/Scripts/Player/Player.cs` — `OnDisable` coop guard only

**Avoid modifying:**

- `Player_Movement.cs`, `Player_AimController.cs`, `Player_WeaponController.cs`, `Player_Health.cs` — logic stays SP-only

**One-line summary:** Owner runs full weapon + presentation like SP; replicate minimal state so other clients render anim/model/3D SFX only — SP scene Player stays disabled in coop.

---

*Last updated: 2026-06-20 — Phase 1.5 Player Visual Parity spec added*

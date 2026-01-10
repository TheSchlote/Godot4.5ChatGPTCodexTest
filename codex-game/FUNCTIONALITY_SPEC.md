# Codex Game Functionality Spec (Current Behavior)

This document describes the current, implemented gameplay behavior in the repository so a developer can recreate it exactly. It is based on the C# domain layer, Godot scene wiring, and the demo data files.

## Entry Point
- Main scene: `res://Infrastructure/Scenes/BattleScene.tscn` (set in `project.godot`).
- Scene root script: `Infrastructure/Scenes/BattleSceneRoot.cs`.
- On `_Ready()`, the scene builds the map, spawns units, sets up input, creates camera/cursor, and initializes the battle state machine.

## Core Loop (Battle Flow)
- A deterministic turn system (TurnQueue) drives which unit is active.
- Each turn resets two per-unit flags: `moveAvailable` and `actionAvailable` (both start as `true`).
- A unit can move once and act once per turn; "End Turn" consumes both.
- After both are spent, the system auto-advances to the next ready unit.
- Battle ends when only one team remains alive (or none), showing Game Over and a Restart button.

## Input and Controls
The input map is created/extended at runtime in `BattleSceneRoot.EnsureInputMap()`:

Keyboard:
- Move camera/cursor: `W/A/S/D` mapped to `cam_forward/back/left/right` and `ui_up/down/left/right`.
- Attack/confirm: `Space` mapped to `attack` and `ui_accept`.
- End turn: `T` mapped to `end_turn`.
- Cancel: `Esc` mapped to `ui_cancel`.
- Camera rotate: `Q/E` mapped to `cam_rotate_left/right`; arrows for `cam_rotate_up/down`.
- Zoom: `+/-` mapped to `cam_zoom_in/out`.

Controller:
- Attack/confirm: `A`.
- Cancel: `B`.
- End turn: `Start`.
- Camera/cursor move: D-pad and left stick.
- Camera rotate: right stick.
- Zoom: triggers.

Mouse:
- Right mouse drag rotates camera.
- Mouse wheel zooms camera.

## Map System
Data:
- Map JSON schema (`Domain/Maps/MapData.cs`):
  - `version`, `width`, `height`
  - `cells` list: `{ x, y, elevation, type }`
  - `spawns` list: `{ team, x, y }`
- Demo map: `Infrastructure/Scenes/Maps/demo_map.json`.

Loading:
- `MapLoader.TryLoad` reads JSON into `MapData`.
- If the map fails to load, it falls back to a flat grid (`BuildFlatGrid`).

Rendering:
- `MapBuilder` builds a grid of `BoxMesh` tiles at `tileSize = 2` and `tileHeight = 0.2`.
- Elevation is visual only: tiles are offset by `elevation * tileHeight`.
- Tile color by `type`:
  - `grass`, `water`, `stone`, `sand`, default.

Spawn:
- If map data has spawn points, those are used per team in order.
- If no spawn is available, it falls back to the unit’s own `spawn` cell.

## Units and Stats
Unit data:
- Loaded from `Infrastructure/Scenes/Maps/demo_units.json` via `UnitContentLoader`.
- If that fails, `DemoContent.GetUnits()` is used.

Unit blueprint fields:
- `id`, `name`, `affinity`, `baseStats`, `moveRange`, `abilities`, `defaultQTE`, `aiProfileId`.

Runtime state:
- `UnitState` has `CurrentHP/CurrentMP`, derived from `StatBlock.MaxHP/MaxMP`.
- Damage is applied via `ApplyDamage(int amount)`.
- MP can be reduced via `SpendMP(int amount)` (not currently used by abilities).

Presentation:
- Each unit is a `Node3D` with a capsule mesh and a small facing indicator.
- Floating `Label3D` health bar shows `CurrentHP/MaxHP`, facing the camera each frame.
- Unit color is set from the unit JSON.
- Team AI control default: teams with `team > 1` are AI; teams 0 and 1 are player-controlled.

Demo units (from `demo_units.json`):
- `Player` (team 0, Fire), abilities: `basic_attack`, `ranged_shot`.
- `AllyTwo` (team 1, Earth), abilities: `basic_attack`.
- `EnemyAI` (team 2, Water), abilities: `basic_attack`.

## Turn System
Core types:
- `TurnMeter(unitId, speed, threshold=1000, turnRateConstant=1)`.
- `TurnQueue` stores meters and advances them deterministically.

Turn behavior:
- `AdvanceToNextReady()` jumps the meters forward to the next ready unit (no real time).
- `TurnOrderEntry` snapshots are used by UI to show upcoming turns.

Turn UI:
- `BattleUi` renders 6 slots: "Current" at the bottom, then upcoming order.

## Movement
Cursor:
- `SelectionCursor` moves on a grid and snaps after `SnapDelay = 0.2s`.
- Cursor position follows camera look ray when idle.
- The indicator mesh changes color if the selected tile is occupied.

Pathfinding:
- `AstarPathfinding` builds a 4-direction grid A* (no diagonal).
- Cell size is set to `tileSize = 2`.
- The map does not block cells; only units block movement.

Movement planning:
- `MovementPlanner.Plan`:
  - If path length < 2, return `NoPath`.
  - If `path.Length - 1 > moveRange`, return `NoPath`.
  - If any step (except start) is occupied, return `Occupied`.
  - Otherwise, return `Success`.

Movement execution:
- `MovementController.TryStartMove` tweens the unit along the path at `moveSpeed = 6`.
- Camera follows the moving unit (if available).
- On completion, `moveAvailable` is set to `false`.

Path preview:
- Preview is calculated, but `NullPathVisualizer` is used, so no line is shown.

## Abilities and Combat
Ability model:
- `Ability` fields: `id`, `name`, `mpCost`, `range`, `aoe`, `element`, `qte`, `damageFormula`, `tags`.

Implemented abilities (`AbilityFactory`):
- `basic_attack`
  - Range: 1 (min 1, max 1).
  - AoE: single target.
  - Element: Neutral.
  - Tags: `Knockback` (no current effect).
- `ranged_shot`
  - Range: 1-3 (min 1, max 3).
  - AoE: single target.
  - Element: Neutral.
  - Tags: none.

Damage calculation:
- `baseDamage = attacker.PhysicalAttack - defender.PhysicalDefense` (floored at 0).
- Facing multiplier:
  - `Front` = 1.0
  - `Side` = 1.5
  - `Back` = 2.0
- QTE multiplier:
  - `Critical` = 1.5
  - `Great` = 1.2
  - `Good` = 1.0
  - `Miss` = 0.0
- Total damage = `(int)(baseDamage * facingMultiplier * qteMultiplier)`.

Facing logic:
- Attacker rotates to face target.
- Facing is determined by dot product between defender facing and attacker direction.

Ability targeting/range:
- Range uses Manhattan distance (no diagonals).
- Range highlights are drawn as translucent blue tiles.

Ability selection flow:
- If cursor is on a unit, the ability panel opens (no team check).
- On selection:
  - If target in range: execute (player enters QTE, AI auto-resolves).
  - If target out of range and movement available: move into range and auto-attack.
  - If movement fails, ability is canceled.

## QTE (Timing Bar)
Evaluator:
- `TimingBarEvaluator` uses `QTEProfile.CritWindow`.
- Score windows:
  - `Critical`: `delta <= critWindow`
  - `Great`: `delta <= critWindow * 2.5`
  - `Good`: `delta <= critWindow * 4`
  - `Miss`: otherwise

UI/Controller:
- QTE duration: `1.5s`, target at `0.75s`.
- Player presses `attack` to stop the timer.
- If time expires, it forces a miss by using `pressTime = duration + 0.5`.
- AI always uses a perfect press (`pressTime == target`).

## AI
- AI is active for teams with `team > 1`.
- Target selection: nearest enemy by distance (no range consideration).
- Ability choice: first ability in the unit's list.
- If target in range: attack immediately.
- If out of range: attempts to move into the nearest reachable cell within range.
- If movement fails: consumes turn and advances.

## UI Elements
- Turn order panel (right side).
- Phase label (top center).
- Actions status label (top left).
- End Turn button (top left).
- Ability panel (centered).
- Toast messages for movement failures.
- QTE panel with timing bar and score zones.
- Game Over label and Restart button.

## Data and Asset Files
- `Infrastructure/Scenes/Maps/demo_map.json` (map cells, elevations, spawns).
- `Infrastructure/Scenes/Maps/demo_units.json` (unit definitions).
- Demo fallback data: `Infrastructure/Scenes/DemoContent.cs`.

## Automated Tests (Behavior Locked In)
Tests in `Tests/DomainTests` and `Tests/InfrastructureTests` validate:
- Damage calculation formula and scaling (`DamageCalculatorTests`).
- QTE scoring thresholds (`TimingBarEvaluatorTests`).
- Ability ranges and QTE propagation (`AbilityFactoryTests`, `AbilityResultQteTests`).
- Turn queue prediction and advancement (`TurnQueueTests`, `TurnMeterTests`).
- Movement planner range/occupancy rules (`MovementPlannerTests`).
- Map serializer round-trip (`MapSerializerTests`).

## Known Gaps / Needed Updates
These are either missing implementations or likely behavior updates needed to match a full design:
- QTE settings from `UnitBlueprint.DefaultQTE` are not used; `BattleManager` always creates a default `QTEProfile` with default difficulty/crit window.
- QTE UI timings use `QteController` values (1.5s duration, 0.1s crit window) and are not synchronized with `QTEProfile`.
- Ability MP costs are defined but never spent; MP is never displayed.
- Element affinity, special attack/defense, and ability tags are not used in damage or status effects.
- AoE and range shapes other than Manhattan distance are not implemented.
- AI domain behaviors (`Domain/AI/*`) are not wired into the scene AI.
- Map cell `type` only affects tile color; no terrain costs or blocking.
- Elevation is visual-only; movement and pathfinding ignore height.
- Path preview is calculated but not rendered (uses `NullPathVisualizer`).
- Selection cursor reports the last snapped tile; the target tile may lag input during the snap delay.
- Ability targeting allows acting on allied units (no team restriction).


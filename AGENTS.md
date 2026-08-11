Include ..\AGENTS.md

# Bot Storage — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `botstorage`
- **Namespace:** `Calloatti.BotStorage`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** `Timberborn.BlueprintSystem` is publicized via `CommonModSettings.props`, with `DoNotPublicize` for `ComponentSpec.EqualityContract`/`PrintMembers` (record-inheritance CS0507 fix — see csproj)
- **ModId:** `Calloatti.BotStorage`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Adds a bot storage building where bots can be parked. Prevents deterioration of stored bots via a Harmony prefix on `Deteriorable.Tick` that short-circuits when the bot is in the `ProtectedBots` set (O(1) `ConcurrentDictionary` lookup). Note: this file is documentation for AI agents, not a public API reference.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `BotStorage.cs` | Entire mod (single file). `BotStorageModStarter`, `BotStorageBuildingSpec`, `BotStorageBuilding`, `BotStorageBannerSetter`, `BotStorageConfigurator`, `PreventUnstaffedStatusPatch`, `DeteriorableTickPatch` |

## Classes in `BotStorage.cs`

### `BotStorageModStarter : IModStarter`
Entry point. `StartMod` runs `new Harmony("calloatti.botstorage").PatchAll()`.

### `BotStorageBuilding : BaseComponent, IAwakableComponent, IInitializableEntity, IDeletableEntity`
Core component. Uses `Enterable` events to track bots entering/leaving.
- `public static ConcurrentDictionary<Deteriorable, bool> ProtectedBots` — global O(1) set of bots whose deterioration is suppressed.
- `Awake()`: subscribes to `EntererAdded`/`EntererRemoved`, sets `WorkplacePriority` to `VeryLow`. No reflection.
- `OnEntererAdded`: disables the entering bot's `NeedManager` needs; adds bot's `Deteriorable` to `ProtectedBots`. Fires for bots loaded on map load too (`Enterer` resolving its loaded state → `Enter` → `Add`).
- `OnEntererRemoved`: re-enables needs; removes from `ProtectedBots`.
- `InitializeEntity()` (fires once on placement and on save load): populates `ProtectedBots` from `EnterersInside` as a safety net for bots already inside on map load; the `Awake` event subscription is the primary path. `IInitializableEntity` is the 1.1-compatible replacement for the removed `IStartableComponent`.

### `BotStorageBannerSetter : BaseComponent, IAwakableComponent, IFinishedStateListener, IDeletableEntity`
Sets bot-head texture and icon color on the building banner. Loads texture once (static), caches material, destroys it on `DeleteEntity`.

### `BotStorageConfigurator : Configurator`
Bindito config. `[Context("Game")]`. Binds `BotStorageBuilding` and `BotStorageBannerSetter` transient; registers `TemplateModule` decorators for `BotStorageBuildingSpec`: `BotStorageBuilding`, `WaitInsideIdlyWorkplaceBehavior`, `BotStorageBannerSetter`, `PausableBuilding`.

### `PreventUnstaffedStatusPatch : HarmonyPatch(StatusSubject, RegisterStatus)`
Prefix returning `false` (suppresses status) when the subject is a `BotStorageBuilding` and the status sprite name contains `"NoUnemployed"`. Prevents the "unstaffed" warning on this building.

### `DeteriorableTickPatch : HarmonyPatch(Deteriorable, Tick)`
Prefix returns `false` (skips original `Deteriorable.Tick`) when `BotStorageBuilding.ProtectedBots.ContainsKey(__instance)`. This is the mod's core performance feature — an O(1) dictionary lookup per tick per deteriorating bot.

## Performance Characteristics (important for optimization work)
- `BotStorageBuilding` is **purely event-driven** — it does not implement `IUpdatableComponent` or `TickableComponent`, so there is **no per-frame and no per-tick work** for the mod's own components. Cost is zero except when a bot actually enters/leaves (event handlers run the `NeedManager` and `ProtectedBots` updates).
- The `InitializeEntity()` one-time population scan and the `Awake()` event subscriptions are the only load-time costs.
- `Deteriorable.Tick` patch runs once per game **tick** per deteriorating bot. Cost is a `ContainsKey` on a `ConcurrentDictionary`.
- `ProtectedBots` is a static `ConcurrentDictionary` (O(1) lookups, thread-safe); entries are added/removed strictly via `EntererAdded`/`EntererRemoved`, so there is no drift or leak.

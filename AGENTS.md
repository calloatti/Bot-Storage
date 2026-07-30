Include ..\AGENTS.md

# Bot Storage — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `botstorage`
- **Namespace:** `Calloatti.BotStorage`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** removes `Timberborn.BlueprintSystem`
- **ModId:** `Calloatti.BotStorage`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Adds a bot storage building where bots can be parked. Prevents deterioration of stored bots via `Deteriorable.Tick` patch with O(1) `ConcurrentDictionary` tracking. Includes easter egg animations for idle bots.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `BotStorage.cs` | `IModStarter` entry point, `BotStorageBuilding` component, `BotStorageBannerSetter`, `BotStorageConfigurator`, `DeteriorableTickPatch` |

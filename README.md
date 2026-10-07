# UTowny

UTowny is an original OpenMod plugin for Unturned inspired by the *gameplay category* of town-management plugins: persistent towns, grid territory, resident plots, economy, nations, diplomacy and consensual PvP wars.

## Compatibility choice

- OpenMod.Unturned: **3.8.10**
- OpenMod.Unturned.Redist: **3.26.1.1**
- Target framework: **netstandard2.1**
- Database: SQLite (`Microsoft.Data.Sqlite`), WAL mode, foreign keys enabled, integer currency.

The plugin deliberately isolates direct SDG.Unturned calls behind adapters/listeners. Hot protection data is cached; protection checks do not query SQLite.

## Part 1 scope

This source slice includes the core domain, SQLite schema/migration bootstrap, repositories, in-memory state cache, economy, persistent server playtime, town creation/membership/roles/bank, grid claims, plot sale/purchase, authorization, public interfaces, lifecycle and connection playtime listener.

Later source parts complete the command tree, full Unturned protection listeners/interactions, configurable shop inventory adapter, spawn teleports, upkeep/tax processor, nations, alliances, wars, claim visualization and admin surface.

## Build

Restore NuGet packages and build in Release. Copy the plugin package/output into the OpenMod plugin environment using the normal OpenMod deployment flow.

## Data integrity

The schema enforces unique town names, one town membership per player, one grid owner per `(map_id, grid_x, grid_z)`, a single plot owner, and transactional transfers. All money is `INTEGER` (`long` in C#).

## Permissions

See `permissions.md`.

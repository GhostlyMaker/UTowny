# Part 1 of 4 — completion ledger

## Completed in this part

- Pinned OpenMod/Unturned dependency strategy and netstandard2.1 project.
- YAML manifest/configuration/translations baseline.
- Strong IDs, town/member/claim/player domain models.
- SQLite connection factory, WAL/foreign-key setup and schema v1 migration.
- Persistent players, balances and server playtime.
- Towns, exactly-one membership invariant, Mayor role at creation.
- Atomic town creation purchase and town treasury deposits.
- World state cache for towns, memberships and claims.
- Deterministic world-coordinate grid math.
- Claim allowance formula, adjacency check, treasury claim cost and unique-grid constraint.
- Plot listing/unlisting and atomic purchase into town treasury with race protection.
- Central town-role authorization service.
- Public API facade baseline and initial public event types.
- OpenMod DI configuration and plugin lifecycle.
- Player connect/disconnect playtime listener.
- Initial `/balance`, `/bal`, `/town`, `/t create`, `/t deposit`, `/t leave` commands.
- Permission documentation baseline.

## Remaining for Part 2

- Complete town invitation/accept/kick/promote/demote/mayor transfer/disband/info/list/residents/PvP/tax/spawn command/service paths.
- Full OpenMod building/barricade/player/vehicle protection listeners and centralized ProtectionService.
- Claim-tool interaction and temporary boundary visualization.
- Plot permissions and plot command tree.
- Config-driven `/buy` and `/sell` inventory-safe shop implementation.
- Spawn teleport warmup/cooldown/damage/movement/combat cancellation.

## Remaining for Part 3

- Upkeep/tax restart-safe scheduler and overdue idempotent processing.
- Nations, membership/invites/capital governance, alliances and persistence.
- Consensual town-war workflow, restart-safe state transitions and PvP override.

## Remaining for Part 4

- Complete admin/diagnostic command surface, destructive-action logging and bypass controls.
- Finish public cancellable events/interfaces for all major operations.
- Final localization pass, README/config documentation, install guide and manual verification checklist.
- Build/API compatibility fixes found during final compile/server verification.

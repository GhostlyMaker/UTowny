# Staging-server verification checklist

Use a disposable server/database and at least two towns with separate resident accounts. Back up before changing timing configuration. Compilation and selected SQLite checks do not replace these game-server checks.

- [ ] Start with a clean database: verify plugin discovery, SQLite native loading, migrations and all interaction patches without exceptions.
- [ ] Edit YAML values, reload, and confirm effective settings. Verify invalid settings retain the current configuration and grid changes with existing claims fail safely.
- [ ] Connect/reconnect/restart: starting balance is granted once, server playtime advances only while connected, and all balances/memberships persist.
- [ ] Create town: insufficient playtime/balance fail; successful creation charges once and creates exactly one Mayor. Duplicate name/membership fail.
- [ ] Invite, accept, promote Co-Mayor, demote and transfer mayorship. Co-Mayor cannot remove Mayor or perform forbidden irreversible actions.
- [ ] Spam deposits; verify player debit equals treasury credit. Reject zero, negative and overflowing amounts.
- [ ] Sell 10 Scrap and all Scrap; buy 10 Scrap. Verify exact quantities and currency. Test full inventory, partial stacks, equipped items, disconnect and simultaneous requests.
- [ ] Interrupt a shop trade on staging. Verify pending-trade quarantine and both manually verified reconciliation outcomes; repeated resolution must not pay twice.
- [ ] Claim on positive and negative coordinates, another map, an occupied cell, disconnected territory, and at/above allowance. Verify treasury costs and administrator bypass.
- [ ] Configure a harmless installed effect and tool item. Preview and claim using keys; confirm markers are private, temporary, rate-limited and removed on unload.
- [ ] List and buy plots. Race two purchases; only one buyer pays/owns. Leaving or removal clears plot ownership and private flags.
- [ ] Test outsiders, residents on another plot, plot owners, Co-Mayor, Mayor and permissioned bypass: structure/barricade placement, damage, lethal destruction, salvage, transform, storage, doors, generators, crops, signs/displays, beds and vehicle operations.
- [ ] Test town/plot public action flags; check vanilla locks continue to apply.
- [ ] Toggle town PvP. Verify unrelated players remain protected and environmental/self damage behaves correctly.
- [ ] Set town spawn inside/outside territory; test resident/public access, movement and damage cancellation, combat lockout, cooldown, cost, death, disconnect, vehicle use and full map mismatch.
- [ ] Unclaim spawn territory and delete town; spawn must become unavailable.
- [ ] Use short tax intervals on staging. Verify successful transfer, exemptions, failed cycles and automatic removal; no inactivity removal.
- [ ] Verify sufficient upkeep, warning to online Mayor, insufficient-upkeep immediate deletion, notifications and cleanup of plots/nation/war references.
- [ ] Restart before/after tax/upkeep deadlines and repeatedly at the same deadline; no duplicate cycle charges.
- [ ] Create nation (treasury charge), invite/accept/kick/leave, alliance request/accept/decline/remove, self/duplicate rejection, disband and capital deletion.
- [ ] Request and explicitly accept war. Verify default 12-hour preparation and 30-minute duration using persisted UTC timestamps.
- [ ] Restart during preparation, during active war, and after end time. Timers must not reset; expired wars must not briefly reactivate.
- [ ] Active war permits opposing participants' PvP but preserves property protections and excludes unrelated players.
- [ ] Check every admin action's authorization and destructive-action log, including temporary bypass expiry/disconnect.
- [ ] Inject public API from a second plugin; exercise cancellable pre-events, post-events, unloaded state and OpenMod reload without stale listeners/tasks.

## rc.2 startup regression

CI opens the actual SQLite connection factory before any OpenMod plugin exists, checks foreign keys, and reopens the database to verify persistence. On a staging server, confirm startup reaches `UTowny loaded; schema and persistent timers ready`, then restart with existing towns and overdue timers. The automated test does not replace an OpenMod/Mono server smoke test.

## rc.3 reload candidate

CI verifies cancellation of queued game-thread work without a pumping thread, no stale action after cancellation, and completion of already-running work before disposal. The periodic loop starts on the thread pool, uses cancellable visual dispatch, and avoids Unity dispatch for empty notifications/effects. Unload logs each drain/flush stage and reports waits every ten seconds; persistence is never abandoned on a timeout. Cosmetic cleanup alone may be cancelled after five seconds. Confirm `openmod reload` on the actual Windows server: unload completes, the plugin reloads, towns/balances persist, and repeating reload creates no duplicate timer or interaction hooks. Test with active claim visuals and connected players as well as an empty server.

## rc.4 permission registration

CI uses OpenMod 3.8.10 PermissionRegistry to verify every documented gameplay subcommand resolves to a registered permission, registration does not grant access by default, repeated registration does not duplicate entries, and a new plugin owner can register after unload. Unknown gameplay actions return syntax without asking OpenMod to check an unregistered permission. On the server, verify `/t`, `/t create <name>`, plot/nation/war commands, existing admin grants, and denied access for an unprivileged player. Town leadership, creation balance/playtime requirements and administrative checks remain enforced.

## rc.5 creation and command feedback

CI runs the actual town service, migrations, mutation gate, repositories and cache against SQLite. Admin creation with zero funds/playtime succeeds without charging; normal creation still checks playtime, minimum balance, and charges the configured fee. Duplicate names, existing membership, invalid names and persistence are verified. Alias permission mapping is tested. On the server test `/t`, `/t new`, `/town create`, `/towny help`, `/t new VazerTown`, and `/nation` without town membership. New help strings have built-in fallbacks so existing translations.yaml files need not be overwritten. Trusted plugin API callers of CreateAsAdminAsync must authorize the administrator before invoking it.

## rc.6 chat, countdown and payments

CI tests real SQLite transfers for conservation of funds, overdrafts, invalid/self payments, overflow rollback, and concurrent attempts to overspend. It checks RGB 0/174/98 and the pay permission mapping. On the Windows server verify countdown and final/cancel messages in green, all command/scheduled/tool messages in green, admin player/town credits, regular-user denial of admin credits, and recipient notification for `/pay`. Test warmup 0 and movement/damage cancellation. Countdown and visual color require in-game confirmation.

## rc.7 combat and effect diagnosis

CI covers environmental/self/zero damage, PvP tagging both parties, combat expiry despite recurring environment damage, per-player reset on death/respawn, and zero-duration lock. Server wiring records the actual Damaged event, after protection cancellation, instead of the preliminary Damaging event. Effect previews now use reliable TriggerEffectParameters with upward direction and the requesting player target. Test `/utowny effect 146` in-game; ID existence does not guarantee a visible suitable particle on clients. Verify accepted PvP blocks spawn, blocked PvP does not, environment damage cancels only an active warmup, and respawning clears old locks.

## rc.8 town boundaries and cell grid

CI tests actual geometry for adjacent cells, a four-plot block, L-shaped territory, a hole, disconnected territory, negative coordinates, cross-map exclusion, duplicate claims, non-divisible spacing, and explicit rejection at the marker budget. It also verifies default and invalid visualization configuration. Rendering does not write ownership or plot data.

On the Windows server, keep effect 2 and test `/t show` on one claim and after adding adjacent claims. Walk to each boundary: internal and outer edges should have markers at the configured spacing, including on slopes and near fences/trees. Verify `/t show cell`, `/t show off`, no markers sent to other players, the cooldown, and `openmod reload` during a large preview. Confirm existing `/plot forsale` and `/plot buy` still refer to a whole cell. Check effect-specific splatter lifetime separately; the asset may retain decals despite effect cleanup. Full custom-sized plots remain outside this release.


## rc.9 preview duration

Boundary markers are re-sent every `visualization.refresh_seconds` (default 1 second), independently of database/tax processing, until `duration_seconds` expires after the initial drawing completes. The new setting defaults automatically for existing configs. Cached terrain positions avoid repeat raycasts. Refresh work is bounded to 512 markers per player per 100ms tick; large previews or server lag may delay refresh. `/t show off`, replacement previews, disconnect, map changes and unload stop the old refresh. No respawns occur at or after the deadline; effect clearing is requested then. Client particle/splatter tails can still outlast cleanup, and very short-lived assets may need a shorter refresh interval.

Regression tests simulate two-second effects over eight seconds, incremental drawing, bounded batches, expiry, cancellation and replacement sessions. In-game: keep effect 134, duration 8, refresh 1; verify the border remains visible, then test off and reload during a preview.


## rc.10 rectangular plots

CI exercises migration from schema 3 with legacy owned/listed claims, four adjacent 32m plots in a 64m claim, arbitrary rectangles, negative positions, exact shared edges, claim/map containment, rejected overlaps/names/roles, concurrent buyers, insufficient funds, treasury-overflow rollback, outsider policy, precise owner/outsider/leader action checks, public flags, reload persistence, release/deletion, member-leave cleanup, legacy purchases, admin and normal unclaim guards, database overlap constraints, preview geometry and town deletion cleanup.

Live-server verification: back up before migration; select/preview/create using feet and exact coordinates; purchase with a resident; try build, storage, doors, salvage, damage and vehicle access as owner/non-owner at both sides of an edge and above/below it; test moving a buildable across the boundary; confirm town protection in gaps, vanilla locks, legacy plots, `/plot release confirm`, `/plot delete confirm`, and restart persistence. New command and event wiring still needs in-game testing.

## rc.11 planning and drafts

CI tests migration of a v4 live listing, saved draft persistence, blocked draft purchases, role checks, invalid overlapping/out-of-town moves, valid move/resize/rename/price edits, publish/unpublish, live-listing edit restrictions, owned-plot safety for every named edit, sale accounting, deleted draft reuse, database rejection of owned drafts, focus retention/clear, nearby multi-plot geometry, selected emphasis, map filtering and explicit marker budget reporting.

In-game: enable planning with effect 134; walk among several drafts and existing plots; check focus/status messages, `/plot select`, `/plot select clear`, raised outline, corner preview alongside neighbouring plots, changes after create/move/delete, publish then buy as a resident, and mode-off/disconnect/reload cleanup. Recheck legacy listings and owned plots after migration. Geometry and domain tests do not verify live Unturned rendering or command wiring.

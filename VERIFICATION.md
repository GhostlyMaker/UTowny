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

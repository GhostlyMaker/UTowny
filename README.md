# UTowny 1.0.0-rc.5

UTowny is one OpenMod gameplay plugin (`UTowny.dll`) for Unturned. It provides persistent towns, grid claims, private plots, virtual currency, a Scrap shop, town spawns, taxes, upkeep, nations, alliances and consensual PvP wars.

## Verification status

Release candidate. GitHub Actions compiles the source and packages the DLL. SQL migrations and selected integrity constraints were checked separately. This has **not been run on a live Unturned server**; complete `VERIFICATION.md` on a staging server before public use. A successful build is not a server compatibility or exploit-resistance certification.

## Requirements

- OpenMod.Unturned **3.8.10**.
- Unturned API reference package **3.26.1.1**. Actual server API compatibility must be checked on your server version, particularly interaction patches.
- Target **.NET Standard 2.1**. The .NET 8 SDK is for compiling; the Unturned server uses its own runtime.
- SQLite provider **Microsoft.Data.Sqlite 8.0.10**, SQLitePCLRaw **2.1.6**. Windows x64 and Linux x64 dependency bundles are included.

## Install

1. Stop the server and back up existing UTowny data.
2. Download the artifact from the latest successful **Build UTowny** run. Extract the bundle matching your server OS.
3. Copy the contents of its `plugins/` directory into the server's existing `openmod/plugins/` directory. It contains **one UTowny plugin DLL plus SQLite runtime libraries**. Do not replace other plugins' shared libraries blindly if your server already uses a different SQLitePCLRaw version.
4. Start the server and check the UTowny startup log. OpenMod extracts embedded default `config.yaml` and `translations.yaml` to UTowny's working directory. Edit those generated files, not the copies in the downloaded archive.
5. Grant the OpenMod permissions described in `permissions.md`. Normal players receive no admin permissions from UTowny.
6. Configure claim-tool item and effect IDs if you want the visual claim tool. Commands remain available without these assets.

On Windows, also copy `e_sqlite3.dll` from the Windows x64 bundle beside the server executable (`Unturned.exe`), then fully restart the server process. On Linux, ensure `libe_sqlite3.so` is on the server native library search path. Check startup on staging before allowing players to create towns. Do not copy Unturned, Unity or OpenMod reference assemblies from NuGet into the server.

## Configuration and gameplay

All stored money is whole integer currency. New players start at zero and can sell configured Scrap to earn money. Town creation requires the configured minimum balance and 20 hours of tracked server playtime by default; it deducts the creation price. Nation creation is paid by the founding town treasury.

Claim coordinates are floor(world position / grid size), including negative positions. Claims include a map identifier. The default size is 64 metres. Claim allowance is starting claims + residents × claims per resident. New claims must touch existing territory. Losing residents can leave a town above its allowance; it cannot add claims until within allowance. Unclaiming does not refund the purchase price.

Upkeep and resident tax deadlines are persisted in UTC. Tax is processed before upkeep when deadlines coincide. Overdue work is processed chronologically in bounded batches. A town that cannot pay upkeep is deleted immediately; its claims, memberships and war references are removed. Deleting a capital town disbands its nation and alliances. There is no inactivity removal. Mayors are always tax-exempt to preserve the required Mayor invariant.

War requests need explicit acceptance from the opposing Mayor. Default preparation is 12 hours, followed by 30 minutes of PvP. Timers survive restart. Only opposing participants receive the PvP override; property protection remains active. This plugin does not bypass a server-wide disabled-PvP setting.

Outsiders and ordinary residents cannot modify town-owned protected land by default. Owners control their own plots, and Mayor/Co-Mayor leadership has broad territory authority. Plot flags apply to everyone else, including outsiders: enabling a plot interaction flag makes that action public. Existing vanilla ownership/lock rules still apply; UTowny restrictions add to them.

Town spawn supports one destination, public/private access, movement/damage cancellation, combat lockout, warmup, cooldown and cost. Death, disconnection, vehicle occupancy, missing territory or map mismatch prevent teleportation. Warmup and cooldown are session state; town spawn itself is persistent.

### Claim-tool assets

Set `visualization.claim_tool_asset_id` to an installed item asset and `effect_asset_id` to an installed harmless temporary effect. Both default to zero because there is no universal built-in boundary asset supplied with this plugin. Clients must have the selected effect asset. While holding the item, plugin key 0 previews the current cell and plugin key 1 claims it by default. `/t show` previews without the item. Each preview sends 16 private boundary markers, is limited to once per three seconds, and clears after its configured duration (checked once per second). No permanent structures or map overlays are created.

### Reload and data

`/utowny reload` validates and applies gameplay configuration. Changing the database filename, grid size, map identity or visualization effect requires a restart. Changing grid size or map identity with existing claims additionally requires a migration/removal of claims; startup rejects mismatches rather than silently moving territory. War deadlines already accepted do not change when timing configuration changes.

Playtime checkpoints run every ten seconds and on disconnect/unload. A process crash can lose at most the uncheckpointed interval. Back up the database using SQLite's backup mechanism or with the server stopped, including any outstanding WAL data. Never manually edit a live database.

### Interrupted shop trades

SQLite cannot commit atomically with Unturned inventory saves. UTowny reserves/journals a trade, changes inventory on Unity's thread, saves the player, then finalizes currency. A crash between stages leaves a `pending` record and blocks further shop trades by that player. **Do not automatically refund or retry it.** Use `/utowny trades <Steam64>` and inspect the saved inventory/logs. Then use `/utowny resolve <trade-id> completed|cancelled`. Completing a pending sale credits it; cancelling a pending purchase refunds it. Administrators must choose the outcome matching the actual saved items. Other plugins that rewrite inventory saves require additional compatibility testing.

## Public integration

Inject `IUTownyApi` after declaring your plugin's UTowny dependency. The global API forwards to the live plugin scope and rejects calls while unavailable. It exposes town, membership management, claims, plots, economy, playtime, nation, war and protection services. Do not retain service implementations across UTowny reloads; retrieve them through the API again.

`TownyChangingEvent` supports cancellation before a domain mutation. `TownyChangedEvent` reports completed domain operations using `DomainOperation` (`Kind`, `Actor`, optional `Town` and `Detail`). Kinds include `town.create`, `town.deposit`, `town.leave`, `town.join`, town management actions, `claim.add`, plot sale/buy actions, economy changes, nation/war actions, scheduled tax/upkeep notifications and war transitions. Post-event failures are logged and cannot undo a committed action. Handlers run outside Unity unless they explicitly switch threads; do not synchronously wait on Unity from a server event.

## Build

Run `dotnet restore UTowny.csproj` then `dotnet build UTowny.csproj -c Release --no-restore`. Output is `bin/Release/netstandard2.1/UTowny.dll`. GitHub Actions additionally runs `scripts/package.py` to create OS-specific install bundles and SHA-256 checksums.

Original Part 1 has been extended in the same project. `COMMANDS.md`, `permissions.md`, and `VERIFICATION.md` describe the current version.

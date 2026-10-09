# UTowny 1.0.0-rc.10

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

Set `visualization.claim_tool_asset_id` to an installed item asset and `effect_asset_id` to an installed harmless temporary effect. Both default to zero because there is no universal built-in boundary asset supplied with this plugin. Clients must have the selected effect asset. While holding the item, plugin key 0 previews the town grid and plugin key 1 claims the current cell by default. `/t show` (or `/t show town`) draws the town at your feet, falling back to your own town; without either it previews the current unclaimed cell. It draws every claimed cell on the current map, including the town's outer perimeter, internal dividers, holes and disconnected claims. Shared edges and corners are emitted only once. `/t show cell` isolates the current cell; `/t show off` stops the preview. Markers follow terrain, excluding tree/fence/roof colliders, and are private to the requesting player.

New optional visualization settings: `marker_spacing_meters: 2` (0.5–16), `marker_height_meters: 0.15` (0.02–2), and `max_markers: 4096` (128–8192). Existing configs get these defaults automatically. Rendering uses batches of 64 markers, with cancellable pauses so reload can stop it. Previews are limited to once per three seconds. Oversized full-town previews are rejected with instructions; no incomplete grid is silently presented. Use `/t show cell` or adjust spacing/budget for large towns. Effect clearing is requested after `duration_seconds` from the end of rendering, checked roughly every 100 milliseconds. Particles and especially splatter decals can have their own lifetimes and may disappear earlier or remain after clearing. No permanent structures or map overlays are created.

The town preview shows the claim grid. Rectangular plots can subdivide those cells without changing their size: use `/plot pos1`, `/plot pos2`, `/plot create <name> <total price>`. `/plot show` previews the actual plot at your feet. Existing whole-cell plots remain valid. Do not change `grid_size_meters` on an existing town to resize plots.

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


## rc.9 preview duration

Boundary markers are re-sent every `visualization.refresh_seconds` (default 1 second), independently of database/tax processing, until `duration_seconds` expires after the initial drawing completes. The new setting defaults automatically for existing configs. Cached terrain positions avoid repeat raycasts. Refresh work is bounded to 512 markers per player per 100ms tick; large previews or server lag may delay refresh. `/t show off`, replacement previews, disconnect, map changes and unload stop the old refresh. No respawns occur at or after the deadline; effect clearing is requested then. Client particle/splatter tails can still outlast cleanup, and very short-lived assets may need a shorter refresh interval.


## Rectangular plots (rc.10)

Stop the server and back up the UTowny data directory before upgrading. Schema version 4 adds rectangular plots; it leaves existing claims, plot owners, flags, prices and listings unchanged. Once rectangular plots exist, do not run an older DLL against the upgraded database: it does not enforce their ownership boundaries. Restore the pre-upgrade backup if rolling back.

Mayor/Co-Mayor leadership can select opposite corners using `/plot pos1` and `/plot pos2` at their feet. Optional exact integer coordinates are supported: `/plot pos1 <x> <z>` and `/plot pos2 <x> <z>`. Feet coordinates round to the nearest metre (half-metres away from zero). Both corners define boundary lines, not inclusive block positions. The minimum edges are included and maximum edges excluded, so neighbouring rectangles have no overlapping ownership. Selection covers all heights; there is no vertical subdivision.

The second corner previews valid selections and reports dimensions. `/plot preview` repeats the selection preview; `/plot clear` clears it. Selections expire after 30 minutes, disconnect or reload, and reset when selecting on a different map or for a different town. `/plot create <name> <total price>` validates again, creates the rectangle and lists it. Names use 1–32 ASCII letters, digits, underscores or hyphens and are unique within the town ignoring case. Price is a nonnegative integer total, not a price per square metre.

Every covered claim must belong to the town. Rectangles are 1–1024 metres per side, covering at most 256 cells. They may cross claim edges but cannot cross wilderness, another town, another rectangular plot, or a whole-cell plot already owned/listed. Unlist an unsold legacy cell before dividing it. Existing owned legacy plots cannot be subdivided by this workflow.

Stand inside a rectangle for `/plot info`, `/plot show`, `/plot buy`, `/plot forsale <price>`, `/plot notforsale` and `/plot permissions <action> on|off`. The purchase atomically transfers the price from the buyer to the town treasury; a simultaneous second buyer, insufficient funds or treasury overflow cannot partially change money/ownership. Outsider purchase follows `plots.outsiders_can_buy`.

Owners use `/plot release confirm` to return their rectangle to the town without a refund. Leadership uses `/plot delete confirm` to remove an unowned rectangle. Owned rectangles cannot be deleted/relisted by leadership until released. These new release/delete commands apply only to rectangular plots. Unclaiming (including admin unclaim) is blocked while any rectangle intersects the cell. Explicit town deletion still deletes its plots. Leaving, being kicked, or being removed for tax releases plots in that town, matching the existing membership model.

Protection uses the object's horizontal position for build, damage, salvage, interaction and vehicle checks; transformed buildables check both old and new positions. Plot owners do not gain rights elsewhere in the same claim. Objects at any height at that position belong to the plot's protection region. The object's anchor/placement point decides its region, not every part of an oversized object's mesh. Town leadership and the explicit admin bypass retain existing rights. Unowned plots and space outside rectangles follow town protection. Vanilla locks and ownership restrictions still apply.

For API integrations, use `IProtectionService.CanAt(player, grid, x, z, action)` with the target object's coordinates. The old grid-only `Can` fails closed for ordinary players in towns with rectangles because it cannot resolve a precise target. Legacy `IPlotService` whole-cell mutations reject cells intersected by rectangular plots. `IWorldStateCache.GetTownPlots` and `GetPlotAt` expose rectangular plot snapshots; `RectPlotService` is available within the plugin scope.

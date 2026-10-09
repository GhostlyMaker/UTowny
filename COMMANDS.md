# Commands

`/town` has alias `/t`; `/nation` has alias `/n`; `/balance` has alias `/bal`. Names are exact and case-insensitive. Player arguments accept an exact online character name (quote names containing spaces) or Steam64 ID. Monetary amounts are integer currency units.

| Command | Purpose |
|---|---|
| `/balance` | Own virtual balance |
| `/sell scrap <count|all>` | Sell carried Scrap; excludes equipped slots and external storage |
| `/buy scrap <count>` | Buy Scrap if inventory has room |
| `/t create <name>` or `/t new <name>` | Create a town; users granted `UTowny:admin` skip the creation fee, minimum balance and playtime checks |
| `/t info [name]`, `/t list`, `/t residents` | Town details, towns, or residents |
| `/t invite <player>`, `/t accept <town>` | Persistent invitation and acceptance |
| `/t leave`, `/t kick <player>` | Leave or remove a lower-role member |
| `/t promote <player>`, `/t demote <player>` | Mayor assigns Co-Mayor or Resident |
| `/t mayor <player>` | Transfer mayorship |
| `/t disband confirm` | Delete town and release territory |
| `/t deposit <amount>` | Transfer personal funds to treasury |
| `/t claim`, `/t unclaim` | Claim/release the current ownership cell |
| `/t show [town]` | Show town outer boundary and grid of all its claimed cells on this map |
| `/t show cell`, `/t show off` | Isolate the current cell / stop the preview |
| `/t pvp on|off` | Mayor changes town PvP |
| `/t tax on|off`, `/t tax set <amount>` | Mayor configures resident taxes; inspect through `/t info` |
| `/t setspawn`, `/t spawn [town]` | Set/use town spawn |
| `/t public on|off` | Mayor makes spawn public/private |
| `/t protection <flags>` | Mayor configures public actions on town-owned land |
| `/plot info`, `/plot forsale <price>`, `/plot notforsale` | Inspect/list/unlist current plot |
| `/plot buy` | Atomically purchase current plot |
| `/plot permissions Build|Damage|Salvage|Interact|Vehicle on|off` | Owner/leadership changes public action access |
| `/n create <name>`, `/n info`, `/n members`, `/n allies` | Found or inspect a nation |
| `/n invite <town>`, `/n accept <nation>` | Invite/accept town membership |
| `/n kick <town>`, `/n leave`, `/n disband` | Nation membership and disbanding |
| `/n ally <nation>`, `/n allyaccept <nation>`, `/n allydecline <nation>`, `/n unally <nation>` | Alliance workflow |
| `/war request <town>`, `/war accept <town>`, `/war decline <town>`, `/war cancel <town>` | Consensual war workflow; cancel withdraws an unaccepted request |
| `/war info`, `/war list` | IDs, states and exact UTC deadlines |

Protection flags are the sum of public actions: Build=1, Damage=2, Salvage=4, Interact=8, Vehicle=16. Zero protects everything. For example, 8 permits public interaction but leaves construction/destruction protected. These flags do not change vanilla ownership checks.

## Administrator commands

All require the admin permission in addition to the root command permission. Balance/membership operations accept persisted Steam64 IDs. First connect the player to initialize their profile.

- `/utowny info <town>`, `/utowny inspect`, `/utowny debug`, `/utowny member <Steam64>`
- `/utowny delete <town>` — immediate deletion, no refund.
- `/utowny claim <town>`, `/utowny unclaim` — current cell; force claims bypass adjacency, allowance and price.
- `/utowny balance|addbalance|removebalance <Steam64> <amount>`
- `/utowny townbalance|addtownbalance|removetownbalance <town> <amount>`
- `/utowny addmember <town> <Steam64>`, `/utowny removemember <Steam64>`
- `/utowny role <Steam64> Resident|CoMayor|Mayor` — Mayor assignment transfers existing mayorship.
- `/utowny nationremove <town>` — removes a member; removing a capital disbands the nation.
- `/utowny endwar <war-id>` — cancel an unaccepted, preparing or active war.
- `/utowny bypass on|off` — additionally requires bypass permission; expires in five minutes or on disconnect.
- `/utowny reload` — validated gameplay configuration reload.
- `/utowny trades <Steam64>` — pending shop trade IDs and details.
- `/utowny resolve <trade-id> completed|cancelled` — reconcile only after verifying saved inventory; see README.

`/towny` is an alias of `/town` and `/t`. Use `/t help` for quick help; `/t` also shows help when you have no town. Creation always needs a valid, unused name and a player who does not already belong to a town. Admin creation does not exempt the resulting town from ordinary upkeep.

## Chat and money (rc.6)

UTowny chat uses #00AE62. Spawn teleport sends a countdown during the configured warmup, followed by the success or cancellation message.

- `/pay <online player name|Steam64> <amount>` transfers your existing money to another known player. Requires `UTowny:commands.pay`. Offline players can be paid by Steam64 if they already have a balance record. Names must match exactly; quote names containing spaces.
- `/utowny addbalance <online player name|Steam64> <amount>` creates money for the target balance without charging the caller. Requires both `UTowny:commands.utowny` and `UTowny:admin`.
- `/utowny addtownbalance <town> <amount>` creates money for a town treasury under the same admin checks.

Player transfers reject self-payment, nonpositive amounts, overdrafts, unknown recipients, and receiver overflow. Debit and credit commit together.

`/utowny effect <effect ID>` tests a loaded effect three metres in front of the administrator and reports the asset lifetime. It does not change visualization config. Combat lock applies to actual damage between different players, not environmental or self-damage. Death and respawn clear that player's combat lock. Damage still cancels an active warmup when configured.


## Town grid (rc.8)

`/t show` targets the town whose land you stand on, otherwise your own town; without either, it previews the current cell. The response reports cell count, cell dimensions, marker count and your current cell coordinates. All shared cell dividers are shown once, and unclaimed gaps remain outside the boundary. Use `/t show cell` while deciding which cell to sell, then `/plot forsale <price>` to list that whole cell. Each plot still equals one ownership cell; this version does not add custom-sized subdivisions or multi-cell sales. The visual preview never changes claims, ownership, balances, or existing plots.

Spacing defaults to two metres. Old config/translation files can remain; new messages have built-in fallbacks. The effect must be visible on clients; its own particle/splatter lifetime still applies. `/t show off` requests effect removal but some splatter decals may persist until their asset lifetime ends.

## rc.9 preview duration

Boundary markers are re-sent every `visualization.refresh_seconds` (default 1 second), independently of database/tax processing, until `duration_seconds` expires after the initial drawing completes. The new setting defaults automatically for existing configs. Cached terrain positions avoid repeat raycasts. Refresh work is bounded to 512 markers per player per 100ms tick; large previews or server lag may delay refresh. `/t show off`, replacement previews, disconnect, map changes and unload stop the old refresh. No respawns occur at or after the deadline; effect clearing is requested then. Client particle/splatter tails can still outlast cleanup, and very short-lived assets may need a shorter refresh interval.

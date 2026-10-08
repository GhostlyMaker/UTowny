# Commands

`/town` has alias `/t`; `/nation` has alias `/n`; `/balance` has alias `/bal`. Names are exact and case-insensitive. Player arguments accept an exact online character name (quote names containing spaces) or Steam64 ID. Monetary amounts are integer currency units.

| Command | Purpose |
|---|---|
| `/balance` | Own virtual balance |
| `/sell scrap <count|all>` | Sell carried Scrap; excludes equipped slots and external storage |
| `/buy scrap <count>` | Buy Scrap if inventory has room |
| `/t create <name>` | Create a town after balance/playtime checks |
| `/t info [name]`, `/t list`, `/t residents` | Town details, towns, or residents |
| `/t invite <player>`, `/t accept <town>` | Persistent invitation and acceptance |
| `/t leave`, `/t kick <player>` | Leave or remove a lower-role member |
| `/t promote <player>`, `/t demote <player>` | Mayor assigns Co-Mayor or Resident |
| `/t mayor <player>` | Transfer mayorship |
| `/t disband confirm` | Delete town and release territory |
| `/t deposit <amount>` | Transfer personal funds to treasury |
| `/t claim`, `/t unclaim`, `/t show` | Operate on the current grid cell |
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

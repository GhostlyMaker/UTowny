# OpenMod permissions

OpenMod uses a colon after the plugin ID and plural `commands`.

A normal player needs the root permission and the relevant subcommand permission. UTowny also checks town roles/context in services. Giving a command permission does not give a player Mayor authority.

- Economy: `UTowny:commands.balance`, `UTowny:commands.buy`, `UTowny:commands.sell`.
- Town root: `UTowny:commands.town`; individual actions: `UTowny:commands.town.create`, `.info`, `.list`, `.residents`, `.invite`, `.accept`, `.leave`, `.kick`, `.promote`, `.demote`, `.mayor`, `.disband`, `.deposit`, `.claim`, `.unclaim`, `.show`, `.pvp`, `.tax`, `.setspawn`, `.spawn`, `.public`, `.protection` (each suffix follows `UTowny:commands.town`).
- Plot root: `UTowny:commands.plot`; actions `.info`, `.buy`, `.forsale`, `.notforsale`, `.permissions`.
- Nation root: `UTowny:commands.nation`; actions `.create`, `.info`, `.members`, `.allies`, `.invite`, `.accept`, `.kick`, `.leave`, `.disband`, `.ally`, `.allyaccept`, `.allydecline`, `.unally`.
- War root: `UTowny:commands.war`; actions `.request`, `.accept`, `.decline`, `.cancel`, `.info`, `.list`.
- Admin root: `UTowny:commands.utowny` **and** `UTowny:admin`.
- Temporary protection bypass: additionally `UTowny:admin.bypass`.

The held claim tool requires `UTowny:commands.town.claim`; claiming still requires leadership. For trusted regular players, granting only the four gameplay command subtrees (`town`, `plot`, `nation`, `war`) plus economy commands is convenient if your permission store supports wildcards. Never grant the entire `UTowny:*` namespace to ordinary players because it includes administration.

UTowny registers all of the gameplay action nodes above during startup. Root command permissions are registered by OpenMod. An unknown action returns syntax help instead of checking an arbitrary unregistered permission. Registration alone does not grant the permission to any role.

`UTowny:admin` also exempts the caller from town-creation playtime, minimum balance and the creation fee. `/t new` uses the existing `UTowny:commands.town.create` permission. `/towny` is an alias for the town root. `/t help` uses town info permission.

Player transfers require `UTowny:commands.pay`. They never use the admin money-creation commands. Admin balance set/add/remove accept online player names or Steam64 IDs and remain protected by the admin root and `UTowny:admin`.

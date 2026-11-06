# EconomyScanner 0.6.0: player profiles

Source branch: `economy-scanner`. The old root EconomyScanner-source.zip is a legacy archive; use the branch source or the latest successful Actions artifact.

## Install
1. Download EconomyScanner-OpenMod from the successful Build EconomyScanner OpenMod workflow for version 0.6.0.
2. Stop the server, back up the installed plugin/config, and replace the existing EconomyScanner DLL with the new DLL (do not load both versions).
3. Preserve your existing ApiEndpoint, ApiKey, and X-Economy-Key header. PlayerLinkEndpoint is optional: if blank, /economy-snapshot becomes /economy-player-link automatically.
4. If specifying it explicitly, use https://rkbiirmdawiapfnonada.supabase.co/functions/v1/economy-player-link.
5. With RocketMod permission integration, add to the default group's Permissions in Rocket/Permissions.config.xml:

```xml
<Permission Cooldown="0">GhostlyMaker.EconomyScanner:commands.econlink</Permission>
```

For OpenMod-managed permissions, add `GhostlyMaker.EconomyScanner:commands.econlink` to the intended role instead.
6. Start the server. The load message should report v0.6.0 and list /econlink.

## Verify
Sign into the website Account page, select your observed player, generate a link code, then join as that exact player and run `/econlink CODE`. No group membership is required. The website checks automatically for two minutes; use Check verification if needed. Edit/save the avatar and bio, then inspect the player page and group roster.

Codes expire after ten minutes and can be redeemed once. Each server player and website account can have one link per server. Existing links cannot be overwritten by this command. The command obtains the SteamID from the authenticated Unturned user; it never accepts an arbitrary ID.

The server posts {server_id, steam_id, code} using its existing API key. Keys/codes are never logged by the new publisher. HTTPS is required, redirects are disabled, requests have a bounded timeout, and per-player cooldown/in-flight limits apply.

GitHub CI checks request serialization, code validation, endpoint rules, response handling, cooldown/concurrency, then compiles the actual OpenMod plugin. A successful build does not replace the final in-game installation/verification test.

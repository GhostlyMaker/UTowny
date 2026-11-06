# EconomyScanner 0.7.0

Install the new CI-built DLL/nupkg and restart the game server. Keep existing API key and endpoint.
Existing configs need no additions: PresenceEnabled defaults true; PresenceEndpoint derives /economy-presence from ApiEndpoint.
UploadEnabled must be true. Presence sends Steam IDs only (no inventories), on a five-minute heartbeat and within roughly ten seconds of roster changes. Failed uploads retry after thirty seconds. HTTPS required, redirects disabled, requests cancel on unload. An ungraceful shutdown becomes stale on the website after seven minutes.

The website reads economy_player_presence every sixty seconds while visible. Empty fresh roster means zero online; missing, failed or stale feed means unknown. Economy scans remain hourly.

## Email-free player sign-in
Enable anonymous sign-ins in Supabase Authentication > Sign In / Providers for project rkbiirmdawiapfnonada. Keep email confirmation enabled for existing email accounts.
The website creates a temporary authenticated browser session on explicit click. It gains player-profile access only after a new, ten-minute single-use code is verified by the server using the actual command actor Steam ID.
Use /econlink CODE from your own browser only. A new browser can repeat this to recover the same avatar/bio. Existing owner and group permissions are not transferred. Email accounts can continue using their existing sign-in.
Permission remains GhostlyMaker.EconomyScanner:commands.econlink.

Private economy_player_sessions grants verified browser identities access to the stable profile. Do not bulk-delete anonymous auth users: original profile ownership currently references auth.users with cascade. Account lifecycle deletion needs a separate migration before introducing anonymous-user cleanup.

Backend SQL files document migrations already applied to the hosted project; do not rerun blindly. economy-presence edge function uses X-Economy-Key and the existing hashed server key. Never expose that key in browser code.

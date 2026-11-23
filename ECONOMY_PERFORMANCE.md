# Economy performance changes — 23 November 2026

## Database

Four migrations replace the three whole-history materialized views with private incremental caches and read-only public views using the same column names and calculations. The original materialized views remain in the private schema with the suffix `_legacy_20261123` for comparison and recovery. No raw snapshots or history were deleted.

Snapshot/item mutations queue affected snapshot IDs. Item category changes and group renames invalidate affected history; unchanged dimension updates do not. Each minute, cron job 1 processes at most 25 snapshots with a 10-second cooperative batch budget and a 25-second statement timeout. Advisory locking prevents overlapping workers. Queue rows are locked and coalesced within each ingest transaction to avoid lost updates and repeated writes.

Prices retain the original semantics: historical wealth is valued using the latest item prices, including effective_from/id tie ordering. Price changes update current views immediately, then reprice history in bounded batches. Historical wealth remains on the last complete valuation until all pending batches are ready, then publishes atomically. A large repricing can therefore take several minutes; it never exposes a mixture of old/new valuations.

The public history interfaces intentionally use owner permissions over private aggregate-only caches. Anonymous/authenticated roles have SELECT on public summaries, no public writes, no direct private-cache access, and no ability to call the private worker. Security advisor notices about owner-permission views and default-deny private RLS are expected for this design; do not grant blanket private-schema access to silence them.

## Validation

- Sampled oldest/middle/recent history: 69 wealth, 97 category and 875 group-category rows matched exactly (zero differences).
- Transactional regression script validates source-item updates, unchanged/changed group names, and atomic historical repricing. All mutations are rolled back.
- Queue drained, latest history equals latest snapshot, and oldest retained history is unchanged.
- Public anonymous HTTP reads succeed for all three history endpoints; permissions checks deny writes and private worker access.
- Group/category history lookup uses the new composite index; observed execution was about 13 ms including selection of the test group/snapshot.

## Plugin 0.7.1

Presence failures back off for 30, 60, 120, 240, then 300 seconds. Warnings are limited to once per five minutes and include a safe HTTP status or timeout/connection reason. Recovery logs once and resets retry timing. Cancellation on unload is preserved. No API keys, response bodies, or player IDs are logged. Economy scan scheduling and valuation are unchanged.

The .NET build and 82 protocol/retry checks passed in GitHub Actions run 45228257553 (commit 01ab9e56d019b8ad429f5fb02c1bdbf476a81486). DLL SHA-256: 1d83dd0551c264412147f22d6efb66f265102381f33a32c57a68e2e3f05dfcf3.

Install by replacing the existing EconomyScanner.dll with the 0.7.1 build and restarting the game server. Keep the existing configuration and secret values; no configuration update is required.

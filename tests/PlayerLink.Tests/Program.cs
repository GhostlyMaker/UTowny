using EconomyScanner.Services;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
Check(PlayerLinkProtocol.IsCode("ABCDEF123456"), "valid code");
foreach (var value in new string[] { null, "", "ABCDEF12345", "ABCDEF1234567", "abcdef123456", "GBCDEF123456", "ABCDEF12345\n" })
    Check(!PlayerLinkProtocol.IsCode(value), "reject malformed code");
Check(PlayerLinkProtocol.ResolveEndpoint("", "https://example.com/functions/v1/economy-snapshot") == "https://example.com/functions/v1/economy-player-link", "derive endpoint");
Check(PlayerLinkProtocol.ResolveEndpoint("", "https://example.com/functions/v1/economy-snapshot/") == "https://example.com/functions/v1/economy-player-link", "trailing slash");
Check(PlayerLinkProtocol.ResolveEndpoint("https://example.com/link", "") == "https://example.com/link", "explicit endpoint");
foreach (var endpoint in new[] { "http://example.com/link", "file:///tmp/link", "https://user:pass@example.com/link", "https://example.com/link?key=1", "https://example.com/link#fragment" })
    Check(PlayerLinkProtocol.ResolveEndpoint(endpoint, "") == null, "unsafe endpoint");
Check(PlayerLinkProtocol.ResolveEndpoint("", "https://example.com/wrong") == null, "no guessed endpoint");
using (var json = JsonDocument.Parse(PlayerLinkProtocol.Payload("a\"\n\t\\b", 76561198000000001UL, "ABCDEF123456")))
{
    var root = json.RootElement;
    Check(root.GetProperty("server_id").GetString() == "a\"\n\t\\b", "JSON escaping");
    Check(root.GetProperty("steam_id").GetString() == "76561198000000001", "SteamID string precision");
    Check(root.GetProperty("code").GetString() == "ABCDEF123456", "code payload");
    Check(root.EnumerateObject().Count() == 3, "exact snake_case fields");
}
Check(PlayerLinkProtocol.Message(200, "{\"ok\":true,\"result\":{}}").StartsWith("Player linked!"), "confirmed success");
foreach (var body in new[] { "", "not json", "{}", "{\"ok\":false}" })
    Check(!PlayerLinkProtocol.Message(200, body).StartsWith("Player linked!"), "unconfirmed response");
foreach (var status in new[] { 400, 401, 403, 409, 429, 500, 503, 302 })
    Check(!PlayerLinkProtocol.Message(status, "{\"ok\":true}").StartsWith("Player linked!"), "HTTP error");
var gate = new PlayerLinkGate();
var now = DateTime.UtcNow;
Check(gate.TryEnter(1, now), "first request");
Check(!gate.TryEnter(1, now.AddSeconds(30)), "pending request blocked even after cooldown");
gate.Exit(1);
Check(gate.TryEnter(1, now.AddSeconds(31)), "request after completion");
gate.Exit(1);
Check(!gate.TryEnter(1, now.AddSeconds(32)), "cooldown");
Check(gate.TryEnter(1, now.AddSeconds(42)), "cooldown elapsed");
for (ulong i=2;i<=16;i++) Check(gate.TryEnter(i, now.AddSeconds(42)), "concurrent allowance");
Check(!gate.TryEnter(17, now.AddSeconds(42)), "global bound");
Console.WriteLine($"{checks} player-link checks passed.");

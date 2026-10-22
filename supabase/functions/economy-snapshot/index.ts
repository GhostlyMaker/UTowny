import { createClient } from "npm:@supabase/supabase-js@2";

const MAX_BODY_BYTES = 2 * 1024 * 1024;

function jsonResponse(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}

function safeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return diff === 0;
}

function getSupabaseAdminKey(): string | null {
  const legacy = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (legacy) return legacy;

  const raw = Deno.env.get("SUPABASE_SECRET_KEYS");
  if (!raw) return null;

  try {
    const keys = JSON.parse(raw) as Record<string, string>;
    if (keys.default) return keys.default;
    const first = Object.values(keys)[0];
    return first ?? null;
  } catch {
    return null;
  }
}

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") {
    return jsonResponse(405, { error: "method_not_allowed" });
  }

  const expectedKey = Deno.env.get("ECONOMY_INGEST_KEY");
  if (!expectedKey) {
    console.error("ECONOMY_INGEST_KEY is not configured");
    return jsonResponse(500, { error: "server_not_configured" });
  }

  const suppliedKey = req.headers.get("x-economy-key") ?? "";
  if (!safeEqual(suppliedKey, expectedKey)) {
    return jsonResponse(401, { error: "unauthorized" });
  }

  const declaredLength = Number(req.headers.get("content-length") ?? "0");
  if (Number.isFinite(declaredLength) && declaredLength > MAX_BODY_BYTES) {
    return jsonResponse(413, { error: "payload_too_large" });
  }

  let payload: Record<string, unknown>;
  try {
    const rawBody = await req.text();
    if (new TextEncoder().encode(rawBody).byteLength > MAX_BODY_BYTES) {
      return jsonResponse(413, { error: "payload_too_large" });
    }
    payload = JSON.parse(rawBody) as Record<string, unknown>;
  } catch {
    return jsonResponse(400, { error: "invalid_json" });
  }

  if (payload.schemaVersion !== 2) {
    return jsonResponse(400, { error: "unsupported_schema", expected: 2 });
  }
  if (typeof payload.serverId !== "string" || payload.serverId.trim().length === 0) {
    return jsonResponse(400, { error: "server_id_required" });
  }
  if (typeof payload.generatedAtUtc !== "string" || payload.generatedAtUtc.trim().length === 0) {
    return jsonResponse(400, { error: "generated_at_required" });
  }
  if (!Array.isArray(payload.groups)) {
    return jsonResponse(400, { error: "groups_must_be_array" });
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const adminKey = getSupabaseAdminKey();
  if (!supabaseUrl || !adminKey) {
    console.error("Supabase server credentials are unavailable to the Edge Function");
    return jsonResponse(500, { error: "server_credentials_unavailable" });
  }

  const supabase = createClient(supabaseUrl, adminKey, {
    auth: {
      persistSession: false,
      autoRefreshToken: false,
    },
  });

  const { data, error } = await supabase.rpc("ingest_economy_snapshot", {
    payload,
  });

  if (error) {
    console.error("Snapshot ingestion failed", error);
    return jsonResponse(500, {
      error: "ingestion_failed",
      message: error.message,
    });
  }

  return jsonResponse(200, {
    ok: true,
    snapshotId: data,
    serverId: payload.serverId,
    generatedAtUtc: payload.generatedAtUtc,
  });
});

/**
 * Evection Hook settings sync.
 *
 *   GET    /auth/discord/start?redirect=http://127.0.0.1:<port>/callback/   → Discord login
 *   GET    /auth/discord/callback                                           → back to the app with ?token=
 *   GET    /v1/settings     (Bearer token)  → the player's synced settings JSON, 404 if none yet
 *   PUT    /v1/settings     (Bearer token)  → store settings JSON (max 64 KB)
 *   DELETE /v1/settings     (Bearer token)  → delete everything stored for the player
 *
 * Stores nothing but one JSON blob per Discord user id. Sessions are HMAC-signed tokens, so there's no session table.
 */

export interface Env {
  SETTINGS: KVNamespace;
  DISCORD_CLIENT_ID: string;
  DISCORD_CLIENT_SECRET: string;
  SESSION_SECRET: string;
}

const MAX_BODY_BYTES = 64 * 1024;
const SESSION_SECONDS = 180 * 24 * 60 * 60;
const STATE_SECONDS = 10 * 60;
// Only ever hand tokens back to the Evection Hook app listening on this machine.
const LOOPBACK_REDIRECT = /^http:\/\/127\.0\.0\.1:\d{2,5}\/callback\/$/;

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    try {
      if (url.pathname === "/") return new Response("Evection Hook sync server\n");
      if (url.pathname === "/auth/discord/start" && request.method === "GET") return await start(url, env);
      if (url.pathname === "/auth/discord/callback" && request.method === "GET") return await callback(url, env);
      if (url.pathname === "/v1/settings") return await settings(request, env);
      return json({ error: "Not found" }, 404);
    } catch (e) {
      console.error(e);
      return json({ error: "Server error" }, 500);
    }
  },
};

// ---------------- Auth ----------------

async function start(url: URL, env: Env): Promise<Response> {
  const redirect = url.searchParams.get("redirect") ?? "";
  if (!LOOPBACK_REDIRECT.test(redirect)) return json({ error: "Invalid redirect" }, 400);

  const state = await sign({ r: redirect, n: crypto.randomUUID(), exp: now() + STATE_SECONDS }, env);
  const discord = new URL("https://discord.com/oauth2/authorize");
  discord.searchParams.set("client_id", env.DISCORD_CLIENT_ID);
  discord.searchParams.set("response_type", "code");
  discord.searchParams.set("redirect_uri", `${url.origin}/auth/discord/callback`);
  discord.searchParams.set("scope", "identify");
  discord.searchParams.set("state", state);
  discord.searchParams.set("prompt", "none");
  return Response.redirect(discord.toString(), 302);
}

async function callback(url: URL, env: Env): Promise<Response> {
  const state = await verify<{ r: string }>(url.searchParams.get("state") ?? "", env);
  if (!state || !LOOPBACK_REDIRECT.test(state.r)) return json({ error: "Sign-in expired, try again from the app." }, 400);

  const back = new URL(state.r);
  const code = url.searchParams.get("code");
  if (!code) {
    back.searchParams.set("error", "Sign-in was cancelled.");
    return Response.redirect(back.toString(), 302);
  }

  const tokenResponse = await fetch("https://discord.com/api/oauth2/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      client_id: env.DISCORD_CLIENT_ID,
      client_secret: env.DISCORD_CLIENT_SECRET,
      grant_type: "authorization_code",
      code,
      redirect_uri: `${url.origin}/auth/discord/callback`,
    }),
  });
  if (!tokenResponse.ok) {
    back.searchParams.set("error", "Discord sign-in failed.");
    return Response.redirect(back.toString(), 302);
  }
  const { access_token } = (await tokenResponse.json()) as { access_token: string };

  const userResponse = await fetch("https://discord.com/api/users/@me", { headers: { Authorization: `Bearer ${access_token}` } });
  if (!userResponse.ok) {
    back.searchParams.set("error", "Couldn't read your Discord profile.");
    return Response.redirect(back.toString(), 302);
  }
  const user = (await userResponse.json()) as { id: string; username: string; global_name?: string | null };

  const sub = `discord:${user.id}`;
  const name = user.global_name || user.username;
  back.searchParams.set("token", await sign({ sub, name, exp: now() + SESSION_SECONDS }, env));
  back.searchParams.set("user", sub);
  back.searchParams.set("name", name);
  return Response.redirect(back.toString(), 302);
}

// ---------------- Settings ----------------

async function settings(request: Request, env: Env): Promise<Response> {
  const auth = request.headers.get("Authorization") ?? "";
  const session = auth.startsWith("Bearer ") ? await verify<{ sub: string }>(auth.slice(7), env) : null;
  if (!session?.sub) return json({ error: "Not signed in" }, 401);
  const key = `settings:${session.sub}`;

  switch (request.method) {
    case "GET": {
      const value = await env.SETTINGS.get(key);
      return value === null ? json({ error: "Nothing synced yet" }, 404) : new Response(value, { headers: { "Content-Type": "application/json" } });
    }
    case "PUT": {
      const body = await request.text();
      if (new TextEncoder().encode(body).length > MAX_BODY_BYTES) return json({ error: "Settings too large" }, 413);
      let parsed: unknown;
      try {
        parsed = JSON.parse(body);
      } catch {
        return json({ error: "Invalid JSON" }, 400);
      }
      if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) return json({ error: "Expected a JSON object" }, 400);
      await env.SETTINGS.put(key, body);
      return json({ ok: true, updatedAt: new Date().toISOString() });
    }
    case "DELETE":
      await env.SETTINGS.delete(key);
      return json({ ok: true });
    default:
      return json({ error: "Method not allowed" }, 405);
  }
}

// ---------------- Signed tokens ----------------

export async function sign(payload: Record<string, unknown>, env: Pick<Env, "SESSION_SECRET">): Promise<string> {
  const body = base64url(new TextEncoder().encode(JSON.stringify(payload)));
  const signature = await crypto.subtle.sign("HMAC", await hmacKey(env), new TextEncoder().encode(body));
  return `${body}.${base64url(new Uint8Array(signature))}`;
}

export async function verify<T>(token: string, env: Pick<Env, "SESSION_SECRET">): Promise<(T & { exp: number }) | null> {
  const [body, signature] = token.split(".");
  if (!body || !signature) return null;
  let valid = false;
  try {
    valid = await crypto.subtle.verify("HMAC", await hmacKey(env), fromBase64url(signature), new TextEncoder().encode(body));
  } catch {
    return null;
  }
  if (!valid) return null;
  const payload = JSON.parse(new TextDecoder().decode(fromBase64url(body))) as T & { exp: number };
  return typeof payload.exp === "number" && payload.exp > now() ? payload : null;
}

async function hmacKey(env: Pick<Env, "SESSION_SECRET">): Promise<CryptoKey> {
  if (!env.SESSION_SECRET || env.SESSION_SECRET.length < 32) throw new Error("SESSION_SECRET must be set (32+ characters)");
  return crypto.subtle.importKey("raw", new TextEncoder().encode(env.SESSION_SECRET), { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
}

function base64url(bytes: Uint8Array): string {
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromBase64url(text: string): Uint8Array {
  const binary = atob(text.replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

const now = () => Math.floor(Date.now() / 1000);

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

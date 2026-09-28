// Runs the worker against an in-memory KV (no Discord, no network): npm test
import assert from "node:assert/strict";
import worker, { sign, verify } from "../src/index.ts";

const store = new Map<string, string>();
const env = {
  SETTINGS: {
    get: async (k: string) => store.get(k) ?? null,
    put: async (k: string, v: string) => void store.set(k, v),
    delete: async (k: string) => void store.delete(k),
  },
  DISCORD_CLIENT_ID: "123",
  DISCORD_CLIENT_SECRET: "secret",
  SESSION_SECRET: "x".repeat(40),
} as any;

const call = (path: string, init?: RequestInit) => worker.fetch(new Request("https://sync.test" + path, init), env);
const exp = () => Math.floor(Date.now() / 1000) + 3600;

// Tokens
const token = await sign({ sub: "discord:1", name: "Test", exp: exp() }, env);
assert.equal((await verify<{ sub: string }>(token, env))?.sub, "discord:1");
assert.equal(await verify(token.slice(0, -2) + "xx", env), null, "tampered token rejected");
assert.equal(await verify(await sign({ sub: "a", exp: 1 }, env), env), null, "expired token rejected");

// Auth start only redirects back to loopback
assert.equal((await call("/auth/discord/start?redirect=" + encodeURIComponent("https://evil.example/callback/"))).status, 400);
const start = await call("/auth/discord/start?redirect=" + encodeURIComponent("http://127.0.0.1:5123/callback/"));
assert.equal(start.status, 302);
assert.match(start.headers.get("Location")!, /^https:\/\/discord\.com\/oauth2\/authorize\?client_id=123/);

// Settings
const auth = { Authorization: `Bearer ${token}` };
assert.equal((await call("/v1/settings")).status, 401, "no token");
assert.equal((await call("/v1/settings", { headers: auth })).status, 404, "nothing yet");
assert.equal((await call("/v1/settings", { method: "PUT", headers: auth, body: "[1,2]" })).status, 400, "arrays rejected");
assert.equal((await call("/v1/settings", { method: "PUT", headers: auth, body: "x".repeat(70_000) })).status, 413, "too large");
assert.equal((await call("/v1/settings", { method: "PUT", headers: auth, body: '{"Theme":"Light"}' })).status, 200);
assert.deepEqual(await (await call("/v1/settings", { headers: auth })).json(), { Theme: "Light" });

// Users can't read each other's data
const other = { Authorization: `Bearer ${await sign({ sub: "discord:2", exp: exp() }, env)}` };
assert.equal((await call("/v1/settings", { headers: other })).status, 404);

assert.equal((await call("/v1/settings", { method: "DELETE", headers: auth })).status, 200);
assert.equal((await call("/v1/settings", { headers: auth })).status, 404, "deleted");

console.log("sync-worker: all tests passed");

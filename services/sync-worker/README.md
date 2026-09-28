# Evection Hook sync server

Optional. Lets players sign in with Discord so their Evection Hook settings follow them to another computer.
Without it, the app works fully and the sign-in button explains that sync isn't set up.

It's a [Cloudflare Worker](https://developers.cloudflare.com/workers/) with one KV namespace — the free tier is plenty.
It stores one small JSON blob per Discord account (preferences + mod names — never files or paths) and nothing else.

## Deploy

1. **Discord app** — https://discord.com/developers/applications → New Application → OAuth2:
   - copy the **Client ID** and **Client Secret**
   - add redirect `https://<your-worker>.workers.dev/auth/discord/callback`
2. **Cloudflare**
   ```sh
   cd services/sync-worker
   npm install
   npx wrangler login
   npx wrangler kv namespace create SETTINGS        # paste the id into wrangler.toml
   # put your Discord Client ID into wrangler.toml [vars]
   npx wrangler secret put DISCORD_CLIENT_SECRET
   npx wrangler secret put SESSION_SECRET           # e.g. output of: openssl rand -hex 32
   npx wrangler deploy
   ```
3. **Point the app at it** — build with the URL baked in:
   ```sh
   dotnet publish src/Evection.GUI -p:SyncServerUrl=https://<your-worker>.workers.dev
   ```
   (or, for testing, paste the URL into Settings → Advanced → Sync server).

## Develop

```sh
npm install
npm test          # runs the worker against an in-memory KV
npm run typecheck
npm run dev       # local server via wrangler
```

## Security notes

- Tokens are only ever redirected to `http://127.0.0.1:<port>/callback/` — the app on the player's own machine.
- Sessions are HMAC-signed (SESSION_SECRET) and expire after 180 days. Rotating the secret signs everyone out.
- Players can delete their data from the app (Settings → Account → Delete cloud data).

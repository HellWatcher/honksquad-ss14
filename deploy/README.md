# Deploy assets

Canonical server configuration kept in-repo so it can be copied into the
server build output by the (external) codebase switcher / deploy script.

## `server_config.toml`

Drop this next to `Robust.Server` in the deployed server build (it is the
file Robust looks for at startup).

### Provenance

This file was reconstructed from an older backup that **predated the Lowpop
ruleset**. The only known config change made after that backup was:

- **Lowpop ruleset** (PR #284): activate by setting
  `game.secret_weight_prototype = "LowpopSecret"` (default is `"Secret"`).
  This is included under `[game]`.

If any other manual edits were made on the live server after the backup was
taken, they could not be recovered here — diff this file against the running
server's config and reconcile any differences before relying on it.

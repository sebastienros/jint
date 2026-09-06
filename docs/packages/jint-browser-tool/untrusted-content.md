# Untrusted content

`fetch`, `eval`, and `serve` do not enable the hardened profile by default. Add `--untrusted` for content nobody has reviewed:

```bash
jint-browser fetch https://unknown.example/ \
  --untrusted \
  --max-task-duration 2s \
  --memory-limit 256mb
```

The profile disables dynamic code evaluation, CLR interop, module loading, the debugger, and experimental engine features. It also applies bounds to script execution and enables private-network blocking by default.

`--max-task-duration` limits one page turn, not the whole command. `--memory-limit` limits managed allocation during one turn. Navigation has a separate `--timeout`.

Network posture:

- `--block-private-network` explicitly refuses loopback and private addresses.
- `--allow-private-network` explicitly allows them, including under `--untrusted`.
- Giving both is a usage error.

The private-network rule is coarse and does not resolve hostnames itself. Run untrusted pages in an appropriately isolated network environment when DNS or routing can expose sensitive services.

The `mcp` command is the inverse: it hardens pages by default because an agent commonly chooses unknown URLs. `mcp --trusted` turns that profile off.

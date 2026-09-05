# Security

Jint is an in-process interpreter, not an operating-system security boundary.

For scripts you do not trust:

1. Start with `options.ForUntrustedCode()`.
2. Expose only the host functions and objects the script needs.
3. Keep CLR access disabled.
4. Bound parsing, statements, time, memory, recursion, regular expressions, and module graphs.
5. Use a fresh engine across trust domains.
6. Add process-level CPU and memory isolation for hostile input.

Network-capable Web APIs and browser automation also need URL filtering that is checked on redirects. Block
loopback, private, link-local, and cloud metadata addresses unless access is intentional.

Read [Running Untrusted Code](../guide/untrusted-code.md) and the repository
[threat model](https://github.com/sebastienros/jint/blob/main/.github/THREAT_MODEL.md) before deploying a script
service.

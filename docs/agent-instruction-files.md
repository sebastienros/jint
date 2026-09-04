# Which agents load which instruction file

The root `AGENTS.md` keeps itself under 24 KiB and every co-located `AGENTS.md` under 32 KiB, and
`Jint.Tests/AgentInstructionFileTests.cs` fails the build when either is crossed. This page holds the evidence
those two numbers rest on, verified 2026-08-23, so the root file does not have to carry it.

| Agent | Root file it loads | Loads a nested `AGENTS.md`? |
| --- | --- | --- |
| OpenAI Codex | `AGENTS.override.md`, then `AGENTS.md` | No — never below the working directory |
| Claude Code | `CLAUDE.md`, which imports this file; it does **not** read `AGENTS.md` | Yes, indirectly — it loads a directory's `CLAUDE.md` when it reads a file there, and each of those is one line importing the `AGENTS.md` beside it; `.claude/rules/*.md` with `paths:` routes to the same files |
| Copilot cloud agent | `AGENTS.md` (since 2025-08-28) *and* `.github/copilot-instructions.md`; both are supplied | Yes, `**/AGENTS.md`, nearest in the tree wins |
| Copilot code review | `AGENTS.md` (since 2026-06-18) | No — root only |
| Copilot CLI | `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` | Upward only, cwd → git root |
| Copilot in VS Code | `AGENTS.md` (on by default since v1.105) | Off by default; even when on it injects the *path*, not the content |
| Copilot in Visual Studio / JetBrains | `.github/copilot-instructions.md` only — no `AGENTS.md` support | No |
| Cursor | `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/*.mdc` | Yes, combined with parents, more specific wins |
| Amp | `AGENTS.md`, falling back per directory to `AGENT.md` / `CLAUDE.md` | Yes, lazily, when it reads a file in that subtree |
| Devin Desktop (ex-Windsurf) / Devin CLI | `AGENTS.md`, `.devin/rules/*.md`, `.windsurf/rules/*.md` | Yes — a subdirectory file becomes a glob rule for `<dir>/**` |
| Gemini CLI | `GEMINI.md`; `AGENTS.md` only if `context.fileName` names it | Upward from a touched path, just in time |
| Jules | `AGENTS.md` at the repository root | Not documented |
| Aider | nothing automatically; needs `read: [AGENTS.md]` in `.aider.conf.yml` | No |

A one-line `CLAUDE.md` — the single import `@AGENTS.md` — sits beside every co-located `AGENTS.md`, mirroring
the repository root. It carries no content of its own on purpose: the substance is in the `AGENTS.md`, and a
`CLAUDE.md` that grew any would be a second copy nothing holds it to the first. What it buys is a route into
a nested file for the ecosystems the last column says have none — Claude Code above all, which does not read
`AGENTS.md` at any depth, and Copilot CLI, whose walk is upward from the working directory. Amp and Cursor
already descend and simply arrive at the same file by a second path; nothing is duplicated either way,
because the pointer is an import rather than a copy.

**32 KiB is Codex's `project_doc_max_bytes` default**, and it is a running budget across the whole
root-to-cwd chain rather than a per-file allowance, so a fat root file starves a nested one; overflow is a
silent mid-file byte truncation whose only signal is a log line below the level `codex exec` prints at. The
Devin CLI caps an always-on rule file at the same 32 KiB, truncating with a pointer to the source. Devin
Desktop documents 12,000 characters per workspace rule file; whether that applies to an `AGENTS.md` its rules
engine processes is undocumented, so treat it as the tightest plausible budget.

---
paths:
  - "Jint.Browser.Tool/**"
  - "Jint.Tests.Browser/Tool/**"
---

You are editing the `jint-browser` command line. `Jint.Browser.Tool/AGENTS.md` carries the one rule the project exists for — it consumes `Jint.Browser`'s **public** surface and never takes an `InternalsVisibleTo` grant, so a seam it turns out to need is a seam promoted on the package with a baseline diff — plus what is deliberately absent (no command-line library, no output format of its own, nothing that runs script to read a document), the exit-code contract, and how the tool package is built and packed.

**Read [`Jint.Browser.Tool/AGENTS.md`](../../Jint.Browser.Tool/AGENTS.md) before you edit**, and [`Jint.Browser/AGENTS.md`](../../Jint.Browser/AGENTS.md) beside it for the package this drives. Neither is repeated here or in the repository-root
`AGENTS.md`; that file's index says what each co-located instruction file covers.

An unknown option is a usage error, never a positional argument, and an option a command cannot act on is refused rather than ignored. The exit codes are a contract stated in three places at once — `ExitCode`, the package README's table and `--help` — so a new one changes all three.

`Program.cs` stays a shell over `ToolProgram.RunAsync(args, output, error, token)`: the suite runs the real entry point in process, so logic that moves into `Program.cs` is logic nothing tests.

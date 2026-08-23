# Copilot instructions for Jint

**The instructions live in [`AGENTS.md`](../AGENTS.md) at the repository root. Read it first.** Its index
names an `AGENTS.md` co-located with each area of the code — read the one matching what you are about to
change before you change it.

Nothing is repeated here on purpose. Copilot's cloud agent and Copilot code review read `AGENTS.md`
directly, and the cloud agent additionally picks up the nested ones; a second copy of the same rules in
this file would be supplied *alongside* them and would drift out of date, which is exactly what happened
to the previous version of this file.

This file therefore exists for the two surfaces that read it and nothing else: Copilot in **Visual Studio**
and in **JetBrains** IDEs have no `AGENTS.md` support, so a pointer is the only thing that reaches them.

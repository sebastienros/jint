// The same strong-name public key Jint's own grants carry (Jint/AssemblyInfoExtras.cs); this assembly is
// signed with Jint.snk, so a friend has to name it.
//
// Nothing here is public: a command-line tool has one entry point and no callers, so publishing a surface
// would be publishing a contract nobody asked for. The suite runs `ToolProgram.RunAsync` in process instead
// of spawning one, which is what lets a test assert an exit code, stdout and stderr together.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Jint.Tests.Browser, PublicKey=0024000004800000940000000602000000240000525341310004000001000100bf2553c9f214cb21f1f64ed62cadad8fe4f2fa11322a5dfa1d650743145c6085aba05b145b29867af656e0bb9bfd32f5d0deb1668263a38233e7e8e5bad1a3c6edd3f2ec6c512668b4aa797283101444628650949641b4f7cb16707efba542bb754afe87ce956f3a5d43f450d14364eb9571cbf213d1061852fb9dd47a6c05c4")]

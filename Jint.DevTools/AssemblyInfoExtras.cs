// The same strong-name public key Jint's own grants carry (Jint/AssemblyInfoExtras.cs); this assembly is
// signed with Jint.snk, so a friend has to name it.
//
// Jint.Browser is on the list because the page-level domains it adds derive from the generated dispatch
// bases here, which are internal for the same reason everything else in this package is: the protocol
// surface is not a compatibility contract until a host has something to hold on to.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Jint.Tests.DevTools, PublicKey=0024000004800000940000000602000000240000525341310004000001000100bf2553c9f214cb21f1f64ed62cadad8fe4f2fa11322a5dfa1d650743145c6085aba05b145b29867af656e0bb9bfd32f5d0deb1668263a38233e7e8e5bad1a3c6edd3f2ec6c512668b4aa797283101444628650949641b4f7cb16707efba542bb754afe87ce956f3a5d43f450d14364eb9571cbf213d1061852fb9dd47a6c05c4")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Jint.Browser, PublicKey=0024000004800000940000000602000000240000525341310004000001000100bf2553c9f214cb21f1f64ed62cadad8fe4f2fa11322a5dfa1d650743145c6085aba05b145b29867af656e0bb9bfd32f5d0deb1668263a38233e7e8e5bad1a3c6edd3f2ec6c512668b4aa797283101444628650949641b4f7cb16707efba542bb754afe87ce956f3a5d43f450d14364eb9571cbf213d1061852fb9dd47a6c05c4")]

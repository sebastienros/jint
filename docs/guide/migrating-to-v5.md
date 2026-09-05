# Migrating to Jint 5

Jint 5 includes public API changes, new defaults, and behavioral changes that embedders must review.

The complete migration record is maintained in [Migrating from Jint 4.16 to Jint 5](../v5-migration.md).
It covers:

- target framework changes;
- removed, renamed, and reshaped APIs;
- changes to CLR writes and interop;
- constraints, promises, modules, and event-loop behavior;
- Web APIs and browser packages;
- Native AOT and trimming.

Treat behavioral changes as seriously as signature changes. An existing project can compile successfully while
still needing a configuration update.

# Native input selection bindings

HTML §4.10.20 defines `selectionDirection` as a writable attribute. The old pinned
projection exposed only its getter. Native binding review approved correcting that
descriptor while preserving every existing interface and member name. The setter
uses the input's existing native selection state and callback-aware algorithm:
offsets stay unchanged, unknown directions normalize to `none`, and unsupported
input types throw `InvalidStateError`.

Changed ranges use the existing Browser Events task queue for `select` and the
existing coalesced `selectionchange` scheduler. They introduce no second selection
or event model. Getters and setters pass the calling realm's checkpoint and token
to native value-state demand and selection operations.

# Checkedness invocation checkpoints

This source checkpoint adds invocation-owned Action<int>? overloads immediately before
CancellationToken to the native checked-state lookup, checked/unchecked/default/indeterminate
predicates, radio applicability/facts/same-group/snapshot/first-checked queries, checked assignments,
reset/copy and indeterminate assignment. Existing signatures remain available. No callback or engine
is stored on an element, document, sidecar or index; HtmlCheckedWorkProbe remains test instrumentation.

The existing counter follows actual indexed metadata reads, parent/tree visits, radio-name hashing,
collision comparisons and checked-peer reads. The callback runs at each 256-source-unit checkpoint
and each completed publication boundary. A publication callback can repeat the current count:
allocations may have occurred since the previous source visit. A warm constant query can report zero,
because no source iteration occurred; it still checks the caller's current engine constraints.
There is no count-only preflight pass or delegate adapter allocation.

Cold metadata prepares the complete sidecar locally and checks before publication. Group bootstrap
builds the complete index before publishing membership handles; completed metadata sidecars from
an interrupted bootstrap remain valid caches. Warm incremental registration reserves member/checked
capacity and checks before assigning membership. Checkedness operations stage peer identities and
reserve capacity before a final check, then commit flags/exclusion without invoking caller code during
partial commit. Predicates preserve the lazy nonradio boundary and do not create nonradio sidecars.

The uninterruptible flag and index-handle commit loops consume already prepared identities. Structural
DOM mutation hooks, defaultChecked attribute writes and Browser/clone consumer forwarding remain
separate work. This checkpoint does not claim every raw DOM entry is covered by the Browser budget.

CheckedInvocationCheckpointTests uses real Action<int> callers without installing a document probe.
It covers long metadata/tree/name scans, source-count tails, unpublished cold sidecars/indexes,
original cancellation tokens, caller exceptions, atomic peer/constant flag operations and allocation-free
warm queries. No builds or tests have been run for this source checkpoint: the coordinator owns the
serialized build slot. Fresh Release net10 validation is pending its explicit grant.

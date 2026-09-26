# Textarea invocation checkpoints

This source checkpoint extends the existing textarea store with invocation-owned Action<int>?
overloads immediately before CancellationToken. It covers current/default/submission value and
text length reads, public/private selection, Select, Reset and both SetRangeText forms. Existing
SetValue and CopyFrom callbacks now span their actual dependent reads/comparisons and final tail.
No callback, engine or alternate value/selection store is attached to the document.

Cold reads prepare raw child text and normalized API text in local immutable references. They
publish the derived caches only after the caller's final checkpoint. Selection likewise prepares
its dependent value and offset before checking and committing. Script writes/reset/replacement
prepare actual copies, normalization and equality under one counter; commit takes a known raw
change result and does no unpolled post-check string comparison. CopyFrom does not warm its source
while preparing a target and checks its short tail before assigning target fields.

Submission threads the same counter through value preparation, actual wrap/column attributes and
hard-wrap counting/copying. Text-control length/column/wrap reads now flush short tails, and attribute
iteration uses actual indexed attributes. Child text collection counts child visits and actual
character copies; its old no-op character-accounting loop in the O(1) length pass was removed, so
the budget checks no longer execute a fake source preflight. The length phase flushes its actual
child-visit count before allocating a copy buffer, and the copy phase flushes before allocating the
immutable string. Those allocation boundaries can repeat the same actual count in a subsequent
final tail; they do not invent character visits for an O(1) length read. User editing keeps its established
shared ancestry/normalization/comparison cadence and receives the corrected actual collection work.

Textarea defaultValue writes and raw DOM child-mutation hooks still use the existing core DOM
mutation routes; those are outside this read/selection producer seam. Checkedness callbacks and
Browser/clone consumption remain independent checkpoints. No complete Browser-budget, WPT,
benchmark, public API or deployment claim is made.

TextAreaInvocationCheckpointTests covers cold reads and selections, cancellation/budget exceptions,
original tokens, deferred caches, exact actual work counts, submission wrapping/long columns,
atomic value/reset/splice operations, copy-tail failures and warm allocation-free callbacks.
Builds/tests have not been run for this source checkpoint: the coordinator explicitly holds all
builds while common validation owns the single build slot. Release net10 verification is pending.

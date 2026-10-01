# Browser HTML contract algorithms

Editing state, spellcheck, translation, dragging and legacy context-menu assignment
use the reviewed Browser helpers over native attributes and ordinary ancestors.
Document design mode uses the existing document state and focus/selection seams.
These facts do not become convenience properties on the native Element or Document.

The pinned document implementation delegates editing commands to an optional command
provider. This Browser has no such provider: the boolean command operations return
false and `queryCommandValue` returns the existing projected empty string. Every
operation retains its receiver check and argument conversions in their original
order; it does not execute the separate content-editing implementation.

HTML imports have no native import processor or produced document, so a link's
legacy `import` getter returns null. The legacy HTML manifest property reflects the
raw attribute, matching the pinned implementation. `requestAutocomplete` now raises
the named `NotSupportedError` because the Browser has no autofill service, replacing
the pinned operation's empty body; this observable correction was approved during
the migration review. `forceSpellCheck` likewise uses the helper's explicit missing
provider refusal.

The body setter validates an HTML body or frameset, preserves identical ownership,
replaces the actual old body when present, and otherwise appends to the actual
document element. Null reaches the HTML `HierarchyRequestError` rather than failing
the nullable interface conversion. An existing foreign document element remains
the setter's append target; a document without one refuses the assignment.

The pinned anchor getter is descendant text content. Its native binding uses the
bounded descendant-text reader and the existing actual child replacement operation.
The original generated projection omitted the HTMLAnchorElement.text setter; the
migration review approved restoring that WebIDL setter as an intentional descriptor
correction, in addition to the separately documented input selectionDirection fix.

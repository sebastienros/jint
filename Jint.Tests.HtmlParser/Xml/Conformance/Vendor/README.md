# W3C XML test corpus sources

The acceptance inventory is the W3C XML Test Suite 20130923 archive at
`https://www.w3.org/XML/Test/xmlts20130923.tar.gz`, SHA-256
`9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f`.
The ignored `../Cache/xmlts20130923.tar.gz` is restored explicitly; tests never
download it. It is for internal test use and must not be published as an artifact.

`Edinburgh/xmlconf/eduni/` contains exact archive bytes from the five initial
collections: `errata-2e`, `errata-3e`, `errata-4e`, `namespaces/1.0`, and
`namespaces/errata-1e`. Their `xmlconf.xml` notices permit redistribution
with copyright retained. All collection and per-file notices remain in place.
These catalogs contain 491 rows, which remain a subset of the 2,585-row gate.

`xmltest.zip` is James Clark's unchanged 1998-11-18 archive, SHA-256
`a919d7142fe6f72af51fc796b4df40732f385c9eb313b8993c6d39cc92acc410`.
Its embedded `xmltest/readme.html` permits redistribution of the unmodified
ZIP and restricts extracted redistribution. Tests read matching entries through
`ZipArchive`; no extracted copy is committed. The W3C archive changed nine
of the original paths and added thirteen more. `../corpus.lock.json` records
both sides' hashes for changed paths and the exact source of every W3C file.
The W3C-specific bytes are read from the verified ignored cache, never replaced
by the original ZIP's different bytes.

The W3C FAQ describes the suite's distribution under the W3C Software Notice
and License: <https://www.w3.org/XML/Test/faq.html> and
<https://www.w3.org/Consortium/Legal/copyright-software-19980720>.
Retain all existing notices when updating any vendored bytes. The source
catalog notices and `corpus.lock.json` provide the exact provenance.

Run `python3 ../Tools/import_corpus.py restore` only as an explicit developer
or CI step with network access. `import` verifies both pins and regenerates the
full manifest, file lock, and licensed Edinburgh slice. Tests fail their
integrity gate when the required archive is absent or altered.

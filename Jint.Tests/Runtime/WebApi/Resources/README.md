# Web Platform Tests URL corpus

`urltestdata.json` and `setters_tests.json` are vendored verbatim from the
[web-platform-tests](https://github.com/web-platform-tests/wpt) repository, at commit

    6745634413e4153144e23bc3e866d6b14d3e55e6

from `url/resources/urltestdata.json` and `url/resources/setters_tests.json`. `wpt-LICENSE.md` is that
commit's `LICENSE.md`; the corpus is redistributed here under the 3-Clause BSD License it carries.

`UrlCorpusTests` runs every row of both files directly against `Jint.WebApi.Url.Parsing`, without building an
engine — the parser is deliberately engine-free, so a conformance row costs a parse and nothing else. Rows
that do not pass are listed in that file's exclusion table, each with the divergence category it belongs to,
and the driver **fails on a stale exclusion**: a row that starts passing has to be removed from the table in
the same change that fixed it.

To update the corpus, resolve `master` to a concrete commit, fetch both files at that commit, replace them
here, update the SHA above and re-run the driver.

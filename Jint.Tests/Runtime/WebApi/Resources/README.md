# Web Platform Tests URL corpus

`urltestdata.json`, `setters_tests.json` and `urlpatterntestdata.json` are vendored verbatim from the
[web-platform-tests](https://github.com/web-platform-tests/wpt) repository, at commit

    6745634413e4153144e23bc3e866d6b14d3e55e6

from `url/resources/urltestdata.json`, `url/resources/setters_tests.json` and
`urlpattern/resources/urlpatterntestdata.json`. `wpt-LICENSE.md` is that commit's `LICENSE.md`; the corpus is
redistributed here under the 3-Clause BSD License it carries.

`UrlCorpusTests` runs every row of the first two files directly against `Jint.WebApi.Url.Parsing`, without
building an engine — the parser is deliberately engine-free, so a conformance row costs a parse and nothing else.

`UrlPatternCorpusTests` runs every row of `urlpatterntestdata.json` through one engine, driven by a port of the
WPT runner (`urlpattern/resources/urlpatterntests.js`) written in JavaScript, because what those rows assert is
the behaviour of the `URLPattern` object itself — the pattern strings its accessors return and the shape of its
`exec()` result — rather than of a parser underneath it.

Both drivers list the rows that do not pass in an exclusion table in the driver file, each with the divergence
category it belongs to, and **fail on a stale exclusion**: a row that starts passing has to be removed from the
table in the same change that fixed it.

To update the corpus, resolve `master` to a concrete commit, fetch the three files at that commit, replace them
here, update the SHA above and re-run the drivers.

// The ICU side of the formatRange golden table: every (locale, bag, pair) of interval_format.py's LOCALES, BAGS and
// PAIRS, formatted with { timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, printed in the columns
// `interval_format.py compare` reads. Run it under Node, whose ICU is the reference:
//
//   node probe-range-golden.js > icu-range-golden.tsv
//   python interval_format.py compare <cldr-json-root> icu-range-golden.tsv
var out = typeof print === 'function' ? print : function (s) { console.log(s); };
function esc(s) { return s.replace(/[^\x20-\x7e]|\\/g, function (c) { return String.fromCharCode(92) + 'u' + ('0000' + c.charCodeAt(0).toString(16)).slice(-4); }); }
var locales = ['ar', 'bg', 'bn', 'ca', 'cs', 'da', 'de', 'el', 'en', 'en-AU', 'en-GB', 'en-IN', 'es', 'es-MX', 'et', 'fa', 'fi',
  'fil', 'fr', 'fr-CA', 'he', 'hi', 'hr', 'hu', 'id', 'it', 'ja', 'ko', 'lt', 'lv', 'ms', 'nb', 'nl', 'pl', 'pt',
  'pt-PT', 'ro', 'ru', 'sk', 'sl', 'sr', 'sv', 'sw', 'ta', 'th', 'tr', 'uk', 'ur', 'vi', 'zh', 'zh-Hant'];
if (typeof process !== 'undefined' && process.env && process.env.PROBE_LOCALES) {
  locales = process.env.PROBE_LOCALES.split(',');
}
var bags = [
  ['E_d_MMMM', { weekday: 'short', day: 'numeric', month: 'long' }],
  ['EEEE_y_MMMM_d', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }],
  ['y_MMMM', { year: 'numeric', month: 'long' }],
  ['y_MMM_d', { year: 'numeric', month: 'short', day: 'numeric' }],
  ['MMM_d', { month: 'short', day: 'numeric' }],
  ['y_M_d', { year: 'numeric', month: 'numeric', day: 'numeric' }],
  ['yy_MM_dd', { year: '2-digit', month: '2-digit', day: '2-digit' }],
  ['MMMM', { month: 'long' }],
  ['d', { day: 'numeric' }],
  ['G_y', { era: 'short', year: 'numeric' }],
  ['j_mm', { hour: 'numeric', minute: '2-digit' }],
  ['H_mm_h23', { hour: 'numeric', minute: '2-digit', hourCycle: 'h23' }],
  ['j_mm_ss', { hour: 'numeric', minute: 'numeric', second: 'numeric' }],
  ['y_MMMM_d_j_mm', { year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: '2-digit' }],
  ['y_M_d_j_mm_ss', { year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric' }],
];
// Each pair differs first in the field its name says.
var pairs = [
  ['minute', Date.UTC(2022, 11, 24, 15, 7, 9), Date.UTC(2022, 11, 24, 15, 42, 31)],
  ['hour', Date.UTC(2022, 11, 24, 13, 4, 5), Date.UTC(2022, 11, 24, 17, 42, 31)],
  ['ampm', Date.UTC(2022, 11, 24, 9, 4, 5), Date.UTC(2022, 11, 24, 15, 7, 9)],
  ['day', Date.UTC(2022, 11, 24, 15, 7, 9), Date.UTC(2022, 11, 27, 9, 4, 5)],
  ['month', Date.UTC(2023, 2, 6, 9, 4, 5), Date.UTC(2023, 4, 9, 15, 7, 9)],
  ['year', Date.UTC(2022, 11, 24, 15, 7, 9), Date.UTC(2023, 2, 6, 9, 4, 5)],
];
var typeCodes = { literal: 'l', era: 'G', year: 'y', month: 'M', day: 'd', weekday: 'E', dayPeriod: 'a', hour: 'h', minute: 'm', second: 's', fractionalSecond: 'S', timeZoneName: 'z' };
var sourceCodes = { shared: 's', startRange: '1', endRange: '2' };
for (var li = 0; li < locales.length; li++) {
  for (var bi = 0; bi < bags.length; bi++) {
    var f = new Intl.DateTimeFormat(locales[li], Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, bags[bi][1]));
    for (var pi = 0; pi < pairs.length; pi++) {
      var parts = f.formatRangeToParts(pairs[pi][1], pairs[pi][2]);
      var single = parts.every(function (p) { return p.source === 'shared'; });
      // Jint writes a plain space for U+202F everywhere, and for U+2009 as well in a range; a range that collapses to one
      // date is format()'s output, which keeps U+2009 (the zh-Hant short date-time join).
      parts = parts.map(function (p) {
        var value = p.value.replace(/\u202f/g, ' ');
        return { type: p.type, source: p.source, value: single ? value : value.replace(/\u2009/g, ' ') };
      });
      var text = parts.map(function (p) { return p.value; }).join('');
      var encoded = parts.map(function (p) { return (typeCodes[p.type] || '?') + sourceCodes[p.source] + p.value.length; }).join(' ');
      out(esc([locales[li], bags[bi][0], pairs[pi][0], text, encoded].join('\t')));
    }
  }
}

// The ICU side of the golden table: every (locale, bag) of format_matcher.py's LOCALES and BAGS, formatted at its two
// instants with { timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, printed in the column order
// `format_matcher.py compare` reads. Run it under Node, whose ICU is the reference:
//
//   node probe-golden.js > icu-golden.tsv
//   python format_matcher.py compare <cldr-json-root> icu-golden.tsv
var out = typeof print === 'function' ? print : function (s) { console.log(s); };
function esc(s) { return s.replace(/[^\x20-\x7e]|\\/g, function (c) { return String.fromCharCode(92) + 'u' + ('0000' + c.charCodeAt(0).toString(16)).slice(-4); }); }
var dates = [new Date(Date.UTC(2022, 11, 24, 15, 7, 9)), new Date(Date.UTC(2023, 2, 6, 9, 4, 5))];
var locales = ['ar', 'bg', 'bn', 'ca', 'cs', 'da', 'de', 'el', 'en', 'en-AU', 'en-GB', 'en-IN', 'es', 'es-MX', 'et', 'fa', 'fi',
  'fil', 'fr', 'fr-CA', 'he', 'hi', 'hr', 'hu', 'id', 'it', 'ja', 'ko', 'lt', 'lv', 'ms', 'nb', 'nl', 'pl', 'pt',
  'pt-PT', 'ro', 'ru', 'sk', 'sl', 'sr', 'sv', 'sw', 'ta', 'th', 'tr', 'uk', 'ur', 'vi', 'zh', 'zh-Hant'];
var bags = [
  ['E_d_MMMM', { weekday: 'short', day: 'numeric', month: 'long' }],
  ['EEEE_d_MMMM', { weekday: 'long', day: 'numeric', month: 'long' }],
  ['E_y_MMM_d', { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' }],
  ['EEEE_y_MMMM_d', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }],
  ['y_MMMM', { year: 'numeric', month: 'long' }],
  ['y_MMM', { year: 'numeric', month: 'short' }],
  ['y_MM', { year: 'numeric', month: '2-digit' }],
  ['MMMM_d', { month: 'long', day: 'numeric' }],
  ['MMM_d', { month: 'short', day: 'numeric' }],
  ['M_d', { month: 'numeric', day: 'numeric' }],
  ['y_M_d', { year: 'numeric', month: 'numeric', day: 'numeric' }],
  ['yy_MM_dd', { year: '2-digit', month: '2-digit', day: '2-digit' }],
  ['MMMM', { month: 'long' }],
  ['EEEE', { weekday: 'long' }],
  ['d', { day: 'numeric' }],
  ['G_y', { era: 'short', year: 'numeric' }],
  ['GGGG_y_MMMM_d', { era: 'long', year: 'numeric', month: 'long', day: 'numeric' }],
  ['j_mm', { hour: 'numeric', minute: '2-digit' }],
  ['H_mm_h23', { hour: 'numeric', minute: '2-digit', hourCycle: 'h23' }],
  ['h_mm_h12', { hour: 'numeric', minute: '2-digit', hour12: true }],
  ['j_mm_ss', { hour: 'numeric', minute: 'numeric', second: 'numeric' }],
  ['E_j_mm', { weekday: 'short', hour: 'numeric', minute: '2-digit' }],
  ['y_MMMM_d_j_mm', { year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: '2-digit' }],
  ['MMM_d_j_mm', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }],
  ['y_M_d_j_mm_ss', { year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric' }],
];
var keys = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];
for (var li = 0; li < locales.length; li++) {
  for (var bi = 0; bi < bags.length; bi++) {
    var options = Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, bags[bi][1]);
    var f = new Intl.DateTimeFormat(locales[li], options);
    var ro = f.resolvedOptions();
    var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
    if (ro.hour !== undefined) {
      record += ',hourCycle=' + ro.hourCycle;
    }
    // ICU writes U+202F in its time patterns; the golden table, like Jint, writes a plain space.
    var texts = dates.map(function (d) { return f.format(d).replace(/ /g, ' '); });
    out(esc([locales[li], bags[bi][0]].concat(texts, [record]).join('\t')));
  }
}

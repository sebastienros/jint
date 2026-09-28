// The ICU side of the style golden table: every case `format_matcher.py style-cases` lists, formatted at the golden
// instants with { timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' } added, printed in the column order
// `format_matcher.py compare-styles` reads. A dateStyle/timeStyle case is asked for as it is; a Temporal case is asked
// for as the component bag AdjustDateTimeStyleFormat matched, which is what an ICU engine writes for it, since Node's V8
// does not ship Temporal. Run it under Node, whose ICU is the reference:
//
//   python format_matcher.py style-cases <cldr-json-root> > style-cases.tsv
//   node probe-styles.js style-cases.tsv > icu-styles.tsv
//   python format_matcher.py compare-styles <cldr-json-root> icu-styles.tsv
var fs = require('fs');
var out = function (s) { console.log(s); };
function esc(s) { return s.replace(/[^\x20-\x7e]|\\/g, function (c) { return String.fromCharCode(92) + 'u' + ('0000' + c.charCodeAt(0).toString(16)).slice(-4); }); }
var dates = [new Date(Date.UTC(2022, 11, 24, 15, 7, 9)), new Date(Date.UTC(2023, 2, 6, 9, 4, 5))];
var keys = ['dateStyle', 'timeStyle', 'weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second',
  'fractionalSecondDigits', 'timeZoneName'];
var lines = fs.readFileSync(process.argv[2], 'utf8').split(/\r?\n/);
for (var i = 0; i < lines.length; i++) {
  if (lines[i].length === 0) {
    continue;
  }
  var columns = lines[i].split('\t');
  var options = Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, JSON.parse(columns[2]));
  var f = new Intl.DateTimeFormat(columns[0], options);
  var ro = f.resolvedOptions();
  var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
  if (ro.hourCycle !== undefined) {
    record += ',hourCycle=' + ro.hourCycle;
  }
  // ICU writes U+202F in its time patterns; the golden table, like Jint, writes a plain space.
  var texts = dates.map(function (d) { return f.format(d).replace(/ /g, ' '); });
  out(esc([columns[0], columns[1]].concat(texts, [record]).join('\t')));
}

// Installed with Page.addScriptToEvaluateOnNewDocument, so it runs after the protocol's bindings are
// re-installed and before the document's first inline script — which is the order Jint.Browser's PageTarget
// commits a document in, and the only order in which this can work at all.
//
// What it is: upstream's own `message-queue.js`, with the callback wired straight to a CDP binding instead
// of to an async WebDriver script. `message-queue.js` opens by returning if the queue already exists,
// because "another script already set up the testdriver infrastructure" — this is that script. Nothing here
// re-implements the harness; `testharnessreport.js` still pushes `{type: "complete", tests, status}` and
// `testdriver-extra.js` still pushes `{type: "action", ...}`, both unchanged and both upstream's.
(function () {
  "use strict";

  if (window.__wptrunner_message_queue && window.__wptrunner_process_next_event) {
    return;
  }

  // Captured and then removed from the global, so a document that enumerates `window` does not find the
  // runner's plumbing among its results. Runtime.addBinding puts it back on the next document.
  var report = window["%(binding)s"];
  try {
    delete window["%(binding)s"];
  } catch (e) {
  }

  var queue = [];
  var nextId = 0;

  // testdriver-extra.js asks whether this window is the one actions are routed through; it is, because the
  // runner navigates the top-level page to the test itself. testharnessreport.js sets this too.
  window.__wptrunner_is_test_context = true;
  window.__wptrunner_url = null;
  window.__wptrunner_testdriver_callback = null;

  window.__wptrunner_message_queue = {
    push: function (item) {
      var id = nextId++;
      item.id = id;
      queue.push(item);
      window.__wptrunner_process_next_event();
      return id;
    },
    shift: function () {
      return queue.shift();
    }
  };

  window.__wptrunner_process_next_event = function () {
    var data = queue.shift();
    if (!data) {
      return;
    }

    var payload;

    switch (data.type) {
      case "complete":
        var tests = data.tests;
        var status = data.status;
        if (tests && status) {
          payload = [
            status.status,
            status.message,
            status.stack,
            tests.map(function (test) {
              return [test.name, test.status, test.message, test.stack];
            })
          ];
        } else {
          // A non-testharness document, which this runner does not ask for.
          payload = [];
        }
        break;
      case "action":
        payload = data;
        break;
      default:
        return;
    }

    if (!report) {
      return;
    }

    // One string, because that is the whole of what a binding can carry, and because a value crossing to
    // the runner would be a value belonging to a thread the runner is not on.
    report(JSON.stringify([data.type, payload]));
  };
})();

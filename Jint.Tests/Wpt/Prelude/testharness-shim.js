// A shim for the slice of web-platform-tests' testharness.js that the vendored `.any.js` suites use.
//
// This file is *not* vendored: upstream's testharness.js is ~2,700 lines of browser plumbing (a reporting
// DOM table, cross-window `fetch_tests_from_window`, `EventWatcher`, `format_value` over DOM nodes, worker
// bootstrapping) whose only job here would be to be switched off again. What the suites in
// Jint.Tests/Wpt/Vendor actually call is small enough to read in one sitting, and writing it out means the
// harness can hand results back to the driver as data rather than as rendered HTML.
//
// The contract with WptHarness.cs is one object, `__wpt`, carrying two live arrays:
//   __wpt.results     — { name, status, message } per test, in registration order, updated as each finishes.
//   __wpt.outstanding — the names of the tests that have started and not finished. Empty means the file is
//                       over, and while it is not, it is the message a stalled run reports.
// The driver supplies `__wptReadResource(path)` before this file runs; nothing else crosses the boundary.
//
// Deliberate divergences from upstream testharness.js, all of them recorded in Jint.Tests/Wpt/Vendor/README.md:
//   * `self.GLOBAL` reports neither window nor worker nor shadow realm, so the DOM-only branches of a
//     `.any.js` file skip themselves the way they do in a global they were not written for.
//   * `fetch` is a *resource loader* over the vendored tree, not the Fetch API. It is what
//     `fetch("resources/urltestdata.json")` needs and nothing more.
//   * Timers are the engine's own: this file installs no `setTimeout`. See WptHarness.cs.
(function (global) {
    'use strict';

    var results = [];
    // The names of the tests that have started and not finished — every async_test until it calls done(),
    // and every promise_test until its link of the chain settles. Empty means the file is over. Names rather
    // than a count, so a run that cannot finish says which test it is waiting on.
    var outstanding = [];
    var completionCallbacks = [];
    var promiseChain = null;

    function stopWaitingFor(test) {
        var at = outstanding.indexOf(test.name);
        if (at >= 0) {
            outstanding.splice(at, 1);
        }
    }

    // ---------------------------------------------------------------- assertions

    // Thrown by a failing assertion. Caught by the test wrapper that is running, never by the test itself:
    // `assert_throws_js` re-throws it rather than counting it as the exception it was asking for.
    function AssertionError(message) {
        this.message = message;
        this.stack = message;
    }
    AssertionError.prototype = Object.create(Error.prototype);
    AssertionError.prototype.constructor = AssertionError;
    AssertionError.prototype.name = 'AssertionError';
    AssertionError.prototype.toString = function () { return 'AssertionError: ' + this.message; };

    // https://github.com/web-platform-tests/wpt/blob/master/resources/testharness.js — `format_value`'s
    // escape table, so a test name built out of one matches the name the same file produces in a browser.
    var controlEscapes = {
        0: '0', 1: 'x01', 2: 'x02', 3: 'x03', 4: 'x04', 5: 'x05', 6: 'x06', 7: 'x07',
        8: 'b', 9: 't', 10: 'n', 11: 'v', 12: 'f', 13: 'r', 14: 'x0e', 15: 'x0f',
        16: 'x10', 17: 'x11', 18: 'x12', 19: 'x13', 20: 'x14', 21: 'x15', 22: 'x16', 23: 'x17',
        24: 'x18', 25: 'x19', 26: 'x1a', 27: 'x1b', 28: 'x1c', 29: 'x1d', 30: 'x1e', 31: 'x1f'
    };

    function format_value(value, seen) {
        if (seen === undefined) {
            seen = [];
        }
        if (typeof value === 'object' && value !== null) {
            if (seen.indexOf(value) >= 0) {
                return '[...]';
            }
            seen = seen.concat([value]);
        }
        if (Array.isArray(value)) {
            var elements = [];
            for (var i = 0; i < value.length; i++) {
                elements.push(format_value(value[i], seen));
            }
            return '[' + elements.join(', ') + ']';
        }
        switch (typeof value) {
            case 'string':
                var escaped = value.replace(/\\/g, '\\\\');
                for (var code in controlEscapes) {
                    // split/join rather than a RegExp: the table starts at U+0000, and a literal NUL in a
                    // pattern is exactly the kind of thing that is a different character class per engine.
                    escaped = escaped.split(String.fromCharCode(code)).join('\\' + controlEscapes[code]);
                }
                return '"' + escaped.replace(/"/g, '\\"') + '"';
            case 'boolean':
            case 'undefined':
                return String(value);
            case 'number':
                // -0 is a different value from 0 and has to look like one.
                if (value === 0 && 1 / value === -Infinity) {
                    return '-0';
                }
                return String(value);
            case 'bigint':
                return String(value) + 'n';
            case 'symbol':
                return value.toString();
            case 'function':
                return 'function "' + value.name + '"';
            case 'object':
                if (value === null) {
                    return 'null';
                }
                if (typeof value.constructor === 'function' && value.constructor.name) {
                    return 'object "' + value.constructor.name + '"';
                }
                return 'object';
            default:
                return String(value);
        }
    }

    function assert(condition, message) {
        if (!condition) {
            throw new AssertionError(message);
        }
    }

    function describe(description) {
        return description === undefined || description === null || description === ''
            ? ''
            : ' (' + description + ')';
    }

    // https://tc39.es/ecma262/#sec-samevalue, which is what testharness compares with: NaN equals NaN, and
    // +0 does not equal -0.
    function same_value(x, y) {
        if (y !== y) {
            return x !== x;
        }
        if (x === 0 && y === 0) {
            return 1 / x === 1 / y;
        }
        return x === y;
    }

    function assert_true(actual, description) {
        assert(actual === true, 'expected true got ' + format_value(actual) + describe(description));
    }

    function assert_false(actual, description) {
        assert(actual === false, 'expected false got ' + format_value(actual) + describe(description));
    }

    function assert_equals(actual, expected, description) {
        assert(same_value(actual, expected),
            'expected ' + format_value(expected) + ' but got ' + format_value(actual) + describe(description));
    }

    function assert_not_equals(actual, expected, description) {
        assert(!same_value(actual, expected),
            'got disallowed value ' + format_value(actual) + describe(description));
    }

    function assert_array_equals(actual, expected, description) {
        assert(typeof actual === 'object' && actual !== null && 'length' in actual,
            'value is ' + format_value(actual) + ', expected an array-like' + describe(description));
        assert(actual.length === expected.length,
            'lengths differ, expected array ' + format_value(expected) + ' length ' + expected.length +
            ', got ' + format_value(actual) + ' length ' + actual.length + describe(description));
        for (var i = 0; i < actual.length; i++) {
            assert(Object.prototype.hasOwnProperty.call(actual, i) === Object.prototype.hasOwnProperty.call(expected, i),
                'expected property ' + i + ' to be ' +
                (Object.prototype.hasOwnProperty.call(expected, i) ? 'present' : 'absent') + describe(description));
            assert(same_value(actual[i], expected[i]),
                'expected property ' + i + ' to be ' + format_value(expected[i]) +
                ' but got ' + format_value(actual[i]) + describe(description));
        }
    }

    function assert_unreached(description) {
        throw new AssertionError('reached unreachable code' + describe(description));
    }

    function assert_throws_js(constructor, func, description) {
        assert(typeof constructor === 'function', format_value(constructor) + ' is not a constructor');
        try {
            func.call(this);
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            assert(typeof e === 'object' && e !== null,
                'threw ' + format_value(e) + ', not an object' + describe(description));
            // testharness deliberately compares the constructor and the name rather than using instanceof,
            // so a subclass or a same-shaped object from elsewhere is not accepted for the base type.
            assert(e.constructor === constructor && e.name === constructor.name,
                'expected ' + constructor.name + ' but got ' + format_value(e) + describe(description));
            return;
        }
        throw new AssertionError('did not throw' + describe(description));
    }

    // https://webidl.spec.whatwg.org/#idl-DOMException-error-names — the legacy code each name carries, so a
    // DOMException assertion checks the same two things a browser's testharness checks. A name absent here
    // has no legacy code and `code` must read 0.
    var domExceptionCodes = {
        IndexSizeError: 1, HierarchyRequestError: 3, WrongDocumentError: 4, InvalidCharacterError: 5,
        NoModificationAllowedError: 7, NotFoundError: 8, NotSupportedError: 9, InUseAttributeError: 10,
        InvalidStateError: 11, SyntaxError: 12, InvalidModificationError: 13, NamespaceError: 14,
        InvalidAccessError: 15, TypeMismatchError: 17, SecurityError: 18, NetworkError: 19,
        AbortError: 20, URLMismatchError: 21, QuotaExceededError: 22, TimeoutError: 23,
        InvalidNodeTypeError: 24, DataCloneError: 25
    };

    function assert_throws_dom(type, func, description) {
        try {
            func.call(this);
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            assert(typeof e === 'object' && e !== null,
                'threw ' + format_value(e) + ', not an object' + describe(description));
            assert(e.constructor === global.DOMException,
                'expected a DOMException but got ' + format_value(e) + describe(description));
            if (typeof type === 'number') {
                assert(e.code === type,
                    'expected DOMException code ' + type + ' but got ' + e.code + describe(description));
            } else {
                assert(e.name === type,
                    'expected DOMException "' + type + '" but got "' + e.name + '"' + describe(description));
                var expectedCode = Object.prototype.hasOwnProperty.call(domExceptionCodes, type) ? domExceptionCodes[type] : 0;
                assert(e.code === expectedCode,
                    'expected DOMException "' + type + '" to carry code ' + expectedCode +
                    ' but it carried ' + e.code + describe(description));
            }
            return;
        }
        throw new AssertionError('did not throw' + describe(description));
    }

    function assert_throws_exactly(expected, func, description) {
        try {
            func.call(this);
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            assert(same_value(e, expected),
                'expected to throw ' + format_value(expected) + ' but threw ' + format_value(e) + describe(description));
            return;
        }
        throw new AssertionError('did not throw' + describe(description));
    }

    // ---------------------------------------------------------------- test objects

    function Test(name) {
        // Coerced, because a name is whatever the suite passed and the driver reads these back as JSON:
        // an absent name has to arrive as a string rather than as a missing property.
        this.name = String(name);
        this.phase = 'started';
        this.status = 'PASS';
        this.message = null;
        this.index = results.length;
        results.push({ name: this.name, status: 'NOTRUN', message: null });
    }

    Test.prototype.record = function () {
        results[this.index].status = this.status;
        results[this.index].message = this.message;
    };

    Test.prototype.fail = function (error) {
        if (this.phase === 'complete') {
            return;
        }
        this.status = 'FAIL';
        if (error instanceof AssertionError) {
            this.message = error.message;
        } else if (error && typeof error === 'object' && 'message' in error) {
            this.message = (error.name ? error.name + ': ' : '') + error.message;
        } else {
            this.message = 'threw ' + format_value(error);
        }
        this.record();
    };

    Test.prototype.complete = function () {
        if (this.phase === 'complete') {
            return;
        }
        this.phase = 'complete';
        this.record();
    };

    // The body of a step, shared by every entry point. A throw is the test's failure, except that an
    // AssertionError raised inside a nested assert_throws_* has already been classified.
    Test.prototype.step = function (func, thisObj) {
        if (this.phase === 'complete') {
            return undefined;
        }
        try {
            return func.apply(thisObj === undefined ? this : thisObj, Array.prototype.slice.call(arguments, 2));
        } catch (e) {
            this.fail(e);
            return undefined;
        }
    };

    Test.prototype.step_func = function (func, thisObj) {
        var test = this;
        return function () {
            return test.step.apply(test, [func, thisObj].concat(Array.prototype.slice.call(arguments)));
        };
    };

    Test.prototype.step_func_done = function (func, thisObj) {
        var test = this;
        return function () {
            if (func) {
                test.step.apply(test, [func, thisObj].concat(Array.prototype.slice.call(arguments)));
            }
            test.done();
        };
    };

    Test.prototype.unreached_func = function (description) {
        return this.step_func(function () {
            assert_unreached(description);
        });
    };

    Test.prototype.done = function () {
        if (this.phase === 'complete') {
            return;
        }
        this.complete();
        if (this.isAsync) {
            stopWaitingFor(this);
        }
    };

    function test(func, name) {
        var t = new Test(name);
        t.step(func, t, t);
        t.complete();
        return t;
    }

    function async_test(func, name) {
        if (typeof func !== 'function') {
            name = func;
            func = null;
        }
        var t = new Test(name);
        t.isAsync = true;
        outstanding.push(t.name);
        if (func) {
            t.step(func, t, t);
        }
        return t;
    }

    // Promise tests run in sequence — the next one starts only once the previous has finished, which is what
    // https://web-platform-tests.org/writing-tests/testharness-api.html#promise-tests specifies and what lets
    // the vendored url-setters.any.js and url-origin.any.js register plain test() cases from inside a promise
    // body and still have them collected in a predictable order.
    // A promise test is deliberately not marked `isAsync`: that flag is what makes `done()` stop the wait,
    // and a promise test is finished by its link of the chain settling rather than by its body calling done.
    function promise_test(func, name) {
        var t = new Test(name);
        outstanding.push(t.name);
        if (promiseChain === null) {
            promiseChain = Promise.resolve();
        }
        var settle = function () {
            t.complete();
            stopWaitingFor(t);
        };
        promiseChain = promiseChain.then(function () {
            var promise;
            try {
                promise = func.call(t, t);
            } catch (e) {
                t.fail(e);
                settle();
                return undefined;
            }
            return Promise.resolve(promise).then(settle, function (e) {
                t.fail(e);
                settle();
            });
        });
        return t;
    }

    // `setup` exists here for the one shape the corpus uses — a function run for its side effects before the
    // tests are registered. The properties form (`explicit_done`, `single_test`, timeouts) is a browser
    // scheduling concern the driver has no analogue for, so it is accepted and ignored.
    function setup(func) {
        if (typeof func === 'function') {
            func();
        }
    }

    function add_completion_callback(callback) {
        completionCallbacks.push(callback);
    }

    // Upstream's `done()` ends a run started with `explicit_done`. Here every file is run to completion by
    // the driver, so this only fires the completion callbacks; the results are already recorded.
    function done() {
        for (var i = 0; i < completionCallbacks.length; i++) {
            completionCallbacks[i](results);
        }
        completionCallbacks = [];
    }

    // ---------------------------------------------------------------- environment

    global.self = global;

    // A `.any.js` file is served into a global that tells it what it is. Jint's is none of the three, and
    // saying so is what makes the DOM-only branches (`document.createElement`, `location.searchParams`)
    // skip themselves rather than throw.
    global.GLOBAL = {
        isWindow: function () { return false; },
        isWorker: function () { return false; },
        isShadowRealm: function () { return false; }
    };

    // `/common/subset-tests.js` and `/common/subset-tests-by-key.js` read `location.search` to decide which
    // slice of a sharded suite to run. An empty search means "run the whole file", which is the union of
    // every `// META: variant=` line — see WptHarness.cs on why the driver does not shard.
    global.location = { search: '', hash: '', href: 'about:blank' };

    // Not the Fetch API: a loader for the vendored `resources/*.json` files a suite reads its corpus from,
    // and the only reason any of these files needs a network verb at all. There is deliberately no failure
    // path here — a reference the corpus does not hold, or one that tries to leave the vendored tree, is a
    // vendoring bug rather than a test result, so the driver's reader raises a CLR exception that erupts past
    // this function and is reported as a harness error for the whole file. See WptHarness.BuildEngine.
    global.fetch = function (input) {
        var text = __wptReadResource(String(input));
        return Promise.resolve({
            ok: true,
            status: 200,
            url: String(input),
            text: function () { return Promise.resolve(text); },
            json: function () { return Promise.resolve(JSON.parse(text)); }
        });
    };

    global.test = test;
    global.async_test = async_test;
    global.promise_test = promise_test;
    global.setup = setup;
    global.done = done;
    global.add_completion_callback = add_completion_callback;
    global.format_value = format_value;
    global.assert_true = assert_true;
    global.assert_false = assert_false;
    global.assert_equals = assert_equals;
    global.assert_not_equals = assert_not_equals;
    global.assert_array_equals = assert_array_equals;
    global.assert_unreached = assert_unreached;
    global.assert_throws_js = assert_throws_js;
    global.assert_throws_dom = assert_throws_dom;
    global.assert_throws_exactly = assert_throws_exactly;

    // Both of these are the live arrays, and the driver reads them through the object model rather than by
    // evaluating a script. That is not a shortcut: `engine.Evaluate` drains the event loop on its way out,
    // so the value it computed can already be out of date by the time the driver acts on it — and the window
    // that opens is exactly the one a due timer settles in, which made the drive loop declare a run stalled
    // that had in fact just finished.
    global.__wpt = {
        results: results,
        outstanding: outstanding
    };
})(globalThis);

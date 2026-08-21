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
//                       status is PASS, FAIL, NOTRUN, or PRECONDITION_FAILED — upstream's outcome for a test
//                       that gave up through `assert_implements_optional`.
//   __wpt.outstanding — the names of the tests that have started and not finished. Empty means the file is
//                       over, and while it is not, it is the message a stalled run reports.
// The driver supplies `__wptReadResource(path)` before this file runs; nothing else crosses the boundary.
//
// Deliberate divergences from upstream testharness.js, all of them recorded in Jint.Tests/Wpt/Vendor/README.md:
//   * `self.GLOBAL` reports neither window nor worker nor shadow realm, so the DOM-only branches of a
//     `.any.js` file skip themselves the way they do in a global they were not written for.
//   * `fetch` is a *resource loader* over the vendored tree, not the Fetch API. It is what
//     `fetch("resources/urltestdata.json")` needs and nothing more.
//   * Timers are the engine's own: this file installs no `setTimeout`, and its `step_timeout` is a thin
//     forwarder onto the engine's, so a scheduled callback rides the shipped TimerQueue. See WptHarness.cs.
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

    // Upstream's `assert_implements_optional` failure. It is an AssertionError, so everything that classifies
    // one classifies this too; what it changes is the status the test is recorded with — PRECONDITION_FAILED,
    // "the feature this test needs is optional and this implementation does not have it", which is neither a
    // pass nor an ordinary failure. The driver has no third bucket, so such a test needs an exclusion like any
    // other; the status is what tells a reader why.
    function OptionalFeatureUnsupportedError(message) {
        AssertionError.call(this, message);
    }
    OptionalFeatureUnsupportedError.prototype = Object.create(AssertionError.prototype);
    OptionalFeatureUnsupportedError.prototype.constructor = OptionalFeatureUnsupportedError;

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

    // The type check is upstream's and is not decoration: `undefined > 0` is false and `"10" > 9` is true, so
    // without it a comparison against a non-number would report the wrong thing about the wrong value.
    function assert_greater_than(actual, expected, description) {
        assert(typeof actual === 'number',
            'expected a number but got a ' + typeof actual + describe(description));
        assert(actual > expected,
            'expected a number greater than ' + format_value(expected) +
            ' but got ' + format_value(actual) + describe(description));
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

    // Membership by `indexOf`, deliberately not by `same_value`: upstream documents that this one "doesn't
    // handle NaN or ±0 correctly", and a shim that quietly handled them would accept a suite a browser fails.
    function assert_in_array(actual, expected, description) {
        assert(expected.indexOf(actual) !== -1,
            'value ' + format_value(actual) + ' not in array ' + format_value(expected) + describe(description));
    }

    // Deprecated upstream since 2015 (https://github.com/web-platform-tests/wpt/issues/2033) and carrying its
    // author's own "this needs to be improved a great deal", so it is copied as it is rather than as it ought
    // to be. Three of its quirks are load-bearing for the suites that still call it: the walk is `for..in`,
    // which reaches inherited enumerable properties as well as own ones, while the presence check on the other
    // side is `hasOwnProperty`; a value already on the stack is not descended into again, so a cycle on the
    // `actual` side terminates and one reachable only from `expected` does not; and the second loop is what
    // catches a property `expected` has and `actual` does not, which the first loop cannot see.
    function assert_object_equals(actual, expected, description) {
        assert(typeof actual === 'object' && actual !== null,
            'value is ' + format_value(actual) + ', expected object' + describe(description));

        function check_equal(actual, expected, stack) {
            stack.push(actual);
            var p;
            for (p in actual) {
                // hasOwnProperty through Object.prototype rather than off the object, as everywhere else in
                // this file: upstream calls it as a method, which a null-prototype object does not have.
                assert(Object.prototype.hasOwnProperty.call(expected, p),
                    'unexpected property ' + format_value(p) + describe(description));
                if (typeof actual[p] === 'object' && actual[p] !== null) {
                    if (stack.indexOf(actual[p]) === -1) {
                        check_equal(actual[p], expected[p], stack);
                    }
                } else {
                    assert(same_value(actual[p], expected[p]),
                        'property ' + format_value(p) + ' expected ' + format_value(expected[p]) +
                        ' got ' + format_value(actual[p]) + describe(description));
                }
            }
            for (p in expected) {
                assert(Object.prototype.hasOwnProperty.call(actual, p),
                    'expected property ' + format_value(p) + ' missing' + describe(description));
            }
            stack.pop();
        }

        check_equal(actual, expected, []);
    }

    // https://webidl.spec.whatwg.org/#dfn-class-string — the string an interface's `@@toStringTag` makes
    // `Object.prototype.toString` report, which is how a suite checks what it was handed actually is one.
    function assert_class_string(object, class_string, description) {
        var actual = Object.prototype.toString.call(object);
        var expected = '[object ' + class_string + ']';
        assert(same_value(actual, expected),
            'expected ' + format_value(expected) + ' but got ' + format_value(actual) + describe(description));
    }

    function assert_own_property(object, property_name, description) {
        assert(Object.prototype.hasOwnProperty.call(object, property_name),
            'expected property ' + format_value(property_name) + ' missing' + describe(description));
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

    // https://webidl.spec.whatwg.org/#dfn-error-names-table — the two tables upstream's assert_throws_dom_impl
    // carries, copied entry for entry. What they encode is not merely a lookup: `nameCodeMap` is a *closed
    // set*, so a name it does not hold is a bug in the test rather than a name whose legacy code is 0, and
    // saying so is what stops a typo'd expectation from being satisfied by a typo'd implementation. The
    // entries mapped to 0 are the names added after the legacy code list closed, and they are listed rather
    // than defaulted for exactly that reason.
    //
    // Two absences are deliberate and are upstream's, not shortcuts. `QuotaExceededError` is in neither table
    // since it became an interface of its own (https://webidl.spec.whatwg.org/#quotaexceedederror), which is
    // why the name and the legacy code 22 are refused below and sent to assert_throws_quotaexceedederror.
    // `QUOTA_EXCEEDED_ERR` left `codenameNameMap` with it, so it is refused as an unrecognized name.
    var codenameNameMap = {
        INDEX_SIZE_ERR: 'IndexSizeError',
        HIERARCHY_REQUEST_ERR: 'HierarchyRequestError',
        WRONG_DOCUMENT_ERR: 'WrongDocumentError',
        INVALID_CHARACTER_ERR: 'InvalidCharacterError',
        NO_MODIFICATION_ALLOWED_ERR: 'NoModificationAllowedError',
        NOT_FOUND_ERR: 'NotFoundError',
        NOT_SUPPORTED_ERR: 'NotSupportedError',
        INUSE_ATTRIBUTE_ERR: 'InUseAttributeError',
        INVALID_STATE_ERR: 'InvalidStateError',
        SYNTAX_ERR: 'SyntaxError',
        INVALID_MODIFICATION_ERR: 'InvalidModificationError',
        NAMESPACE_ERR: 'NamespaceError',
        INVALID_ACCESS_ERR: 'InvalidAccessError',
        TYPE_MISMATCH_ERR: 'TypeMismatchError',
        SECURITY_ERR: 'SecurityError',
        NETWORK_ERR: 'NetworkError',
        ABORT_ERR: 'AbortError',
        URL_MISMATCH_ERR: 'URLMismatchError',
        TIMEOUT_ERR: 'TimeoutError',
        INVALID_NODE_TYPE_ERR: 'InvalidNodeTypeError',
        DATA_CLONE_ERR: 'DataCloneError'
    };

    var nameCodeMap = {
        IndexSizeError: 1,
        HierarchyRequestError: 3,
        WrongDocumentError: 4,
        InvalidCharacterError: 5,
        NoModificationAllowedError: 7,
        NotFoundError: 8,
        NotSupportedError: 9,
        InUseAttributeError: 10,
        InvalidStateError: 11,
        SyntaxError: 12,
        InvalidModificationError: 13,
        NamespaceError: 14,
        InvalidAccessError: 15,
        TypeMismatchError: 17,
        SecurityError: 18,
        NetworkError: 19,
        AbortError: 20,
        URLMismatchError: 21,
        TimeoutError: 23,
        InvalidNodeTypeError: 24,
        DataCloneError: 25,

        EncodingError: 0,
        NotReadableError: 0,
        UnknownError: 0,
        ConstraintError: 0,
        DataError: 0,
        TransactionInactiveError: 0,
        ReadOnlyError: 0,
        VersionError: 0,
        OperationError: 0,
        NotAllowedError: 0,
        OptOutError: 0
    };

    // The inverse over the names that have a legacy code, so the numeric call form can say which name it
    // means. Built once here rather than per call, which is upstream's only cost this shim declines to pay.
    var codeNameMap = {};
    for (var mappedName in nameCodeMap) {
        if (nameCodeMap[mappedName] > 0) {
            codeNameMap[nameCodeMap[mappedName]] = mappedName;
        }
    }

    // The synchronous form always means this global's DOMException. `promise_rejects_dom` may be told which
    // global the exception is expected to come from, so the matching half takes the constructor as an
    // argument — upstream splits the two the same way and for the same reason.
    function assert_throws_dom(type, func, description) {
        assert_throws_dom_impl(type, func, description, global.DOMException);
    }

    function assert_throws_dom_impl(type, func, description, constructor) {
        try {
            func.call(this);
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            assert(typeof e === 'object' && e !== null,
                'threw ' + format_value(e) + ', not an object' + describe(description));
            // Without this a type that is neither would fall past both branches below and leave nothing to
            // check, so the assertion would pass on any exception at all.
            assert(typeof type === 'number' || typeof type === 'string',
                format_value(type) + ' is not a number or string' + describe(description));

            // The refusals below are upstream's, message for message, and they are thrown rather than
            // asserted because none of them is about the exception: the *test* named something the tables
            // cannot, and reporting that as "it did not match" would let a typo read as a divergence. Note
            // where they sit — inside the catch, as upstream — so a body that threw nothing is still
            // reported as "did not throw" rather than as a test bug.
            var required = {};
            var name;
            if (typeof type === 'number') {
                if (type === 0) {
                    throw new AssertionError('Test bug: ambiguous DOMException code 0 passed to assert_throws_dom()');
                }
                if (type === 22) {
                    throw new AssertionError('Test bug: QuotaExceededError needs to be tested for using assert_throws_quotaexceedederror()');
                }
                if (!Object.prototype.hasOwnProperty.call(codeNameMap, type)) {
                    throw new AssertionError('Test bug: unrecognized DOMException code "' + type + '" passed to assert_throws_dom()');
                }
                name = codeNameMap[type];
                required.code = type;
            } else {
                // Upstream's own QuotaExceededError check sits here but reads `name`, which it does not
                // assign until the line after, so the string form never reaches it and is refused one line
                // later as an unrecognized name instead. The call is refused either way and no vendored test
                // makes it; this shim tests `type` so that the refusal names the assertion to use, which is
                // what upstream's message plainly intends to say. Mirroring the line as written would mean
                // committing a branch that is dead by construction, which is why this is the one place the
                // two differ — in the wording of a refusal, never in whether there is one.
                if (type === 'QuotaExceededError') {
                    throw new AssertionError('Test bug: QuotaExceededError needs to be tested for using assert_throws_quotaexceedederror()');
                }
                name = Object.prototype.hasOwnProperty.call(codenameNameMap, type) ? codenameNameMap[type] : type;
                if (!Object.prototype.hasOwnProperty.call(nameCodeMap, name)) {
                    throw new AssertionError('Test bug: unrecognized DOMException code name or name "' + type + '" passed to assert_throws_dom()');
                }
                required.code = nameCodeMap[name];
            }

            // Upstream's condition for checking the name as well as the code. A legacy exception object wore
            // an all-caps name or called itself "DOMException", and comparing that against a modern name would
            // say nothing; a name whose legacy code is 0 is checked either way, because there the code says
            // nothing on its own.
            if (required.code === 0 ||
                ('name' in e && e.name !== e.name.toUpperCase() && e.name !== 'DOMException')) {
                required.name = name;
            }

            for (var prop in required) {
                // `==` rather than `===`, as upstream, and `prop in e` first so that a missing property is a
                // failure rather than an undefined that compares loosely equal to nothing.
                assert(prop in e && e[prop] == required[prop],
                    'threw ' + format_value(e) + ', which is not a DOMException ' + type + ': property "' + prop +
                    '" is ' + format_value(e[prop]) + ', expected ' + format_value(required[prop]) + describe(description));
            }

            // Last, so that a wrong shape is reported by the more informative checks above first.
            assert(e.constructor === constructor,
                'expected a DOMException but got ' + format_value(e) + describe(description));
            return;
        }
        throw new AssertionError('did not throw' + describe(description));
    }

    // https://webidl.spec.whatwg.org/#quotaexceedederror — since 2025 `QuotaExceededError` is an interface of
    // its own deriving from DOMException and carrying `quota` and `requested`, and upstream gave it its own
    // assertion because the plain DOMException one would silently accept the legacy shape. Both call forms are
    // implemented for the same reason `promise_rejects_dom` implements both: the argument-shape sniffing is
    // what keeps a suite that spelled the constructor form from quietly losing its description. `requested`
    // and `quota` may each be null, a number, or a predicate over the value.
    function assert_throws_quotaexceedederror(funcOrConstructor, requestedOrFunc, quotaOrRequested, descriptionOrQuota, maybeDescription) {
        var constructor, func, requested, quota, description;
        if (funcOrConstructor.name === 'QuotaExceededError') {
            constructor = funcOrConstructor;
            func = requestedOrFunc;
            requested = quotaOrRequested;
            quota = descriptionOrQuota;
            description = maybeDescription;
        } else {
            constructor = global.QuotaExceededError;
            func = funcOrConstructor;
            requested = requestedOrFunc;
            quota = quotaOrRequested;
            description = descriptionOrQuota;
            assert(maybeDescription === undefined,
                'Too many args passed to no-constructor version of assert_throws_quotaexceedederror');
        }

        try {
            func.call(this);
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            assert(typeof e === 'object' && e !== null,
                'threw ' + format_value(e) + ', not an object' + describe(description));

            var required = { code: 22, name: 'QuotaExceededError' };
            if (typeof requested !== 'function') {
                required.requested = requested;
            }
            if (typeof quota !== 'function') {
                required.quota = quota;
            }

            for (var prop in required) {
                // `==` rather than `===`, as upstream: a `requested` the caller gave as null is satisfied by a
                // null property, and the properties are numbers either way.
                assert(prop in e && e[prop] == required[prop],
                    'threw ' + format_value(e) + ', which is not a correct QuotaExceededError: property "' + prop +
                    '" is ' + format_value(e[prop]) + ', expected ' + format_value(required[prop]) + describe(description));
            }

            if (typeof requested === 'function') {
                assert(requested(e.requested),
                    'the QuotaExceededError\'s requested value ' + format_value(e.requested) +
                    ' did not pass the predicate' + describe(description));
            }
            if (typeof quota === 'function') {
                assert(quota(e.quota),
                    'the QuotaExceededError\'s quota value ' + format_value(e.quota) +
                    ' did not pass the predicate' + describe(description));
            }

            // Last, so that a wrong shape is reported by the more informative checks above first.
            assert(e.constructor === constructor,
                'expected a QuotaExceededError but got ' + format_value(e) + describe(description));
            return;
        }
        throw new AssertionError('did not throw' + describe(description));
    }

    // A feature the specification *requires*, missing. Upstream's own body is
    // `assert(!!condition, "assert_implements", description)` — an ordinary AssertionError and therefore an
    // ordinary FAIL. The pairing with `assert_implements_optional` below is the whole point of having two:
    // upstream splits "the spec says this must exist" from "the spec says this may exist", and only the
    // second is excused as PRECONDITION_FAILED. A shim that collapsed them would quietly demote a real gap.
    function assert_implements(condition, description) {
        assert(!!condition, 'assert_implements' + describe(description));
    }

    // An optional feature this implementation does not have. Not a failure: the test is recorded
    // PRECONDITION_FAILED, which is upstream's third outcome and this shim's only use of one.
    function assert_implements_optional(condition, description) {
        if (!condition) {
            throw new OptionalFeatureUnsupportedError(description);
        }
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

    // ---------------------------------------------------------------- scheduling

    // Upstream's `step_timeout`, which exists so that a suite never names `setTimeout` itself: a browser
    // multiplies every wpt timeout by a per-run factor, and a test that reached for the raw timer would
    // ignore it. The factor is 1 here — there is no slow-device mode to accommodate — so this is
    // `setTimeout` plus upstream's argument forwarding and nothing else.
    //
    // It is deliberately the *engine's* `setTimeout`, resolved at call time through the global exactly as
    // upstream resolves it, so a scheduled callback rides the shipped TimerQueue that WptHarness pumps and
    // HTML's ordering (one due timer per microtask checkpoint) is the ordering a suite sees. There is no
    // timer implementation anywhere in this file; see WptHarness.cs.
    function step_timeout(func, timeout) {
        var outerThis = this;
        var args = Array.prototype.slice.call(arguments, 2);
        return global.setTimeout(function () {
            func.apply(outerThis, args);
        }, timeout);
    }

    // ---------------------------------------------------------------- test objects

    function Test(name) {
        // Coerced, because a name is whatever the suite passed and the driver reads these back as JSON:
        // an absent name has to arrive as a string rather than as a missing property.
        this.name = String(name);
        this.phase = 'started';
        this.status = 'PASS';
        this.message = null;
        this.cleanupCallbacks = [];
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
        this.status = error instanceof OptionalFeatureUnsupportedError ? 'PRECONDITION_FAILED' : 'FAIL';
        if (error instanceof AssertionError) {
            this.message = error.message;
        } else if (error && typeof error === 'object' && 'message' in error) {
            this.message = (error.name ? error.name + ': ' : '') + error.message;
        } else {
            this.message = 'threw ' + format_value(error);
        }
        this.record();
    };

    // https://web-platform-tests.org/writing-tests/testharness-api.html#cleanup — a function to undo whatever
    // the test did to shared state. The streams corpus is what makes this load-bearing rather than tidy: its
    // `patched-global` suites replace `Object.prototype.then`, `Promise.prototype.then` and `ReadableStream`
    // itself, and a cleanup that did not run would take every later test in the file down with it.
    Test.prototype.add_cleanup = function (callback) {
        this.cleanupCallbacks.push(callback);
    };

    // Run before the phase flips, so a throwing cleanup can still be recorded against the test that
    // registered it. Two deliberate choices. Every callback runs even if an earlier one threw — they undo
    // *different* pieces of shared state, and skipping the rest would poison the file exactly as not running
    // them at all would. And a throw is attributed to this test rather than, as upstream, promoted to a
    // harness-level ERROR: the driver's unit of report is the test, and only if the test is otherwise
    // passing, so a real failure's message is never replaced by the wreckage it left behind.
    // A cleanup that returns a promise is *not* awaited, which upstream does (`AsyncCleanup`); no vendored
    // suite has one, and a shim that pretended to wait would be the kind of silence this file exists to avoid.
    Test.prototype.cleanup = function () {
        for (var i = 0; i < this.cleanupCallbacks.length; i++) {
            try {
                this.cleanupCallbacks[i]();
            } catch (e) {
                if (this.status === 'PASS') {
                    this.fail(e);
                }
            }
        }
        this.cleanupCallbacks = [];
    };

    Test.prototype.complete = function () {
        if (this.phase === 'complete') {
            return;
        }
        this.cleanup();
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

    // The test-bound form of `step_timeout` above: the callback runs as a step of this test, so a throw
    // inside it fails the test rather than erupting into whatever happened to be pumping the event loop.
    Test.prototype.step_timeout = function (func, timeout) {
        var args = Array.prototype.slice.call(arguments, 2);
        return step_timeout.apply(this, [this.step_func(func), timeout].concat(args));
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

    // ---------------------------------------------------------------- promise rejections

    // Upstream re-wraps the promise it was handed in one built here so that a promise from another realm can
    // still be awaited. Jint has a single realm, so what this actually buys is thenable adoption — whatever a
    // suite hands over is driven through its own `then` — and it is kept in that shape because upstream's
    // contract is written against it.
    function bring_promise_to_current_realm(promise) {
        return new Promise(promise.then.bind(promise));
    }

    // Each of the three waits for the rejection and then hands the reason to the matching `assert_throws_*`
    // through a thunk that re-throws it, so what counts as the right exception has exactly one implementation
    // and the synchronous and asynchronous forms of an assertion can never drift apart.
    //
    // A promise that *resolves* fails through `test.unreached_func`, which is a step of the test: it records
    // the failure on the test and returns normally, so the promise this hands back is fulfilled rather than
    // rejected and the promise_test that returned it still settles.
    function promise_rejects_js(test, constructor, promise, description) {
        return bring_promise_to_current_realm(promise)
            .then(test.unreached_func('Should have rejected: ' + description))
            .catch(function (e) {
                assert_throws_js(constructor, function () { throw e; }, description);
            });
    }

    // Two ways to call this, exactly as upstream: with the promise third, or — when the DOMException is
    // expected to come from another global — with that global's DOMException constructor third and the
    // promise fourth. The two are told apart by the third argument being a function named "DOMException",
    // which is also why the no-constructor form asserts that nothing was passed in the fifth position: a
    // suite that spelled the constructor form wrong would otherwise silently lose its description.
    function promise_rejects_dom(test, type, promiseOrConstructor, descriptionOrPromise, maybeDescription) {
        var constructor, promise, description;
        if (typeof promiseOrConstructor === 'function' && promiseOrConstructor.name === 'DOMException') {
            constructor = promiseOrConstructor;
            promise = descriptionOrPromise;
            description = maybeDescription;
        } else {
            constructor = global.DOMException;
            promise = promiseOrConstructor;
            description = descriptionOrPromise;
            assert(maybeDescription === undefined,
                'Too many args passed to no-constructor version of promise_rejects_dom, or accidentally explicitly passed undefined');
        }
        return bring_promise_to_current_realm(promise)
            .then(test.unreached_func('Should have rejected: ' + description))
            .catch(function (e) {
                assert_throws_dom_impl(type, function () { throw e; }, description, constructor);
            });
    }

    function promise_rejects_exactly(test, exception, promise, description) {
        return bring_promise_to_current_realm(promise)
            .then(test.unreached_func('Should have rejected: ' + description))
            .catch(function (e) {
                assert_throws_exactly(exception, function () { throw e; }, description);
            });
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
    global.step_timeout = step_timeout;
    global.promise_test = promise_test;
    global.setup = setup;
    global.done = done;
    global.add_completion_callback = add_completion_callback;
    global.format_value = format_value;
    global.assert_true = assert_true;
    global.assert_false = assert_false;
    global.assert_equals = assert_equals;
    global.assert_not_equals = assert_not_equals;
    global.assert_greater_than = assert_greater_than;
    global.assert_array_equals = assert_array_equals;
    global.assert_in_array = assert_in_array;
    global.assert_object_equals = assert_object_equals;
    global.assert_class_string = assert_class_string;
    global.assert_own_property = assert_own_property;
    global.assert_unreached = assert_unreached;
    global.assert_throws_js = assert_throws_js;
    global.assert_throws_dom = assert_throws_dom;
    global.assert_throws_quotaexceedederror = assert_throws_quotaexceedederror;
    global.assert_throws_exactly = assert_throws_exactly;
    global.assert_implements = assert_implements;
    global.assert_implements_optional = assert_implements_optional;
    // Upstream exposes both of these, and the WebCryptoAPI suites use the first: `err instanceof
    // AssertionError` is how a `catch` that classifies its own errors tells a failed assertion — which it must
    // re-throw — from the operation's own rejection, which is what it is there to inspect.
    global.AssertionError = AssertionError;
    global.OptionalFeatureUnsupportedError = OptionalFeatureUnsupportedError;
    global.promise_rejects_js = promise_rejects_js;
    global.promise_rejects_dom = promise_rejects_dom;
    global.promise_rejects_exactly = promise_rejects_exactly;

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

/*
 * The browser lane's `resources/testdriver-vendor.js`.
 *
 * Upstream ships an EMPTY file at this path and declares every automation call in `testdriver.js` as a
 * member of `window.test_driver_internal` that throws "… is not implemented by testdriver-vendor.js". This
 * file is the implementation that replaces them, the same way `testharnessreport.js` is replaced: `WptServer`
 * takes it as a per-instance overlay, so nothing under `Jint.Tests/Wpt/Vendor/` changes and every other
 * caller still gets upstream's empty stub and upstream's rejections.
 *
 * Four things about it are worth knowing before editing.
 *
 * 1. IT DISPATCHES NOTHING ITSELF. `__jintWptInput` is a CLR delegate the driver installs on every page
 *    engine, and every call below hands it one JSON string describing one input event. What that becomes is
 *    `Jint.Browser`'s `InputDispatcher` — the same flat hit test and the same key dispatch `Input.dispatchMouseEvent`
 *    and `Input.dispatchKeyEvent` reach. A second implementation here would be a second answer to "what does
 *    a click do", and the whole value of running these documents is that there is only one.
 *
 * 2. WHAT IS HERE IS COORDINATES AND UNPACKING, WHICH IS ALL THAT CAN BE. Resolving a WebDriver `origin` to
 *    a point needs the DOM (`getClientRects`), and a page's DOM belongs to that page's thread — so the
 *    resolution has to happen in the page, and only the resulting numbers cross. That is the same boundary
 *    `testharnessreport.js` states for results, in the other direction.
 *
 * 3. IT REMOVES ITS OWN GLOBAL, before the test file is fetched, so a document that enumerates `window` does
 *    not find this lane's plumbing among the results. `testharnessreport.js` runs first and has already made
 *    the hook non-enumerable, for the documents that never load this file at all; this is where it goes for
 *    good.
 *
 * 4. THREE CALLS ARE IMPLEMENTED AND THE REST STAY UPSTREAM'S REJECTIONS. `click`, `send_keys` and
 *    `action_sequence` are what the vendored documents drive input through. Cookies, permissions, window
 *    rects, virtual authenticators and the BiDi surface are not implemented, and are deliberately left to
 *    throw upstream's own "not implemented by testdriver-vendor.js" rather than accepted and ignored: a call
 *    that silently succeeds and changes nothing turns a missing environment into an assertion failure three
 *    lines later, which is the harder failure to read. `in_automation` is set for the same reason — it is
 *    upstream's own flag for "an implementation is present", and it makes an unimplemented call fail at the
 *    call rather than fall back to waiting for a user who is not there.
 *
 * Upstream: resources/testdriver-vendor.js at the commit named in Jint.Tests/Wpt/Vendor/README.md.
 */

(function () {
    "use strict";

    var send = self.__jintWptInput;
    delete self.__jintWptInput;

    if (typeof send !== "function" || typeof window.test_driver_internal !== "object") {
        // A page loaded by something other than this driver, or one that never pulled in testdriver.js.
        return;
    }

    var internal = window.test_driver_internal;

    // Upstream's own flag for "the internal methods are implemented for automation purposes". With it set,
    // a call this file does not override fails immediately instead of falling back to waiting for a real
    // user, which is what `click`'s and `send_keys`'s default implementations do.
    internal.in_automation = true;

    /* The pointer's position, which is what a `pointerMove` with `origin: "pointer"` is relative to and what
     * a `pointerDown` with no move before it happens at. WebDriver starts every input source at the origin. */
    var pointerX = 0;
    var pointerY = 0;

    function mouse(type, x, y, extra) {
        pointerX = x;
        pointerY = y;

        var message = { kind: "mouse", type: type, x: x, y: y, button: 0, buttons: 0, clickCount: 0 };

        for (var key in extra) {
            if (Object.prototype.hasOwnProperty.call(extra, key)) {
                message[key] = extra[key];
            }
        }

        send(JSON.stringify(message));
    }

    function key(type, value) {
        send(JSON.stringify({
            kind: "key",
            type: type,
            key: value.key,
            code: value.code || "",
            text: value.text === undefined ? "" : value.text
        }));
    }

    /*
     * https://w3c.github.io/webdriver/#dfn-get-coordinates-relative-to-an-origin. `viewport` is the point
     * itself, `pointer` is an offset from where the pointer is, and an element is an offset from its
     * in-view centre point — which is the same centre `testdriver.js` computes for a plain `click`, so the
     * two paths land on the same pixel of the same flat box.
     */
    function resolve(action) {
        var x = action.x === undefined ? 0 : action.x;
        var y = action.y === undefined ? 0 : action.y;
        var origin = action.origin === undefined ? "viewport" : action.origin;

        if (origin === "viewport") {
            return [x, y];
        }

        if (origin === "pointer") {
            return [pointerX + x, pointerY + y];
        }

        var rect = origin.getClientRects()[0];

        if (!rect) {
            throw new Error("origin element has no client rectangle");
        }

        var left = Math.max(0, rect.left);
        var right = Math.min(window.innerWidth, rect.right);
        var top = Math.max(0, rect.top);
        var bottom = Math.min(window.innerHeight, rect.bottom);

        return [Math.floor(0.5 * (left + right)) + x, Math.floor(0.5 * (top + bottom)) + y];
    }

    /*
     * https://w3c.github.io/webdriver/#keyboard-actions — the code points WebDriver reserves for the keys a
     * character cannot spell. Only the ones a vendored document could plausibly send are here; anything else
     * in the private use area is passed through as the character it is, which is what a browser does with a
     * key it has no name for.
     */
    var specialKeys = {
        "": { key: "Backspace", code: "Backspace" },
        "": { key: "Tab", code: "Tab" },
        "": { key: "Enter", code: "Enter", text: "\r" },
        "": { key: "Enter", code: "NumpadEnter", text: "\r" },
        "": { key: "Escape", code: "Escape" },
        "": { key: " ", code: "Space", text: " " },
        "": { key: "End", code: "End" },
        "": { key: "Home", code: "Home" },
        "": { key: "Delete", code: "Delete" },
        "": { key: "ArrowLeft", code: "ArrowLeft" },
        "": { key: "ArrowUp", code: "ArrowUp" },
        "": { key: "ArrowRight", code: "ArrowRight" },
        "": { key: "ArrowDown", code: "ArrowDown" }
    };

    function describe(value) {
        return specialKeys[value] || { key: value, code: "", text: value };
    }

    internal.click = function (element, coords) {
        mouse("moved", coords.x, coords.y, {});
        mouse("pressed", coords.x, coords.y, { buttons: 1, clickCount: 1 });
        mouse("released", coords.x, coords.y, { buttons: 0, clickCount: 1 });
        return Promise.resolve();
    };

    internal.send_keys = function (element, keys) {
        // https://w3c.github.io/webdriver/#element-send-keys step 8: the element is focused first, and the
        // keys then go wherever focus is — which is what makes send_keys type into the control it names.
        if (typeof element.focus === "function") {
            element.focus();
        }

        for (var i = 0; i < keys.length; i++) {
            var described = describe(keys[i]);
            key(described.text ? "keyDown" : "rawKeyDown", described);
            key("keyUp", described);
        }

        return Promise.resolve();
    };

    /*
     * https://w3c.github.io/webdriver/#perform-actions. The argument is an array of input sources, each with
     * an `actions` array indexed by tick, and every source's array is the same length — `testdriver-actions.js`
     * pads with `pause`. So one tick is one index across every source, performed in source order.
     *
     * `duration` is ignored throughout: there is no rendering to animate a move across and no user to keep
     * waiting, so a tick is instantaneous. That is the one place this diverges from WebDriver, and it is
     * visible only to a test that measures how long a drag took.
     */
    internal.action_sequence = function (actions) {
        var ticks = 0;

        for (var s = 0; s < actions.length; s++) {
            ticks = Math.max(ticks, actions[s].actions.length);
        }

        for (var tick = 0; tick < ticks; tick++) {
            for (var source = 0; source < actions.length; source++) {
                perform(actions[source].actions[tick]);
            }
        }

        return Promise.resolve();
    };

    function perform(action) {
        if (!action || action.type === "pause") {
            return;
        }

        switch (action.type) {
            case "pointerMove": {
                var moved = resolve(action);
                mouse("moved", moved[0], moved[1], {});
                return;
            }

            case "pointerDown":
                mouse("pressed", pointerX, pointerY, {
                    button: action.button === undefined ? 0 : action.button,
                    buttons: 1,
                    clickCount: 1
                });
                return;

            case "pointerUp":
                mouse("released", pointerX, pointerY, {
                    button: action.button === undefined ? 0 : action.button,
                    buttons: 0,
                    clickCount: 1
                });
                return;

            case "scroll": {
                var scrolled = resolve(action);
                mouse("wheel", scrolled[0], scrolled[1], {
                    deltaX: action.deltaX === undefined ? 0 : action.deltaX,
                    deltaY: action.deltaY === undefined ? 0 : action.deltaY
                });
                return;
            }

            case "keyDown": {
                var down = describe(action.value);
                key(down.text ? "keyDown" : "rawKeyDown", down);
                return;
            }

            case "keyUp":
                key("keyUp", describe(action.value));
                return;

            default:
                throw new Error("unsupported action type: " + action.type);
        }
    }
})();

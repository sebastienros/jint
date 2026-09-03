// The obstacle course's driver, injected into a fixture by the in-process suite rather than loaded by the
// fixture itself. A fixture must run under a real automation client too -- PuppeteerSharp and Playwright
// drive three of them over the protocol, with trusted input and no help from this file -- so nothing here
// may be something a fixture depends on.
//
// Every member answers a string, a number or a boolean. Nothing that belongs to the engine crosses back to
// the caller, which is the page runtime's thread rule restated at the only place a test could break it.
(() => {
  'use strict';

  const one = (selector) => {
    const element = document.querySelector(selector);
    if (element === null) {
      throw new Error('no element matches ' + JSON.stringify(selector));
    }
    return element;
  };

  // React installs an own `value` property on every controlled input, which records the last value it saw
  // and makes it ignore a change whose value already equals it -- so `element.value = 'x'` types into a
  // React input and nothing happens. The prototype's setter is the real one, underneath React's.
  const setValue = (element, value) => {
    const descriptor = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(element), 'value');
    if (descriptor && typeof descriptor.set === 'function') {
      descriptor.set.call(element, value);
    } else {
      element.value = value;
    }
  };

  globalThis.__h = {
    /** Clicks the first match, through the element's own activation behaviour. */
    click(selector) {
      one(selector).click();
      return true;
    },

    /** Clicks the match at an index, for a list whose rows are all the same selector. */
    clickAt(selector, index) {
      const matches = document.querySelectorAll(selector);
      if (index >= matches.length) {
        throw new Error(selector + ' has ' + matches.length + ' matches, so there is no [' + index + ']');
      }
      matches[index].click();
      return true;
    },

    /** Puts text in a field the way a keystroke would: the real setter, then a bubbling `input`. */
    type(selector, text) {
      const element = one(selector);
      element.focus();
      setValue(element, text);
      element.dispatchEvent(new Event('input', { bubbles: true }));
      return true;
    },

    /** A key press at the element, as a `keydown`/`keyup` pair a framework's handler sees. */
    press(selector, key) {
      const element = one(selector);
      const init = { key: key, code: key, bubbles: true, cancelable: true };
      element.dispatchEvent(new KeyboardEvent('keydown', init));
      element.dispatchEvent(new KeyboardEvent('keyup', init));
      return true;
    },

    /** The first match's trimmed text, or the empty string when nothing matches. */
    text(selector) {
      const element = document.querySelector(selector);
      return element === null ? '' : element.textContent.trim();
    },

    /** Every match's trimmed text, joined with `|` so the answer is one string. */
    texts(selector) {
      return Array.prototype.map.call(document.querySelectorAll(selector), (e) => e.textContent.trim()).join('|');
    },

    /** How many elements match. */
    count(selector) {
      return document.querySelectorAll(selector).length;
    },

    /** Whether anything matches. */
    has(selector) {
      return document.querySelector(selector) !== null;
    },

    /** One attribute of the first match, or the empty string. */
    attr(selector, name) {
      const element = document.querySelector(selector);
      const value = element === null ? null : element.getAttribute(name);
      return value === null ? '' : value;
    },

    /** One property of the first match, stringified. */
    prop(selector, name) {
      const element = one(selector);
      return String(element[name]);
    },

    /** The first match's markup, for a fixture whose end state is a fragment somebody swapped in. */
    html(selector) {
      return one(selector).innerHTML.trim();
    },
  };
})();

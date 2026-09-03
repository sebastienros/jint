// The obstacle course's readers, injected into a fixture by the in-process suite rather than loaded by the
// fixture itself. A fixture must run under a real automation client too -- PuppeteerSharp and Playwright
// drive three of them over the protocol with no help from this file -- so nothing here may be something a
// fixture depends on.
//
// Input is NOT here. Clicking, filling and pressing a key are Page.ClickAsync, Page.FillAsync and
// Page.PressAsync, which go through the same input model a protocol client drives; `clickAt` is the one
// that stayed, because the public API answers about the first match and a list case wants the nth.
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

  globalThis.__h = {
    /** Clicks the match at an index, for a list whose rows are all the same selector. */
    clickAt(selector, index) {
      const matches = document.querySelectorAll(selector);
      if (index >= matches.length) {
        throw new Error(selector + ' has ' + matches.length + ' matches, so there is no [' + index + ']');
      }
      matches[index].click();
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

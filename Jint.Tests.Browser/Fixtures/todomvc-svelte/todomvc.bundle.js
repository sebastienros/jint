(() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __typeError = (msg) => {
    throw TypeError(msg);
  };
  var __defNormalProp = (obj, key2, value) => key2 in obj ? __defProp(obj, key2, { enumerable: true, configurable: true, writable: true, value }) : obj[key2] = value;
  var __esm = (fn, res, err) => function __init() {
    if (err) throw err[0];
    try {
      return fn && (res = (0, fn[__getOwnPropNames(fn)[0]])(fn = 0)), res;
    } catch (e) {
      throw err = [e], e;
    }
  };
  var __commonJS = (cb, mod) => function __require() {
    try {
      return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
    } catch (e) {
      throw mod = 0, e;
    }
  };
  var __publicField = (obj, key2, value) => __defNormalProp(obj, typeof key2 !== "symbol" ? key2 + "" : key2, value);
  var __accessCheck = (obj, member, msg) => member.has(obj) || __typeError("Cannot " + msg);
  var __privateGet = (obj, member, getter) => (__accessCheck(obj, member, "read from private field"), getter ? getter.call(obj) : member.get(obj));
  var __privateAdd = (obj, member, value) => member.has(obj) ? __typeError("Cannot add the same private member more than once") : member instanceof WeakSet ? member.add(obj) : member.set(obj, value);
  var __privateSet = (obj, member, value, setter) => (__accessCheck(obj, member, "write to private field"), setter ? setter.call(obj, value) : member.set(obj, value), value);
  var __privateMethod = (obj, member, method) => (__accessCheck(obj, member, "access private method"), method);

  // node_modules/esm-env/true.js
  var init_true = __esm({
    "node_modules/esm-env/true.js"() {
    }
  });

  // node_modules/esm-env/dev-fallback.js
  var node_env, dev_fallback_default;
  var init_dev_fallback = __esm({
    "node_modules/esm-env/dev-fallback.js"() {
      node_env = globalThis.process?.env?.NODE_ENV;
      dev_fallback_default = node_env && !node_env.toLowerCase().startsWith("prod");
    }
  });

  // node_modules/esm-env/false.js
  var init_false = __esm({
    "node_modules/esm-env/false.js"() {
    }
  });

  // node_modules/esm-env/index.js
  var init_esm_env = __esm({
    "node_modules/esm-env/index.js"() {
      init_true();
      init_dev_fallback();
      init_false();
    }
  });

  // node_modules/svelte/src/internal/shared/utils.js
  function run_all(arr) {
    for (var i = 0; i < arr.length; i++) {
      arr[i]();
    }
  }
  function deferred() {
    var resolve;
    var reject;
    var promise = new Promise((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }
  var is_array, index_of, includes, array_from, object_keys, define_property, get_descriptor, get_descriptors, object_prototype, array_prototype, get_prototype_of, is_extensible, noop;
  var init_utils = __esm({
    "node_modules/svelte/src/internal/shared/utils.js"() {
      is_array = Array.isArray;
      index_of = Array.prototype.indexOf;
      includes = Array.prototype.includes;
      array_from = Array.from;
      object_keys = Object.keys;
      define_property = Object.defineProperty;
      get_descriptor = Object.getOwnPropertyDescriptor;
      get_descriptors = Object.getOwnPropertyDescriptors;
      object_prototype = Object.prototype;
      array_prototype = Array.prototype;
      get_prototype_of = Object.getPrototypeOf;
      is_extensible = Object.isExtensible;
      noop = () => {
      };
    }
  });

  // node_modules/svelte/src/internal/client/constants.js
  var DERIVED, EFFECT, RENDER_EFFECT, MANAGED_EFFECT, BLOCK_EFFECT, BRANCH_EFFECT, ROOT_EFFECT, BOUNDARY_EFFECT, PAUSED, CONNECTED, CLEAN, DIRTY, MAYBE_DIRTY, INERT, DESTROYED, REACTION_RAN, DESTROYING, EFFECT_TRANSPARENT, EAGER_EFFECT, HEAD_EFFECT, EFFECT_PRESERVED, USER_EFFECT, EFFECT_OFFSCREEN, WAS_MARKED, REACTION_IS_UPDATING, ASYNC, ERROR_VALUE, STATE_SYMBOL, COMPONENT_SYMBOL, LEGACY_PROPS, LOADING_ATTR_SYMBOL, PROXY_PATH_SYMBOL, ATTRIBUTES_CACHE, CLASS_CACHE, STYLE_CACHE, TEXT_CACHE, FORM_RESET_HANDLER, STALE_REACTION, IS_XHTML, TEXT_NODE, COMMENT_NODE;
  var init_constants = __esm({
    "node_modules/svelte/src/internal/client/constants.js"() {
      DERIVED = 1 << 1;
      EFFECT = 1 << 2;
      RENDER_EFFECT = 1 << 3;
      MANAGED_EFFECT = 1 << 24;
      BLOCK_EFFECT = 1 << 4;
      BRANCH_EFFECT = 1 << 5;
      ROOT_EFFECT = 1 << 6;
      BOUNDARY_EFFECT = 1 << 7;
      PAUSED = 1 << 8;
      CONNECTED = 1 << 9;
      CLEAN = 1 << 10;
      DIRTY = 1 << 11;
      MAYBE_DIRTY = 1 << 12;
      INERT = 1 << 13;
      DESTROYED = 1 << 14;
      REACTION_RAN = 1 << 15;
      DESTROYING = 1 << 25;
      EFFECT_TRANSPARENT = 1 << 16;
      EAGER_EFFECT = 1 << 17;
      HEAD_EFFECT = 1 << 18;
      EFFECT_PRESERVED = 1 << 19;
      USER_EFFECT = 1 << 20;
      EFFECT_OFFSCREEN = 1 << 25;
      WAS_MARKED = 1 << 16;
      REACTION_IS_UPDATING = 1 << 21;
      ASYNC = 1 << 22;
      ERROR_VALUE = 1 << 23;
      STATE_SYMBOL = /* @__PURE__ */ Symbol("$state");
      COMPONENT_SYMBOL = /* @__PURE__ */ Symbol("component");
      LEGACY_PROPS = /* @__PURE__ */ Symbol("legacy props");
      LOADING_ATTR_SYMBOL = /* @__PURE__ */ Symbol("");
      PROXY_PATH_SYMBOL = /* @__PURE__ */ Symbol("proxy path");
      ATTRIBUTES_CACHE = /* @__PURE__ */ Symbol("attributes");
      CLASS_CACHE = /* @__PURE__ */ Symbol("class");
      STYLE_CACHE = /* @__PURE__ */ Symbol("style");
      TEXT_CACHE = /* @__PURE__ */ Symbol("text");
      FORM_RESET_HANDLER = /* @__PURE__ */ Symbol("form reset");
      STALE_REACTION = new class StaleReactionError extends Error {
        constructor() {
          super(...arguments);
          __publicField(this, "name", "StaleReactionError");
          __publicField(this, "message", "The reaction that called `getAbortSignal()` was re-run or destroyed");
        }
      }();
      IS_XHTML = // We gotta write it like this because after downleveling the pure comment may end up in the wrong location
      !!globalThis.document?.contentType && /* @__PURE__ */ globalThis.document.contentType.includes("xml");
      TEXT_NODE = 3;
      COMMENT_NODE = 8;
    }
  });

  // node_modules/svelte/src/constants.js
  var EACH_ITEM_REACTIVE, EACH_INDEX_REACTIVE, EACH_IS_CONTROLLED, EACH_IS_ANIMATED, EACH_ITEM_IMMUTABLE, PROPS_IS_RUNES, PROPS_IS_UPDATED, PROPS_IS_BINDABLE, PROPS_IS_LAZY_INITIAL, TRANSITION_OUT, TRANSITION_GLOBAL, TEMPLATE_FRAGMENT, TEMPLATE_USE_IMPORT_NODE, TEMPLATE_USE_SVG, TEMPLATE_USE_MATHML, HYDRATION_START, HYDRATION_START_ELSE, HYDRATION_START_FAILED, HYDRATION_END, HYDRATION_ERROR, ELEMENT_PRESERVE_ATTRIBUTE_CASE, ELEMENT_IS_INPUT, UNINITIALIZED, FILENAME, NAMESPACE_HTML;
  var init_constants2 = __esm({
    "node_modules/svelte/src/constants.js"() {
      EACH_ITEM_REACTIVE = 1;
      EACH_INDEX_REACTIVE = 1 << 1;
      EACH_IS_CONTROLLED = 1 << 2;
      EACH_IS_ANIMATED = 1 << 3;
      EACH_ITEM_IMMUTABLE = 1 << 4;
      PROPS_IS_RUNES = 1 << 1;
      PROPS_IS_UPDATED = 1 << 2;
      PROPS_IS_BINDABLE = 1 << 3;
      PROPS_IS_LAZY_INITIAL = 1 << 4;
      TRANSITION_OUT = 1 << 1;
      TRANSITION_GLOBAL = 1 << 2;
      TEMPLATE_FRAGMENT = 1;
      TEMPLATE_USE_IMPORT_NODE = 1 << 1;
      TEMPLATE_USE_SVG = 1 << 2;
      TEMPLATE_USE_MATHML = 1 << 3;
      HYDRATION_START = "[";
      HYDRATION_START_ELSE = "[!";
      HYDRATION_START_FAILED = "[?";
      HYDRATION_END = "]";
      HYDRATION_ERROR = {};
      ELEMENT_PRESERVE_ATTRIBUTE_CASE = 1 << 1;
      ELEMENT_IS_INPUT = 1 << 2;
      UNINITIALIZED = /* @__PURE__ */ Symbol("uninitialized");
      FILENAME = /* @__PURE__ */ Symbol("filename");
      NAMESPACE_HTML = "http://www.w3.org/1999/xhtml";
    }
  });

  // node_modules/svelte/src/internal/client/warnings.js
  function await_reactivity_loss(name) {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] await_reactivity_loss
%cDetected reactivity loss when reading \`${name}\`. This happens when state is read in an async function after an earlier \`await\`
https://svelte.dev/e/await_reactivity_loss`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/await_reactivity_loss`);
    }
  }
  function await_waterfall(name, location2) {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] await_waterfall
%cAn async derived, \`${name}\` (${location2}) was not read immediately after it resolved. This often indicates an unnecessary waterfall, which can slow down your app
https://svelte.dev/e/await_waterfall`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/await_waterfall`);
    }
  }
  function derived_inert() {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] derived_inert
%cReading a derived belonging to a now-destroyed effect may result in stale values
https://svelte.dev/e/derived_inert`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/derived_inert`);
    }
  }
  function hydration_attribute_changed(attribute, html2, value) {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] hydration_attribute_changed
%cThe \`${attribute}\` attribute on \`${html2}\` changed its value between server and client renders. The client value, \`${value}\`, will be ignored in favour of the server value
https://svelte.dev/e/hydration_attribute_changed`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/hydration_attribute_changed`);
    }
  }
  function hydration_mismatch(location2) {
    if (dev_fallback_default) {
      console.warn(
        `%c[svelte] hydration_mismatch
%c${location2 ? `Hydration failed because the initial UI does not match what was rendered on the server. The error occurred near ${location2}` : "Hydration failed because the initial UI does not match what was rendered on the server"}
https://svelte.dev/e/hydration_mismatch`,
        bold,
        normal
      );
    } else {
      console.warn(`https://svelte.dev/e/hydration_mismatch`);
    }
  }
  function lifecycle_double_unmount() {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] lifecycle_double_unmount
%cTried to unmount a component that was not mounted
https://svelte.dev/e/lifecycle_double_unmount`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/lifecycle_double_unmount`);
    }
  }
  function state_proxy_equality_mismatch(operator) {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] state_proxy_equality_mismatch
%cReactive \`$state(...)\` proxies and the values they proxy have different identities. Because of this, comparisons with \`${operator}\` will produce unexpected results
https://svelte.dev/e/state_proxy_equality_mismatch`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/state_proxy_equality_mismatch`);
    }
  }
  function svelte_boundary_reset_noop() {
    if (dev_fallback_default) {
      console.warn(`%c[svelte] svelte_boundary_reset_noop
%cA \`<svelte:boundary>\` \`reset\` function only resets the boundary the first time it is called
https://svelte.dev/e/svelte_boundary_reset_noop`, bold, normal);
    } else {
      console.warn(`https://svelte.dev/e/svelte_boundary_reset_noop`);
    }
  }
  var bold, normal;
  var init_warnings = __esm({
    "node_modules/svelte/src/internal/client/warnings.js"() {
      init_esm_env();
      bold = "font-weight: bold";
      normal = "font-weight: normal";
    }
  });

  // node_modules/svelte/src/internal/client/dom/hydration.js
  function set_hydrating(value) {
    hydrating = value;
  }
  function set_hydrate_node(node) {
    if (node === null) {
      hydration_mismatch();
      throw HYDRATION_ERROR;
    }
    return hydrate_node = node;
  }
  function hydrate_next() {
    return set_hydrate_node(get_next_sibling(hydrate_node));
  }
  function reset(node) {
    if (!hydrating) return;
    if (get_next_sibling(hydrate_node) !== null) {
      hydration_mismatch();
      throw HYDRATION_ERROR;
    }
    hydrate_node = node;
  }
  function next(count = 1) {
    if (hydrating) {
      var i = count;
      var node = hydrate_node;
      while (i--) {
        node = /** @type {TemplateNode} */
        get_next_sibling(node);
      }
      hydrate_node = node;
    }
  }
  function skip_nodes(remove = true) {
    var depth = 0;
    var node = hydrate_node;
    while (true) {
      if (node.nodeType === COMMENT_NODE) {
        var data = (
          /** @type {Comment} */
          node.data
        );
        if (data === HYDRATION_END) {
          if (depth === 0) return node;
          depth -= 1;
        } else if (data === HYDRATION_START || data === HYDRATION_START_ELSE || // "[1", "[2", etc. for if blocks
        data[0] === "[" && !isNaN(Number(data.slice(1)))) {
          depth += 1;
        }
      }
      var next2 = (
        /** @type {TemplateNode} */
        get_next_sibling(node)
      );
      if (remove) node.remove();
      node = next2;
    }
  }
  function read_hydration_instruction(node) {
    if (!node || node.nodeType !== COMMENT_NODE) {
      hydration_mismatch();
      throw HYDRATION_ERROR;
    }
    return (
      /** @type {Comment} */
      node.data
    );
  }
  var hydrating, hydrate_node;
  var init_hydration = __esm({
    "node_modules/svelte/src/internal/client/dom/hydration.js"() {
      init_constants();
      init_constants2();
      init_warnings();
      init_operations();
      hydrating = false;
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/equality.js
  function equals(value) {
    return value === this.v;
  }
  function safe_not_equal(a, b) {
    return a != a ? b == b : a !== b || a !== null && typeof a === "object" || typeof a === "function";
  }
  function safe_equals(value) {
    return !safe_not_equal(value, this.v);
  }
  var init_equality = __esm({
    "node_modules/svelte/src/internal/client/reactivity/equality.js"() {
    }
  });

  // node_modules/svelte/src/internal/shared/errors.js
  function invariant_violation(message) {
    if (dev_fallback_default) {
      const error = new Error(`invariant_violation
An invariant violation occurred, meaning Svelte's internal assumptions were flawed. This is a bug in Svelte, not your app \u2014 please open an issue at https://github.com/sveltejs/svelte, citing the following message: "${message}"
https://svelte.dev/e/invariant_violation`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/invariant_violation`);
    }
  }
  var init_errors = __esm({
    "node_modules/svelte/src/internal/shared/errors.js"() {
      init_esm_env();
    }
  });

  // node_modules/svelte/src/internal/client/errors.js
  function async_derived_orphan() {
    if (dev_fallback_default) {
      const error = new Error(`async_derived_orphan
Cannot create a \`$derived(...)\` with an \`await\` expression outside of an effect tree
https://svelte.dev/e/async_derived_orphan`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/async_derived_orphan`);
    }
  }
  function bind_invalid_checkbox_value() {
    if (dev_fallback_default) {
      const error = new Error(`bind_invalid_checkbox_value
Using \`bind:value\` together with a checkbox input is not allowed. Use \`bind:checked\` instead
https://svelte.dev/e/bind_invalid_checkbox_value`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/bind_invalid_checkbox_value`);
    }
  }
  function derived_references_self() {
    if (dev_fallback_default) {
      const error = new Error(`derived_references_self
A derived value cannot reference itself recursively
https://svelte.dev/e/derived_references_self`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/derived_references_self`);
    }
  }
  function each_key_duplicate(a, b, value) {
    if (dev_fallback_default) {
      const error = new Error(`each_key_duplicate
${value ? `Keyed each block has duplicate key \`${value}\` at indexes ${a} and ${b}` : `Keyed each block has duplicate key at indexes ${a} and ${b}`}
https://svelte.dev/e/each_key_duplicate`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/each_key_duplicate`);
    }
  }
  function each_key_volatile(index2, a, b) {
    if (dev_fallback_default) {
      const error = new Error(`each_key_volatile
Keyed each block has key that is not idempotent \u2014 the key for item at index ${index2} was \`${a}\` but is now \`${b}\`. Keys must be the same each time for a given item
https://svelte.dev/e/each_key_volatile`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/each_key_volatile`);
    }
  }
  function effect_in_teardown(rune) {
    if (dev_fallback_default) {
      const error = new Error(`effect_in_teardown
\`${rune}\` cannot be used inside an effect cleanup function
https://svelte.dev/e/effect_in_teardown`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/effect_in_teardown`);
    }
  }
  function effect_in_unowned_derived() {
    if (dev_fallback_default) {
      const error = new Error(`effect_in_unowned_derived
Effect cannot be created inside a \`$derived\` value that was not itself created inside an effect
https://svelte.dev/e/effect_in_unowned_derived`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/effect_in_unowned_derived`);
    }
  }
  function effect_orphan(rune) {
    if (dev_fallback_default) {
      const error = new Error(`effect_orphan
\`${rune}\` can only be used inside an effect (e.g. during component initialisation)
https://svelte.dev/e/effect_orphan`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/effect_orphan`);
    }
  }
  function effect_update_depth_exceeded() {
    if (dev_fallback_default) {
      const error = new Error(`effect_update_depth_exceeded
Maximum update depth exceeded. This typically indicates that an effect reads and writes the same piece of state
https://svelte.dev/e/effect_update_depth_exceeded`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/effect_update_depth_exceeded`);
    }
  }
  function hydration_failed() {
    if (dev_fallback_default) {
      const error = new Error(`hydration_failed
Failed to hydrate the application
https://svelte.dev/e/hydration_failed`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/hydration_failed`);
    }
  }
  function rune_outside_svelte(rune) {
    if (dev_fallback_default) {
      const error = new Error(`rune_outside_svelte
The \`${rune}\` rune is only available inside \`.svelte\` and \`.svelte.js/ts\` files
https://svelte.dev/e/rune_outside_svelte`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/rune_outside_svelte`);
    }
  }
  function state_descriptors_fixed() {
    if (dev_fallback_default) {
      const error = new Error(`state_descriptors_fixed
Property descriptors defined on \`$state\` objects must contain \`value\` and always be \`enumerable\`, \`configurable\` and \`writable\`.
https://svelte.dev/e/state_descriptors_fixed`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/state_descriptors_fixed`);
    }
  }
  function state_prototype_fixed() {
    if (dev_fallback_default) {
      const error = new Error(`state_prototype_fixed
Cannot set prototype of \`$state\` object
https://svelte.dev/e/state_prototype_fixed`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/state_prototype_fixed`);
    }
  }
  function state_unsafe_mutation() {
    if (dev_fallback_default) {
      const error = new Error(`state_unsafe_mutation
Updating state inside \`$derived(...)\`, \`$inspect(...)\` or a template expression is forbidden. If the value should not be reactive, declare it without \`$state\`
https://svelte.dev/e/state_unsafe_mutation`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/state_unsafe_mutation`);
    }
  }
  function svelte_boundary_reset_onerror() {
    if (dev_fallback_default) {
      const error = new Error(`svelte_boundary_reset_onerror
A \`<svelte:boundary>\` \`reset\` function cannot be called while an error is still being handled
https://svelte.dev/e/svelte_boundary_reset_onerror`);
      error.name = "Svelte error";
      throw error;
    } else {
      throw new Error(`https://svelte.dev/e/svelte_boundary_reset_onerror`);
    }
  }
  var init_errors2 = __esm({
    "node_modules/svelte/src/internal/client/errors.js"() {
      init_esm_env();
      init_errors();
    }
  });

  // node_modules/svelte/src/internal/flags/index.js
  var async_mode_flag, legacy_mode_flag, tracing_mode_flag;
  var init_flags = __esm({
    "node_modules/svelte/src/internal/flags/index.js"() {
      async_mode_flag = false;
      legacy_mode_flag = false;
      tracing_mode_flag = false;
    }
  });

  // node_modules/svelte/src/internal/shared/warnings.js
  var init_warnings2 = __esm({
    "node_modules/svelte/src/internal/shared/warnings.js"() {
      init_esm_env();
    }
  });

  // node_modules/svelte/src/internal/shared/clone.js
  var init_clone = __esm({
    "node_modules/svelte/src/internal/shared/clone.js"() {
      init_esm_env();
      init_warnings2();
      init_utils();
    }
  });

  // node_modules/svelte/src/internal/client/dev/tracing.js
  function tag(source2, label) {
    source2.label = label;
    tag_proxy(source2.v, label);
    return source2;
  }
  function tag_proxy(value, label) {
    value?.[PROXY_PATH_SYMBOL]?.(label);
    return value;
  }
  var tracing_expressions;
  var init_tracing = __esm({
    "node_modules/svelte/src/internal/client/dev/tracing.js"() {
      init_constants2();
      init_clone();
      init_constants();
      init_effects();
      init_runtime();
      tracing_expressions = null;
    }
  });

  // node_modules/svelte/src/internal/shared/dev.js
  function get_error(label) {
    const error = new Error();
    const stack2 = get_stack();
    if (stack2.length === 0) {
      return null;
    }
    stack2.unshift("\n");
    define_property(error, "stack", {
      value: stack2.join("\n")
    });
    define_property(error, "name", {
      value: label
    });
    return (
      /** @type {Error & { stack: string }} */
      error
    );
  }
  function get_stack() {
    const limit = Error.stackTraceLimit;
    Error.stackTraceLimit = Infinity;
    const stack2 = new Error().stack;
    Error.stackTraceLimit = limit;
    if (!stack2) return [];
    const lines = stack2.split("\n");
    const new_lines = [];
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const posixified = line.replaceAll("\\", "/");
      if (line.trim() === "Error") {
        continue;
      }
      if (line.includes("validate_each_keys")) {
        return [];
      }
      if (posixified.includes("svelte/src/internal") || posixified.includes("node_modules/.vite")) {
        continue;
      }
      new_lines.push(line);
    }
    return new_lines;
  }
  function invariant(condition, message) {
    if (!dev_fallback_default) {
      throw new Error("invariant(...) was not guarded by if (DEV)");
    }
    if (!condition) invariant_violation(message);
  }
  var init_dev = __esm({
    "node_modules/svelte/src/internal/shared/dev.js"() {
      init_esm_env();
      init_utils();
      init_errors();
    }
  });

  // node_modules/svelte/src/internal/shared/context.js
  var init_context = __esm({
    "node_modules/svelte/src/internal/shared/context.js"() {
      init_errors();
    }
  });

  // node_modules/svelte/src/internal/client/context.js
  function set_component_context(context) {
    component_context = context;
  }
  function set_dev_stack(stack2) {
    dev_stack = stack2;
  }
  function set_dev_current_component_function(fn) {
    dev_current_component_function = fn;
  }
  function push(props, runes = false, fn) {
    component_context = {
      p: component_context,
      i: false,
      c: null,
      e: null,
      s: props,
      x: null,
      r: (
        /** @type {Effect} */
        active_effect
      ),
      l: legacy_mode_flag && !runes ? { s: null, u: null, $: [] } : null
    };
    if (dev_fallback_default) {
      component_context.function = fn;
      dev_current_component_function = fn;
    }
  }
  function pop(component2) {
    var context = (
      /** @type {ComponentContext} */
      component_context
    );
    var effects = context.e;
    if (effects !== null) {
      context.e = null;
      for (var fn of effects) {
        create_user_effect(fn);
      }
    }
    if (component2 !== void 0) {
      context.x = component2;
    }
    context.i = true;
    component_context = context.p;
    if (dev_fallback_default) {
      dev_current_component_function = component_context?.function ?? null;
    }
    return mark_as_component(component2);
  }
  function mark_as_component(component2 = {}) {
    define_property(component2, COMPONENT_SYMBOL, { value: true });
    return component2;
  }
  function is_runes() {
    return !legacy_mode_flag || component_context !== null && component_context.l === null;
  }
  var component_context, dev_stack, dev_current_component_function;
  var init_context2 = __esm({
    "node_modules/svelte/src/internal/client/context.js"() {
      init_esm_env();
      init_errors2();
      init_runtime();
      init_effects();
      init_flags();
      init_constants2();
      init_constants();
      init_utils();
      init_context();
      component_context = null;
      dev_stack = null;
      dev_current_component_function = null;
    }
  });

  // node_modules/svelte/src/internal/client/dom/task.js
  function run_micro_tasks() {
    var tasks = micro_tasks;
    micro_tasks = [];
    run_all(tasks);
  }
  function queue_micro_task(fn) {
    if (micro_tasks.length === 0 && !is_flushing_sync) {
      var tasks = micro_tasks;
      queueMicrotask(() => {
        if (tasks === micro_tasks) run_micro_tasks();
      });
    }
    micro_tasks.push(fn);
  }
  function flush_tasks() {
    while (micro_tasks.length > 0) {
      run_micro_tasks();
    }
  }
  var micro_tasks;
  var init_task = __esm({
    "node_modules/svelte/src/internal/client/dom/task.js"() {
      init_utils();
      init_batch();
      micro_tasks = [];
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/status.js
  function set_signal_status(signal, status) {
    signal.f = signal.f & STATUS_MASK | status;
  }
  function update_derived_status(derived2) {
    if ((derived2.f & CONNECTED) !== 0 || derived2.deps === null) {
      set_signal_status(derived2, CLEAN);
    } else {
      set_signal_status(derived2, MAYBE_DIRTY);
    }
  }
  var STATUS_MASK;
  var init_status = __esm({
    "node_modules/svelte/src/internal/client/reactivity/status.js"() {
      init_constants();
      STATUS_MASK = ~(DIRTY | MAYBE_DIRTY | CLEAN);
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/utils.js
  function clear_marked(deps) {
    if (deps === null) return;
    for (const dep of deps) {
      if ((dep.f & DERIVED) === 0 || (dep.f & WAS_MARKED) === 0) {
        continue;
      }
      dep.f ^= WAS_MARKED;
      clear_marked(
        /** @type {Derived} */
        dep.deps
      );
    }
  }
  function defer_effect(effect2, dirty_effects, maybe_dirty_effects) {
    if ((effect2.f & DIRTY) !== 0) {
      dirty_effects.add(effect2);
    } else if ((effect2.f & MAYBE_DIRTY) !== 0) {
      maybe_dirty_effects.add(effect2);
    }
    clear_marked(effect2.deps);
    set_signal_status(effect2, CLEAN);
  }
  var init_utils2 = __esm({
    "node_modules/svelte/src/internal/client/reactivity/utils.js"() {
      init_constants();
      init_status();
    }
  });

  // node_modules/svelte/src/store/utils.js
  var init_utils3 = __esm({
    "node_modules/svelte/src/store/utils.js"() {
      init_runtime();
      init_utils();
    }
  });

  // node_modules/svelte/src/store/shared/index.js
  var init_shared = __esm({
    "node_modules/svelte/src/store/shared/index.js"() {
      init_utils();
      init_equality();
      init_utils3();
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/store.js
  var legacy_is_updating_store;
  var init_store = __esm({
    "node_modules/svelte/src/internal/client/reactivity/store.js"() {
      init_utils3();
      init_shared();
      init_utils();
      init_runtime();
      init_effects();
      init_sources();
      init_esm_env();
      legacy_is_updating_store = false;
    }
  });

  // node_modules/svelte/src/internal/client/dev/debug.js
  var init_debug = __esm({
    "node_modules/svelte/src/internal/client/dev/debug.js"() {
      init_constants();
      init_clone();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/misc.js
  function add_form_reset_listener() {
    if (!listening_to_form_reset) {
      listening_to_form_reset = true;
      document.addEventListener(
        "reset",
        (evt) => {
          Promise.resolve().then(() => {
            if (!evt.defaultPrevented) {
              for (
                const e of
                /**@type {HTMLFormElement} */
                evt.target.elements
              ) {
                e[FORM_RESET_HANDLER]?.();
              }
            }
          });
        },
        // In the capture phase to guarantee we get noticed of it (no possibility of stopPropagation)
        { capture: true }
      );
    }
  }
  var listening_to_form_reset;
  var init_misc = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/misc.js"() {
      init_hydration();
      init_operations();
      init_task();
      init_constants();
      listening_to_form_reset = false;
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/shared.js
  function without_reactive_context(fn) {
    var previous_reaction = active_reaction;
    var previous_effect = active_effect;
    set_active_reaction(null);
    set_active_effect(null);
    try {
      return fn();
    } finally {
      set_active_reaction(previous_reaction);
      set_active_effect(previous_effect);
    }
  }
  function listen_to_event_and_reset_event(element2, event2, handler, on_reset = handler) {
    element2.addEventListener(event2, () => without_reactive_context(handler));
    const prev = (
      /** @type {any} */
      element2[FORM_RESET_HANDLER]
    );
    if (prev) {
      element2[FORM_RESET_HANDLER] = () => {
        prev();
        on_reset(true);
      };
    } else {
      element2[FORM_RESET_HANDLER] = () => on_reset(true);
    }
    add_form_reset_listener();
  }
  var init_shared2 = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/shared.js"() {
      init_effects();
      init_runtime();
      init_constants();
      init_misc();
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/async.js
  function flatten(blockers, sync, async2, fn) {
    const d = is_runes() ? derived : derived_safe_equal;
    var pending2 = blockers.filter((b) => !b.settled);
    var deriveds = sync.map(d);
    if (dev_fallback_default) {
      deriveds.forEach((d2, i) => {
        d2.label = sync[i].toString().replace("() => ", "").replaceAll("$.eager(() => ", "$state.eager(").replace(/\$\.get\((.+?)\)/g, (_, id) => id);
      });
    }
    if (async2.length === 0 && pending2.length === 0) {
      fn(deriveds);
      return;
    }
    var parent = (
      /** @type {Effect} */
      active_effect
    );
    var restore = capture();
    var blocker_promise = pending2.length === 1 ? pending2[0].promise : pending2.length > 1 ? Promise.all(pending2.map((b) => b.promise)) : null;
    function finish(async3) {
      if ((parent.f & DESTROYED) !== 0) {
        return;
      }
      restore();
      try {
        fn([...deriveds, ...async3]);
      } catch (error) {
        invoke_error_boundary(error, parent);
      }
      unset_context();
    }
    var decrement_pending = increment_pending();
    if (async2.length === 0) {
      blocker_promise.then(() => finish([])).finally(decrement_pending);
      return;
    }
    function run3() {
      Promise.all(async2.map((expression) => async_derived(expression))).then(finish).catch((error) => invoke_error_boundary(error, parent)).finally(decrement_pending);
    }
    if (blocker_promise) {
      blocker_promise.then(() => {
        restore();
        run3();
        unset_context();
      });
    } else {
      run3();
    }
  }
  function capture() {
    var previous_effect = (
      /** @type {Effect} */
      active_effect
    );
    var previous_reaction = active_reaction;
    var previous_component_context = component_context;
    var previous_batch2 = (
      /** @type {Batch} */
      current_batch
    );
    if (dev_fallback_default) {
      var previous_dev_stack = dev_stack;
    }
    return function restore(activate_batch = true) {
      set_active_effect(previous_effect);
      set_active_reaction(previous_reaction);
      set_component_context(previous_component_context);
      if (activate_batch && (previous_effect.f & DESTROYED) === 0) {
        previous_batch2?.activate();
        previous_batch2?.apply();
      }
      if (dev_fallback_default) {
        set_reactivity_loss_tracker(null);
        set_dev_stack(previous_dev_stack);
      }
    };
  }
  function unset_context(deactivate_batch = true) {
    restored = false;
    set_active_effect(null);
    set_active_reaction(null);
    set_component_context(null);
    if (deactivate_batch) current_batch?.deactivate();
    if (dev_fallback_default) {
      set_reactivity_loss_tracker(null);
      set_dev_stack(null);
    }
  }
  function increment_pending() {
    var effect2 = (
      /** @type {Effect} */
      active_effect
    );
    var boundary2 = effect2.b;
    var batch = (
      /** @type {Batch} */
      current_batch
    );
    var blocking = !!boundary2?.is_rendered();
    boundary2?.update_pending_count(1, batch);
    batch.increment(blocking, effect2);
    return () => {
      boundary2?.update_pending_count(-1, batch);
      batch.decrement(blocking, effect2);
    };
  }
  var restored;
  var init_async = __esm({
    "node_modules/svelte/src/internal/client/reactivity/async.js"() {
      init_constants();
      init_esm_env();
      init_context2();
      init_error_handling();
      init_runtime();
      init_batch();
      init_deriveds();
      init_effects();
      restored = false;
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/deriveds.js
  function set_reactivity_loss_tracker(v) {
    reactivity_loss_tracker = v;
  }
  // @__NO_SIDE_EFFECTS__
  function derived(fn) {
    var flags2 = DERIVED | DIRTY;
    if (active_effect !== null) {
      active_effect.f |= EFFECT_PRESERVED;
    }
    const signal = {
      ctx: component_context,
      deps: null,
      effects: null,
      equals,
      f: flags2,
      fn,
      reactions: null,
      rv: 0,
      v: (
        /** @type {V} */
        UNINITIALIZED
      ),
      wv: 0,
      parent: active_effect,
      ac: null
    };
    if (dev_fallback_default && tracing_mode_flag) {
      signal.created = get_error("created at");
    }
    return signal;
  }
  // @__NO_SIDE_EFFECTS__
  function async_derived(fn, label, location2) {
    let parent = (
      /** @type {Effect | null} */
      active_effect
    );
    if (parent === null) {
      async_derived_orphan();
    }
    var promise = (
      /** @type {Promise<V>} */
      /** @type {unknown} */
      void 0
    );
    var signal = source(
      /** @type {V} */
      UNINITIALIZED
    );
    if (dev_fallback_default) signal.label = label ?? fn.toString();
    var should_suspend = !active_reaction;
    var deferreds = /* @__PURE__ */ new Set();
    async_effect(() => {
      var effect2 = (
        /** @type {Effect} */
        active_effect
      );
      if (dev_fallback_default) {
        reactivity_loss_tracker = { effect: effect2, effect_deps: /* @__PURE__ */ new Set(), warned: false };
      }
      var d = deferred();
      promise = d.promise;
      try {
        Promise.resolve(fn()).then(d.resolve, (e) => {
          if (e !== STALE_REACTION) d.reject(e);
        }).finally(unset_context);
      } catch (error) {
        d.reject(error);
        unset_context();
      }
      if (dev_fallback_default) {
        if (reactivity_loss_tracker) {
          if (effect2.deps !== null) {
            for (let i = 0; i < skipped_deps; i += 1) {
              reactivity_loss_tracker.effect_deps.add(effect2.deps[i]);
            }
          }
          if (new_deps !== null) {
            for (let i = 0; i < new_deps.length; i += 1) {
              reactivity_loss_tracker.effect_deps.add(new_deps[i]);
            }
          }
        }
        reactivity_loss_tracker = null;
      }
      var batch = (
        /** @type {Batch} */
        current_batch
      );
      if (should_suspend) {
        if ((effect2.f & REACTION_RAN) !== 0) {
          var decrement_pending = increment_pending();
        }
        if (
          // boundary can be null if the async derived is inside an $effect.root not connected to the component render tree
          parent.b?.is_rendered()
        ) {
          batch.async_deriveds.get(effect2)?.reject(OBSOLETE);
        } else {
          for (const d2 of deferreds.values()) {
            d2.reject(OBSOLETE);
          }
        }
        deferreds.add(d);
        batch.async_deriveds.set(effect2, d);
      }
      const handler = (value, error = void 0) => {
        if (dev_fallback_default) {
          reactivity_loss_tracker = null;
        }
        decrement_pending?.();
        deferreds.delete(d);
        if (error === OBSOLETE) return;
        batch.activate();
        if (error) {
          signal.f |= ERROR_VALUE;
          internal_set(signal, error);
        } else {
          if ((signal.f & ERROR_VALUE) !== 0) {
            signal.f ^= ERROR_VALUE;
          }
          if (dev_fallback_default && location2 !== void 0 && !signal.equals(value)) {
            recent_async_deriveds.add(signal);
            setTimeout(() => {
              if (recent_async_deriveds.has(signal) && (effect2.f & DESTROYED) === 0) {
                await_waterfall(
                  /** @type {string} */
                  signal.label,
                  location2
                );
                recent_async_deriveds.delete(signal);
              }
            });
          }
          internal_set(signal, value);
        }
        batch.deactivate();
      };
      d.promise.then(handler, (e) => handler(null, e || "unknown"));
    });
    teardown(() => {
      for (const d of deferreds) {
        d.reject(OBSOLETE);
      }
    });
    if (dev_fallback_default) {
      signal.f |= ASYNC;
    }
    return new Promise((fulfil) => {
      function next2(p) {
        function go() {
          if (p === promise) {
            fulfil(signal);
          } else {
            next2(promise);
          }
        }
        p.then(go, go);
      }
      next2(promise);
    });
  }
  // @__NO_SIDE_EFFECTS__
  function user_derived(fn) {
    const d = /* @__PURE__ */ derived(fn);
    if (!async_mode_flag) push_reaction_value(d);
    return d;
  }
  // @__NO_SIDE_EFFECTS__
  function derived_safe_equal(fn) {
    const signal = /* @__PURE__ */ derived(fn);
    signal.equals = safe_equals;
    return signal;
  }
  function destroy_derived_effects(derived2) {
    var effects = derived2.effects;
    if (effects !== null) {
      derived2.effects = null;
      for (var i = 0; i < effects.length; i += 1) {
        destroy_effect(
          /** @type {Effect} */
          effects[i]
        );
      }
    }
  }
  function execute_derived(derived2) {
    var value;
    var prev_active_effect = active_effect;
    var parent = derived2.parent;
    if (!is_destroying_effect && parent !== null && derived2.v !== UNINITIALIZED && // if it was never evaluated before, it's guaranteed to fail downstream, so we try to execute instead
    (parent.f & (DESTROYED | INERT)) !== 0) {
      derived_inert();
      return derived2.v;
    }
    set_active_effect(parent);
    if (dev_fallback_default) {
      let prev_eager_effects = eager_effects;
      set_eager_effects(/* @__PURE__ */ new Set());
      try {
        if (includes.call(stack, derived2)) {
          derived_references_self();
        }
        stack.push(derived2);
        derived2.f &= ~WAS_MARKED;
        destroy_derived_effects(derived2);
        value = update_reaction(derived2);
      } finally {
        set_active_effect(prev_active_effect);
        set_eager_effects(prev_eager_effects);
        stack.pop();
      }
    } else {
      try {
        derived2.f &= ~WAS_MARKED;
        destroy_derived_effects(derived2);
        value = update_reaction(derived2);
      } finally {
        set_active_effect(prev_active_effect);
      }
    }
    return value;
  }
  function update_derived(derived2) {
    var value = execute_derived(derived2);
    if (!derived2.equals(value)) {
      derived2.wv = increment_write_version();
      if (!current_batch?.is_fork || derived2.deps === null) {
        if (current_batch !== null) {
          current_batch.capture(derived2, value, true);
          previous_batch?.capture(derived2, value, true);
        } else {
          derived2.v = value;
        }
        if (derived2.deps === null) {
          set_signal_status(derived2, CLEAN);
          return;
        }
      }
    }
    if (is_destroying_effect) {
      return;
    }
    if (batch_values !== null) {
      if (effect_tracking() || current_batch?.is_fork) {
        batch_values.set(derived2, value);
      }
    } else {
      update_derived_status(derived2);
    }
  }
  function freeze_derived_effects(derived2) {
    if (derived2.effects === null) return;
    for (const e of derived2.effects) {
      if (e.teardown || e.ac) {
        e.teardown?.();
        if (e.ac !== null) {
          without_reactive_context(() => {
            e.ac.abort(STALE_REACTION);
            e.ac = null;
          });
        }
        if (e.fn !== null) e.teardown = noop;
        remove_reactions(e, 0);
        destroy_effect_children(e);
      }
    }
  }
  function unfreeze_derived_effects(derived2) {
    if (derived2.effects === null) return;
    for (const e of derived2.effects) {
      if (e.teardown && e.fn !== null) {
        update_effect(e);
      }
    }
  }
  var reactivity_loss_tracker, recent_async_deriveds, OBSOLETE, stack;
  var init_deriveds = __esm({
    "node_modules/svelte/src/internal/client/reactivity/deriveds.js"() {
      init_esm_env();
      init_constants();
      init_runtime();
      init_shared2();
      init_equality();
      init_errors2();
      init_warnings();
      init_effects();
      init_sources();
      init_dev();
      init_flags();
      init_context2();
      init_constants2();
      init_batch();
      init_async();
      init_utils();
      init_status();
      reactivity_loss_tracker = null;
      recent_async_deriveds = /* @__PURE__ */ new Set();
      OBSOLETE = /* @__PURE__ */ Symbol("obsolete");
      stack = [];
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/batch.js
  function flushSync(fn) {
    var was_flushing_sync = is_flushing_sync;
    is_flushing_sync = true;
    try {
      var result;
      if (fn) {
        if (current_batch !== null && !current_batch.is_fork) {
          current_batch.flush();
        }
        result = fn();
      }
      while (true) {
        flush_tasks();
        if (current_batch === null) {
          return (
            /** @type {T} */
            result
          );
        }
        current_batch.flush();
      }
    } finally {
      is_flushing_sync = was_flushing_sync;
    }
  }
  function infinite_loop_guard() {
    if (dev_fallback_default) {
      var updates = /* @__PURE__ */ new Map();
      for (
        const source2 of
        /** @type {Batch} */
        current_batch.current.keys()
      ) {
        for (const [stack2, update2] of source2.updated ?? []) {
          var entry = updates.get(stack2);
          if (!entry) {
            entry = { error: update2.error, count: 0 };
            updates.set(stack2, entry);
          }
          entry.count += update2.count;
        }
      }
      for (const update2 of updates.values()) {
        if (update2.error) {
          console.error(update2.error);
        }
      }
    }
    try {
      effect_update_depth_exceeded();
    } catch (error) {
      if (dev_fallback_default) {
        define_property(error, "stack", { value: "" });
      }
      invoke_error_boundary(error, last_scheduled_effect);
    }
  }
  function flush_queued_effects(effects) {
    var length = effects.length;
    if (length === 0) return;
    var i = 0;
    while (i < length) {
      var effect2 = effects[i++];
      if ((effect2.f & (DESTROYED | INERT)) === 0 && is_dirty(effect2)) {
        eager_block_effects = /* @__PURE__ */ new Set();
        update_effect(effect2);
        if (effect2.deps === null && effect2.first === null && effect2.nodes === null && effect2.teardown === null && effect2.ac === null) {
          unlink_effect(effect2);
        }
        if (eager_block_effects?.size > 0) {
          old_values.clear();
          for (const e of eager_block_effects) {
            if ((e.f & (DESTROYED | INERT)) !== 0) continue;
            const ordered_effects = [e];
            let ancestor = e.parent;
            while (ancestor !== null) {
              if (eager_block_effects.has(ancestor)) {
                eager_block_effects.delete(ancestor);
                ordered_effects.push(ancestor);
              }
              ancestor = ancestor.parent;
            }
            for (let j = ordered_effects.length - 1; j >= 0; j--) {
              const e2 = ordered_effects[j];
              if ((e2.f & (DESTROYED | INERT)) !== 0) continue;
              update_effect(e2);
            }
          }
          eager_block_effects.clear();
        }
      }
    }
    eager_block_effects = null;
  }
  function mark_effects(value, sources, marked, checked) {
    if (marked.has(value)) return;
    marked.add(value);
    if (value.reactions !== null) {
      for (const reaction of value.reactions) {
        const flags2 = reaction.f;
        if ((flags2 & DERIVED) !== 0) {
          mark_effects(
            /** @type {Derived} */
            reaction,
            sources,
            marked,
            checked
          );
        } else if ((flags2 & (ASYNC | BLOCK_EFFECT)) !== 0 && (flags2 & DIRTY) === 0 && depends_on(reaction, sources, checked)) {
          set_signal_status(reaction, DIRTY);
          schedule_effect(
            /** @type {Effect} */
            reaction
          );
        }
      }
    }
  }
  function depends_on(reaction, sources, checked) {
    const depends = checked.get(reaction);
    if (depends !== void 0) return depends;
    if (reaction.deps !== null) {
      for (const dep of reaction.deps) {
        if (includes.call(sources, dep)) {
          return true;
        }
        if ((dep.f & DERIVED) !== 0 && depends_on(
          /** @type {Derived} */
          dep,
          sources,
          checked
        )) {
          checked.set(
            /** @type {Derived} */
            dep,
            true
          );
          return true;
        }
      }
    }
    checked.set(reaction, false);
    return false;
  }
  function schedule_effect(effect2) {
    current_batch.schedule(effect2);
  }
  function reset_branch(effect2, tracked) {
    if ((effect2.f & BRANCH_EFFECT) !== 0 && (effect2.f & CLEAN) !== 0) {
      return;
    }
    if ((effect2.f & DIRTY) !== 0) {
      tracked.d.push(effect2);
    } else if ((effect2.f & MAYBE_DIRTY) !== 0) {
      tracked.m.push(effect2);
    }
    set_signal_status(effect2, CLEAN);
    var e = effect2.first;
    while (e !== null) {
      reset_branch(e, tracked);
      e = e.next;
    }
  }
  function reset_all(effect2) {
    set_signal_status(effect2, CLEAN);
    var e = effect2.first;
    while (e !== null) {
      reset_all(e);
      e = e.next;
    }
  }
  var first_batch, last_batch, current_batch, previous_batch, batch_values, last_scheduled_effect, is_flushing_sync, is_processing, collected_effects, legacy_updates, flush_count, source_stacks, uid, _started, _prev, _next, _commit_callbacks, _discard_callbacks, _pending, _blocking_pending, _deferred, _roots, _new_effects, _dirty_effects, _maybe_dirty_effects, _skipped_branches, _unskipped_branches, _decrement_queued, _Batch_instances, is_deferred_fn, process_fn, traverse_fn, find_earlier_batch_fn, merge_fn, defer_effects_fn, commit_fn, unlink_fn, _Batch, Batch, eager_block_effects;
  var init_batch = __esm({
    "node_modules/svelte/src/internal/client/reactivity/batch.js"() {
      init_constants();
      init_flags();
      init_utils();
      init_runtime();
      init_errors2();
      init_task();
      init_esm_env();
      init_error_handling();
      init_sources();
      init_effects();
      init_utils2();
      init_constants2();
      init_status();
      init_store();
      init_dev();
      init_debug();
      init_deriveds();
      first_batch = null;
      last_batch = null;
      current_batch = null;
      previous_batch = null;
      batch_values = null;
      last_scheduled_effect = null;
      is_flushing_sync = false;
      is_processing = false;
      collected_effects = null;
      legacy_updates = null;
      flush_count = 0;
      source_stacks = /* @__PURE__ */ new Set();
      uid = 1;
      _Batch = class _Batch {
        constructor() {
          __privateAdd(this, _Batch_instances);
          __publicField(this, "id", uid++);
          /** True as soon as `#process` was called */
          __privateAdd(this, _started, false);
          __publicField(this, "linked", true);
          /** @type {Batch | null} */
          __privateAdd(this, _prev, null);
          /** @type {Batch | null} */
          __privateAdd(this, _next, null);
          /** @type {Map<Effect, ReturnType<typeof deferred<any>>>} */
          __publicField(this, "async_deriveds", /* @__PURE__ */ new Map());
          /**
           * The current values of any signals that are updated in this batch.
           * Tuple format: [value, is_derived] (note: is_derived is false for deriveds, too, if they were overridden via assignment)
           * They keys of this map are identical to `this.#previous`
           * @type {Map<Value, [any, boolean]>}
           */
          __publicField(this, "current", /* @__PURE__ */ new Map());
          /**
           * The values of any signals (sources and deriveds) that are updated in this batch _before_ those updates took place.
           * They keys of this map are identical to `this.#current`
           * @type {Map<Value, any>}
           */
          __publicField(this, "previous", /* @__PURE__ */ new Map());
          /**
           * When the batch is committed (and the DOM is updated), we need to remove old branches
           * and append new ones by calling the functions added inside (if/each/key/etc) blocks
           * @type {Set<(batch: Batch) => void>}
           */
          __privateAdd(this, _commit_callbacks, /* @__PURE__ */ new Set());
          /**
           * If a fork is discarded, we need to destroy any effects that are no longer needed
           * @type {Set<(batch: Batch) => void>}
           */
          __privateAdd(this, _discard_callbacks, /* @__PURE__ */ new Set());
          /**
           * The number of async effects that are currently in flight
           */
          __privateAdd(this, _pending, 0);
          /**
           * Async effects that are currently in flight, _not_ inside a pending boundary
           * @type {Map<Effect, number>}
           */
          __privateAdd(this, _blocking_pending, /* @__PURE__ */ new Map());
          /**
           * A deferred that resolves when the batch is committed, used with `settled()`
           * TODO replace with Promise.withResolvers once supported widely enough
           * @type {{ promise: Promise<void>, resolve: (value?: any) => void, reject: (reason: unknown) => void } | null}
           */
          __privateAdd(this, _deferred, null);
          /**
           * The root effects that need to be flushed
           * @type {Effect[]}
           */
          __privateAdd(this, _roots, []);
          /**
           * Effects created while this batch was active.
           * @type {Effect[]}
           */
          __privateAdd(this, _new_effects, []);
          /**
           * Deferred effects (which run after async work has completed) that are DIRTY
           * @type {Set<Effect>}
           */
          __privateAdd(this, _dirty_effects, /* @__PURE__ */ new Set());
          /**
           * Deferred effects that are MAYBE_DIRTY
           * @type {Set<Effect>}
           */
          __privateAdd(this, _maybe_dirty_effects, /* @__PURE__ */ new Set());
          /**
           * A map of branches that still exist, but will be destroyed when this batch
           * is committed — we skip over these during `process`.
           * The value contains child effects that were dirty/maybe_dirty before being reset,
           * so they can be rescheduled if the branch survives.
           * @type {Map<Effect, { d: Effect[], m: Effect[] }>}
           */
          __privateAdd(this, _skipped_branches, /* @__PURE__ */ new Map());
          /**
           * Inverse of #skipped_branches which we need to tell prior batches to unskip them when committing
           * @type {Set<Effect>}
           */
          __privateAdd(this, _unskipped_branches, /* @__PURE__ */ new Set());
          __publicField(this, "is_fork", false);
          __privateAdd(this, _decrement_queued, false);
          if (last_batch === null) {
            first_batch = last_batch = this;
          } else {
            __privateSet(last_batch, _next, this);
            __privateSet(this, _prev, last_batch);
          }
          last_batch = this;
        }
        /**
         * Add an effect to the #skipped_branches map and reset its children
         * @param {Effect} effect
         */
        skip_effect(effect2) {
          if (!__privateGet(this, _skipped_branches).has(effect2)) {
            __privateGet(this, _skipped_branches).set(effect2, { d: [], m: [] });
          }
          __privateGet(this, _unskipped_branches).delete(effect2);
        }
        /**
         * Remove an effect from the #skipped_branches map and reschedule
         * any tracked dirty/maybe_dirty child effects
         * @param {Effect} effect
         * @param {(e: Effect) => void} callback
         */
        unskip_effect(effect2, callback = (e) => this.schedule(e)) {
          var tracked = __privateGet(this, _skipped_branches).get(effect2);
          if (tracked) {
            __privateGet(this, _skipped_branches).delete(effect2);
            for (var e of tracked.d) {
              set_signal_status(e, DIRTY);
              callback(e);
            }
            for (e of tracked.m) {
              set_signal_status(e, MAYBE_DIRTY);
              callback(e);
            }
          }
          __privateGet(this, _unskipped_branches).add(effect2);
        }
        /**
         * Associate a change to a given source with the current
         * batch, noting its previous and current values
         * @param {Value} source
         * @param {any} value
         * @param {boolean} [is_derived]
         */
        capture(source2, value, is_derived = false) {
          if (source2.v !== UNINITIALIZED && !this.previous.has(source2)) {
            this.previous.set(source2, source2.v);
          }
          if ((source2.f & ERROR_VALUE) === 0) {
            this.current.set(source2, [value, is_derived]);
            batch_values?.set(source2, value);
          }
          if (!this.is_fork) {
            source2.v = value;
          }
        }
        activate() {
          current_batch = this;
        }
        deactivate() {
          current_batch = null;
          batch_values = null;
        }
        flush() {
          try {
            if (dev_fallback_default) {
              source_stacks.clear();
            }
            is_processing = true;
            current_batch = this;
            __privateMethod(this, _Batch_instances, process_fn).call(this);
          } finally {
            flush_count = 0;
            last_scheduled_effect = null;
            collected_effects = null;
            legacy_updates = null;
            is_processing = false;
            current_batch = null;
            batch_values = null;
            old_values.clear();
            if (dev_fallback_default) {
              for (const source2 of source_stacks) {
                source2.updated = null;
              }
            }
          }
        }
        discard() {
          for (const fn of __privateGet(this, _discard_callbacks)) fn(this);
          __privateGet(this, _discard_callbacks).clear();
          for (const deferred2 of this.async_deriveds.values()) {
            deferred2.reject(OBSOLETE);
          }
          __privateMethod(this, _Batch_instances, unlink_fn).call(this);
          __privateGet(this, _deferred)?.resolve();
        }
        /**
         * @param {Effect} effect
         */
        register_created_effect(effect2) {
          __privateGet(this, _new_effects).push(effect2);
        }
        /**
         * @param {boolean} blocking
         * @param {Effect} effect
         */
        increment(blocking, effect2) {
          __privateSet(this, _pending, __privateGet(this, _pending) + 1);
          if (blocking) {
            let blocking_pending_count = __privateGet(this, _blocking_pending).get(effect2) ?? 0;
            __privateGet(this, _blocking_pending).set(effect2, blocking_pending_count + 1);
          }
        }
        /**
         * @param {boolean} blocking
         * @param {Effect} effect
         */
        decrement(blocking, effect2) {
          __privateSet(this, _pending, __privateGet(this, _pending) - 1);
          if (blocking) {
            let blocking_pending_count = __privateGet(this, _blocking_pending).get(effect2) ?? 0;
            if (blocking_pending_count === 1) {
              __privateGet(this, _blocking_pending).delete(effect2);
            } else {
              __privateGet(this, _blocking_pending).set(effect2, blocking_pending_count - 1);
            }
          }
          if (__privateGet(this, _decrement_queued)) return;
          __privateSet(this, _decrement_queued, true);
          queue_micro_task(() => {
            __privateSet(this, _decrement_queued, false);
            if (this.linked) {
              this.flush();
            }
          });
        }
        /**
         * @param {Set<Effect>} dirty_effects
         * @param {Set<Effect>} maybe_dirty_effects
         */
        transfer_effects(dirty_effects, maybe_dirty_effects) {
          for (const e of dirty_effects) {
            __privateGet(this, _dirty_effects).add(e);
          }
          for (const e of maybe_dirty_effects) {
            __privateGet(this, _maybe_dirty_effects).add(e);
          }
          dirty_effects.clear();
          maybe_dirty_effects.clear();
        }
        /** @param {(batch: Batch) => void} fn */
        oncommit(fn) {
          __privateGet(this, _commit_callbacks).add(fn);
        }
        /** @param {(batch: Batch) => void} fn */
        ondiscard(fn) {
          __privateGet(this, _discard_callbacks).add(fn);
        }
        settled() {
          return (__privateGet(this, _deferred) ?? __privateSet(this, _deferred, deferred())).promise;
        }
        static ensure() {
          if (current_batch === null) {
            const batch = current_batch = new _Batch();
            if (!is_processing && !is_flushing_sync) {
              queue_micro_task(() => {
                if (!__privateGet(batch, _started)) {
                  batch.flush();
                }
              });
            }
          }
          return current_batch;
        }
        apply() {
          if (!async_mode_flag || !this.is_fork && __privateGet(this, _prev) === null && __privateGet(this, _next) === null) {
            batch_values = null;
            return;
          }
          batch_values = /* @__PURE__ */ new Map();
          for (const [source2, [value]] of this.current) {
            batch_values.set(source2, value);
          }
          for (let batch = first_batch; batch !== null; batch = __privateGet(batch, _next)) {
            if (batch === this || batch.is_fork) continue;
            var intersects = false;
            if (batch.id < this.id) {
              for (const [source2, [, is_derived]] of batch.current) {
                if (is_derived) continue;
                if (this.current.has(source2)) {
                  intersects = true;
                  break;
                }
              }
            }
            if (!intersects) {
              for (const [source2, previous] of batch.previous) {
                if (!batch_values.has(source2)) {
                  batch_values.set(source2, previous);
                }
              }
            }
          }
        }
        /**
         *
         * @param {Effect} effect
         */
        schedule(effect2) {
          last_scheduled_effect = effect2;
          if (effect2.b?.is_pending && (effect2.f & (EFFECT | RENDER_EFFECT | MANAGED_EFFECT)) !== 0 && (effect2.f & REACTION_RAN) === 0) {
            effect2.b.defer_effect(effect2);
            return;
          }
          var e = effect2;
          while (e.parent !== null) {
            e = e.parent;
            var flags2 = e.f;
            if (collected_effects !== null && e === active_effect) {
              if (async_mode_flag) return;
              if ((active_reaction === null || (active_reaction.f & DERIVED) === 0) && !legacy_is_updating_store) {
                return;
              }
            }
            if ((flags2 & (ROOT_EFFECT | BRANCH_EFFECT)) !== 0) {
              if ((flags2 & CLEAN) === 0) {
                return;
              }
              e.f ^= CLEAN;
            }
          }
          __privateGet(this, _roots).push(e);
        }
      };
      _started = new WeakMap();
      _prev = new WeakMap();
      _next = new WeakMap();
      _commit_callbacks = new WeakMap();
      _discard_callbacks = new WeakMap();
      _pending = new WeakMap();
      _blocking_pending = new WeakMap();
      _deferred = new WeakMap();
      _roots = new WeakMap();
      _new_effects = new WeakMap();
      _dirty_effects = new WeakMap();
      _maybe_dirty_effects = new WeakMap();
      _skipped_branches = new WeakMap();
      _unskipped_branches = new WeakMap();
      _decrement_queued = new WeakMap();
      _Batch_instances = new WeakSet();
      is_deferred_fn = function() {
        if (this.is_fork) return true;
        for (const effect2 of __privateGet(this, _blocking_pending).keys()) {
          var e = effect2;
          var skipped = false;
          while (e.parent !== null) {
            if (__privateGet(this, _skipped_branches).has(e)) {
              skipped = true;
              break;
            }
            e = e.parent;
          }
          if (!skipped) {
            return true;
          }
        }
        return false;
      };
      process_fn = function() {
        var _a2, _b, _c;
        __privateSet(this, _started, true);
        if (flush_count++ > 1e3) {
          __privateMethod(this, _Batch_instances, unlink_fn).call(this);
          infinite_loop_guard();
        }
        if (dev_fallback_default) {
          for (const value of this.current.keys()) {
            source_stacks.add(value);
          }
        }
        for (const e of __privateGet(this, _dirty_effects)) {
          __privateGet(this, _maybe_dirty_effects).delete(e);
          set_signal_status(e, DIRTY);
          this.schedule(e);
        }
        for (const e of __privateGet(this, _maybe_dirty_effects)) {
          set_signal_status(e, MAYBE_DIRTY);
          this.schedule(e);
        }
        const roots = __privateGet(this, _roots);
        __privateSet(this, _roots, []);
        this.apply();
        var effects = collected_effects = [];
        var render_effects = [];
        var updates = legacy_updates = [];
        for (const root2 of roots) {
          try {
            __privateMethod(this, _Batch_instances, traverse_fn).call(this, root2, effects, render_effects);
          } catch (e) {
            reset_all(root2);
            if (!__privateMethod(this, _Batch_instances, is_deferred_fn).call(this)) this.discard();
            throw e;
          }
        }
        current_batch = null;
        if (updates.length > 0) {
          var batch = _Batch.ensure();
          for (const e of updates) {
            batch.schedule(e);
          }
        }
        collected_effects = null;
        legacy_updates = null;
        if (__privateMethod(this, _Batch_instances, is_deferred_fn).call(this)) {
          __privateMethod(this, _Batch_instances, defer_effects_fn).call(this, render_effects);
          __privateMethod(this, _Batch_instances, defer_effects_fn).call(this, effects);
          for (const [e, t] of __privateGet(this, _skipped_branches)) {
            reset_branch(e, t);
          }
          if (updates.length > 0) {
            /** @type {unknown} */
            __privateMethod(_a2 = current_batch, _Batch_instances, process_fn).call(_a2);
          }
          return;
        }
        const earlier_batch = __privateMethod(this, _Batch_instances, find_earlier_batch_fn).call(this);
        if (earlier_batch) {
          __privateMethod(this, _Batch_instances, defer_effects_fn).call(this, render_effects);
          __privateMethod(this, _Batch_instances, defer_effects_fn).call(this, effects);
          __privateMethod(_b = earlier_batch, _Batch_instances, merge_fn).call(_b, this);
          return;
        }
        __privateGet(this, _dirty_effects).clear();
        __privateGet(this, _maybe_dirty_effects).clear();
        for (const fn of __privateGet(this, _commit_callbacks)) fn(this);
        __privateGet(this, _commit_callbacks).clear();
        previous_batch = this;
        flush_queued_effects(render_effects);
        flush_queued_effects(effects);
        previous_batch = null;
        __privateGet(this, _deferred)?.resolve();
        var next_batch = (
          /** @type {Batch | null} */
          /** @type {unknown} */
          current_batch
        );
        if (__privateGet(this, _pending) === 0 && (__privateGet(this, _roots).length === 0 || next_batch !== null)) {
          __privateMethod(this, _Batch_instances, unlink_fn).call(this);
          if (async_mode_flag) {
            __privateMethod(this, _Batch_instances, commit_fn).call(this);
            current_batch = next_batch;
          }
        }
        if (__privateGet(this, _roots).length > 0) {
          if (next_batch !== null) {
            const batch2 = next_batch;
            __privateGet(batch2, _roots).push(...__privateGet(this, _roots).filter((r) => !__privateGet(batch2, _roots).includes(r)));
          } else {
            next_batch = this;
          }
        }
        if (next_batch !== null) {
          old_values.clear();
          __privateMethod(_c = next_batch, _Batch_instances, process_fn).call(_c);
        }
      };
      /**
       * Traverse the effect tree, executing effects or stashing
       * them for later execution as appropriate
       * @param {Effect} root
       * @param {Effect[]} effects
       * @param {Effect[]} render_effects
       */
      traverse_fn = function(root2, effects, render_effects) {
        root2.f ^= CLEAN;
        var effect2 = root2.first;
        while (effect2 !== null) {
          var flags2 = effect2.f;
          var is_branch = (flags2 & (BRANCH_EFFECT | ROOT_EFFECT)) !== 0;
          var is_skippable_branch = is_branch && (flags2 & CLEAN) !== 0;
          var skip = is_skippable_branch || (flags2 & INERT) !== 0 || __privateGet(this, _skipped_branches).has(effect2);
          if (!skip && effect2.fn !== null) {
            if (is_branch) {
              effect2.f ^= CLEAN;
            } else if ((flags2 & EFFECT) !== 0) {
              effects.push(effect2);
            } else if (async_mode_flag && (flags2 & (RENDER_EFFECT | MANAGED_EFFECT)) !== 0) {
              render_effects.push(effect2);
            } else if (is_dirty(effect2)) {
              if ((flags2 & BLOCK_EFFECT) !== 0) __privateGet(this, _maybe_dirty_effects).add(effect2);
              update_effect(effect2);
            }
            var child2 = effect2.first;
            if (child2 !== null) {
              effect2 = child2;
              continue;
            }
          }
          while (effect2 !== null) {
            var next2 = effect2.next;
            if (next2 !== null) {
              effect2 = next2;
              break;
            }
            effect2 = effect2.parent;
          }
        }
      };
      find_earlier_batch_fn = function() {
        var batch = __privateGet(this, _prev);
        while (batch !== null) {
          if (!batch.is_fork) {
            for (const [value, [, is_derived]] of this.current) {
              if (batch.current.has(value) && !is_derived) {
                return batch;
              }
            }
          }
          batch = __privateGet(batch, _prev);
        }
        return null;
      };
      /**
       * @param {Batch} batch
       */
      merge_fn = function(batch) {
        var _a2;
        for (const [source2, value] of batch.current) {
          if (!this.previous.has(source2) && batch.previous.has(source2)) {
            this.previous.set(source2, batch.previous.get(source2));
          }
          this.current.set(source2, value);
        }
        for (const [effect2, deferred2] of batch.async_deriveds) {
          const d = this.async_deriveds.get(effect2);
          if (d) deferred2.promise.then(d.resolve).catch(d.reject);
        }
        batch.async_deriveds.clear();
        this.transfer_effects(__privateGet(batch, _dirty_effects), __privateGet(batch, _maybe_dirty_effects));
        const mark = (value) => {
          var reactions = value.reactions;
          if (reactions === null) return;
          if ((value.f & DERIVED) !== 0 && (value.f & (DIRTY | MAYBE_DIRTY)) === 0) {
            return;
          }
          for (const reaction of reactions) {
            var flags2 = reaction.f;
            if ((flags2 & DERIVED) !== 0) {
              mark(
                /** @type {Derived} */
                reaction
              );
            } else {
              var effect2 = (
                /** @type {Effect} */
                reaction
              );
              if (flags2 & (ASYNC | BLOCK_EFFECT) && !this.async_deriveds.has(effect2)) {
                __privateGet(this, _maybe_dirty_effects).delete(effect2);
                set_signal_status(effect2, DIRTY);
                this.schedule(effect2);
              }
            }
          }
        };
        for (const source2 of this.current.keys()) {
          mark(source2);
        }
        this.oncommit(() => batch.discard());
        __privateMethod(_a2 = batch, _Batch_instances, unlink_fn).call(_a2);
        current_batch = this;
        __privateMethod(this, _Batch_instances, process_fn).call(this);
      };
      /**
       * @param {Effect[]} effects
       */
      defer_effects_fn = function(effects) {
        for (var i = 0; i < effects.length; i += 1) {
          defer_effect(effects[i], __privateGet(this, _dirty_effects), __privateGet(this, _maybe_dirty_effects));
        }
      };
      commit_fn = function() {
        var _a2;
        for (let batch = first_batch; batch !== null; batch = __privateGet(batch, _next)) {
          var is_earlier = batch.id < this.id;
          var sources = [];
          for (const [source3, [value, is_derived]] of this.current) {
            if (batch.current.has(source3)) {
              var batch_value = (
                /** @type {[any, boolean]} */
                batch.current.get(source3)[0]
              );
              if (is_earlier && value !== batch_value) {
                batch.current.set(source3, [value, is_derived]);
              } else {
                continue;
              }
            }
            sources.push(source3);
          }
          if (is_earlier) {
            for (const [effect2, deferred2] of this.async_deriveds) {
              const d = batch.async_deriveds.get(effect2);
              if (d) deferred2.promise.then(d.resolve).catch(d.reject);
            }
          }
          var current = [...batch.current.keys()].filter(
            (source3) => !/** @type {[any, boolean]} */
            batch.current.get(source3)[1]
          );
          if (!__privateGet(batch, _started) || current.length === 0) continue;
          var others = current.filter((source3) => !this.current.has(source3));
          if (others.length === 0) {
            if (is_earlier) {
              batch.discard();
            }
          } else if (sources.length > 0) {
            if (dev_fallback_default && !__privateGet(batch, _decrement_queued)) {
              invariant(__privateGet(batch, _roots).length === 0, "Batch has scheduled roots");
            }
            if (is_earlier) {
              for (const unskipped of __privateGet(this, _unskipped_branches)) {
                batch.unskip_effect(unskipped, (e) => {
                  var _a3;
                  if ((e.f & (BLOCK_EFFECT | ASYNC)) !== 0) {
                    batch.schedule(e);
                  } else {
                    __privateMethod(_a3 = batch, _Batch_instances, defer_effects_fn).call(_a3, [e]);
                  }
                });
              }
            }
            batch.activate();
            var marked = /* @__PURE__ */ new Set();
            var checked = /* @__PURE__ */ new Map();
            for (var source2 of sources) {
              mark_effects(source2, others, marked, checked);
            }
            checked = /* @__PURE__ */ new Map();
            var current_unequal = [...batch.current].filter(([c, v1]) => {
              const v2 = this.current.get(c);
              if (!v2) return true;
              return v2[0] !== v1[0] || v2[1] !== v1[1];
            }).map(([c]) => c);
            if (current_unequal.length > 0) {
              for (const effect2 of __privateGet(this, _new_effects)) {
                if ((effect2.f & (DESTROYED | INERT | EAGER_EFFECT)) === 0 && depends_on(effect2, current_unequal, checked)) {
                  if ((effect2.f & (ASYNC | BLOCK_EFFECT)) !== 0) {
                    set_signal_status(effect2, DIRTY);
                    batch.schedule(effect2);
                  } else {
                    __privateGet(batch, _dirty_effects).add(effect2);
                  }
                }
              }
            }
            if (__privateGet(batch, _roots).length > 0 && !__privateGet(batch, _decrement_queued)) {
              batch.apply();
              for (var root2 of __privateGet(batch, _roots)) {
                __privateMethod(_a2 = batch, _Batch_instances, traverse_fn).call(_a2, root2, [], []);
              }
              __privateSet(batch, _roots, []);
            }
            batch.deactivate();
          }
        }
      };
      unlink_fn = function() {
        if (!this.linked) return;
        var prev = __privateGet(this, _prev);
        var next2 = __privateGet(this, _next);
        if (prev === null) {
          first_batch = next2;
        } else {
          __privateSet(prev, _next, next2);
        }
        if (next2 === null) {
          last_batch = prev;
        } else {
          __privateSet(next2, _prev, prev);
        }
        this.linked = false;
      };
      Batch = _Batch;
      eager_block_effects = null;
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/sources.js
  function set_eager_effects(v) {
    eager_effects = v;
  }
  function set_eager_effects_deferred() {
    eager_effects_deferred = true;
  }
  function source(v, stack2) {
    var signal = {
      f: 0,
      // TODO ideally we could skip this altogether, but it causes type errors
      v,
      reactions: null,
      equals,
      rv: 0,
      wv: 0
    };
    if (dev_fallback_default && tracing_mode_flag) {
      signal.created = stack2 ?? get_error("created at");
      signal.updated = null;
      signal.set_during_effect = false;
      signal.trace = null;
    }
    return signal;
  }
  // @__NO_SIDE_EFFECTS__
  function state(v, stack2) {
    const s = source(v, stack2);
    push_reaction_value(s);
    return s;
  }
  // @__NO_SIDE_EFFECTS__
  function mutable_source(initial_value, immutable = false, trackable = true) {
    var _a2;
    const s = source(initial_value);
    if (!immutable) {
      s.equals = safe_equals;
    }
    if (legacy_mode_flag && trackable && component_context !== null && component_context.l !== null) {
      ((_a2 = component_context.l).s ?? (_a2.s = [])).push(s);
    }
    return s;
  }
  function set(source2, value, should_proxy = false) {
    if (active_reaction !== null && // since we are untracking the function inside `$inspect.with` we need to add this check
    // to ensure we error if state is set inside an inspect effect
    (!untracking || (active_reaction.f & EAGER_EFFECT) !== 0) && is_runes() && (active_reaction.f & (DERIVED | BLOCK_EFFECT | ASYNC | EAGER_EFFECT)) !== 0 && (current_sources === null || !current_sources.has(source2))) {
      state_unsafe_mutation();
    }
    let new_value = should_proxy ? proxy(value) : value;
    if (dev_fallback_default) {
      tag_proxy(
        new_value,
        /** @type {string} */
        source2.label
      );
    }
    return internal_set(source2, new_value, legacy_updates);
  }
  function internal_set(source2, value, updated_during_traversal = null) {
    if (!source2.equals(value)) {
      if (is_destroying_effect) {
        old_values.set(source2, value);
      } else if (!old_values.has(source2)) {
        old_values.set(source2, source2.v);
      }
      var batch = Batch.ensure();
      batch.capture(source2, value);
      if (dev_fallback_default) {
        if (tracing_mode_flag || active_effect !== null) {
          source2.updated ?? (source2.updated = /* @__PURE__ */ new Map());
          const count = (source2.updated.get("")?.count ?? 0) + 1;
          source2.updated.set("", { error: (
            /** @type {any} */
            null
          ), count });
          if (tracing_mode_flag || count > 5) {
            const error = get_error("updated at");
            if (error !== null) {
              let entry = source2.updated.get(error.stack);
              if (!entry) {
                entry = { error, count: 0 };
                source2.updated.set(error.stack, entry);
              }
              entry.count++;
            }
          }
        }
        if (active_effect !== null) {
          source2.set_during_effect = true;
        }
      }
      if ((source2.f & DERIVED) !== 0) {
        const derived2 = (
          /** @type {Derived} */
          source2
        );
        if ((source2.f & DIRTY) !== 0) {
          execute_derived(derived2);
        }
        if (batch_values === null) {
          update_derived_status(derived2);
        }
      }
      source2.wv = increment_write_version();
      mark_reactions(source2, DIRTY, updated_during_traversal);
      if (is_runes() && active_effect !== null && (active_effect.f & CLEAN) !== 0 && (active_effect.f & (BRANCH_EFFECT | ROOT_EFFECT)) === 0) {
        if (untracked_writes === null) {
          set_untracked_writes([source2]);
        } else {
          untracked_writes.push(source2);
        }
      }
      if (!batch.is_fork && eager_effects.size > 0 && !eager_effects_deferred) {
        flush_eager_effects();
      }
    }
    return value;
  }
  function flush_eager_effects() {
    eager_effects_deferred = false;
    for (const effect2 of eager_effects) {
      if ((effect2.f & CLEAN) !== 0) {
        set_signal_status(effect2, MAYBE_DIRTY);
      }
      let dirty;
      try {
        dirty = is_dirty(effect2);
      } catch {
        dirty = true;
      }
      if (dirty) {
        update_effect(effect2);
      }
    }
    eager_effects.clear();
  }
  function increment(source2) {
    set(source2, source2.v + 1);
  }
  function mark_reactions(signal, status, updated_during_traversal) {
    var reactions = signal.reactions;
    if (reactions === null) return;
    var runes = is_runes();
    var length = reactions.length;
    for (var i = 0; i < length; i++) {
      var reaction = reactions[i];
      var flags2 = reaction.f;
      if (!runes && reaction === active_effect) continue;
      var not_dirty = (flags2 & DIRTY) === 0;
      if (not_dirty) {
        set_signal_status(reaction, status);
      }
      if ((flags2 & EAGER_EFFECT) !== 0) {
        eager_effects.add(
          /** @type {Effect} */
          reaction
        );
      } else if ((flags2 & DERIVED) !== 0) {
        var derived2 = (
          /** @type {Derived} */
          reaction
        );
        batch_values?.delete(derived2);
        if ((flags2 & WAS_MARKED) === 0) {
          if (flags2 & CONNECTED && (active_effect === null || (active_effect.f & REACTION_IS_UPDATING) === 0)) {
            reaction.f |= WAS_MARKED;
          }
          mark_reactions(derived2, MAYBE_DIRTY, updated_during_traversal);
        }
      } else if (not_dirty) {
        var effect2 = (
          /** @type {Effect} */
          reaction
        );
        if ((flags2 & BLOCK_EFFECT) !== 0 && eager_block_effects !== null) {
          eager_block_effects.add(effect2);
        }
        if (updated_during_traversal !== null) {
          updated_during_traversal.push(effect2);
        } else {
          schedule_effect(effect2);
        }
      }
    }
  }
  var eager_effects, old_values, eager_effects_deferred;
  var init_sources = __esm({
    "node_modules/svelte/src/internal/client/reactivity/sources.js"() {
      init_esm_env();
      init_runtime();
      init_equality();
      init_constants();
      init_errors2();
      init_flags();
      init_tracing();
      init_dev();
      init_context2();
      init_batch();
      init_proxy();
      init_deriveds();
      init_status();
      eager_effects = /* @__PURE__ */ new Set();
      old_values = /* @__PURE__ */ new Map();
      eager_effects_deferred = false;
    }
  });

  // node_modules/svelte/src/internal/client/proxy.js
  function proxy(value) {
    if (typeof value !== "object" || value === null || STATE_SYMBOL in value || COMPONENT_SYMBOL in value) {
      return value;
    }
    const prototype = get_prototype_of(value);
    if (prototype !== object_prototype && prototype !== array_prototype) {
      return value;
    }
    var sources = /* @__PURE__ */ new Map();
    var is_proxied_array = is_array(value);
    var version = state(0);
    var stack2 = dev_fallback_default && tracing_mode_flag ? get_error("created at") : null;
    var parent_version = update_version;
    var with_parent = (fn) => {
      if (update_version === parent_version) {
        return fn();
      }
      var reaction = active_reaction;
      var version2 = update_version;
      set_active_reaction(null);
      set_update_version(parent_version);
      var result = fn();
      set_active_reaction(reaction);
      set_update_version(version2);
      return result;
    };
    if (is_proxied_array) {
      sources.set("length", state(
        /** @type {any[]} */
        value.length,
        stack2
      ));
      if (dev_fallback_default) {
        value = /** @type {any} */
        inspectable_array(
          /** @type {any[]} */
          value
        );
      }
    }
    var path = "";
    let updating = false;
    function update_path(new_path) {
      if (updating) return;
      updating = true;
      path = new_path;
      tag(version, `${path} version`);
      for (const [prop2, source2] of sources) {
        tag(source2, get_label(path, prop2));
      }
      updating = false;
    }
    return new Proxy(
      /** @type {any} */
      value,
      {
        defineProperty(_, prop2, descriptor) {
          if (!("value" in descriptor) || descriptor.configurable === false || descriptor.enumerable === false || descriptor.writable === false) {
            state_descriptors_fixed();
          }
          var s = sources.get(prop2);
          if (s === void 0) {
            with_parent(() => {
              var s2 = state(descriptor.value, stack2);
              sources.set(prop2, s2);
              if (dev_fallback_default && typeof prop2 === "string") {
                tag(s2, get_label(path, prop2));
              }
              return s2;
            });
          } else {
            set(s, descriptor.value, true);
          }
          return true;
        },
        deleteProperty(target, prop2) {
          var s = sources.get(prop2);
          if (s === void 0) {
            if (prop2 in target) {
              const s2 = with_parent(() => state(UNINITIALIZED, stack2));
              sources.set(prop2, s2);
              increment(version);
              if (dev_fallback_default) {
                tag(s2, get_label(path, prop2));
              }
            }
          } else {
            set(s, UNINITIALIZED);
            increment(version);
          }
          return true;
        },
        get(target, prop2, receiver) {
          if (prop2 === STATE_SYMBOL) {
            return value;
          }
          if (dev_fallback_default && prop2 === PROXY_PATH_SYMBOL) {
            return update_path;
          }
          var s = sources.get(prop2);
          var exists = prop2 in target;
          if (s === void 0 && (!exists || get_descriptor(target, prop2)?.writable)) {
            s = with_parent(() => {
              var p = proxy(exists ? target[prop2] : UNINITIALIZED);
              var s2 = state(p, stack2);
              if (dev_fallback_default) {
                tag(s2, get_label(path, prop2));
              }
              return s2;
            });
            sources.set(prop2, s);
          }
          if (s !== void 0) {
            var v = get2(s);
            return v === UNINITIALIZED ? void 0 : v;
          }
          return Reflect.get(target, prop2, receiver);
        },
        getOwnPropertyDescriptor(target, prop2) {
          var descriptor = Reflect.getOwnPropertyDescriptor(target, prop2);
          if (descriptor && "value" in descriptor) {
            var s = sources.get(prop2);
            if (s) descriptor.value = get2(s);
          } else if (descriptor === void 0) {
            var source2 = sources.get(prop2);
            var value2 = source2?.v;
            if (source2 !== void 0 && value2 !== UNINITIALIZED) {
              return {
                enumerable: true,
                configurable: true,
                value: value2,
                writable: true
              };
            }
          }
          return descriptor;
        },
        has(target, prop2) {
          if (prop2 === STATE_SYMBOL) {
            return true;
          }
          var s = sources.get(prop2);
          var has = s !== void 0 && s.v !== UNINITIALIZED || Reflect.has(target, prop2);
          if (s !== void 0 || active_effect !== null && (!has || get_descriptor(target, prop2)?.writable)) {
            if (s === void 0) {
              s = with_parent(() => {
                var p = has ? proxy(target[prop2]) : UNINITIALIZED;
                var s2 = state(p, stack2);
                if (dev_fallback_default) {
                  tag(s2, get_label(path, prop2));
                }
                return s2;
              });
              sources.set(prop2, s);
            }
            var value2 = get2(s);
            if (value2 === UNINITIALIZED) {
              return false;
            }
          }
          return has;
        },
        set(target, prop2, value2, receiver) {
          var s = sources.get(prop2);
          var has = prop2 in target;
          if (is_proxied_array && prop2 === "length") {
            for (var i = value2; i < /** @type {Source<number>} */
            s.v; i += 1) {
              var other_s = sources.get(i + "");
              if (other_s !== void 0) {
                set(other_s, UNINITIALIZED);
              } else if (i in target) {
                other_s = with_parent(() => state(UNINITIALIZED, stack2));
                sources.set(i + "", other_s);
                if (dev_fallback_default) {
                  tag(other_s, get_label(path, i));
                }
              }
            }
          }
          if (s === void 0) {
            if (!has || get_descriptor(target, prop2)?.writable) {
              s = with_parent(() => state(void 0, stack2));
              if (dev_fallback_default) {
                tag(s, get_label(path, prop2));
              }
              set(s, proxy(value2));
              sources.set(prop2, s);
            }
          } else {
            has = s.v !== UNINITIALIZED;
            var p = with_parent(() => proxy(value2));
            set(s, p);
          }
          var descriptor = Reflect.getOwnPropertyDescriptor(target, prop2);
          if (descriptor?.set) {
            descriptor.set.call(receiver, value2);
          }
          if (!has) {
            if (is_proxied_array && typeof prop2 === "string") {
              var ls = (
                /** @type {Source<number>} */
                sources.get("length")
              );
              var n = Number(prop2);
              if (Number.isInteger(n) && n >= ls.v) {
                set(ls, n + 1);
              }
            }
            increment(version);
          }
          return true;
        },
        ownKeys(target) {
          get2(version);
          var own_keys = Reflect.ownKeys(target).filter((key3) => {
            var source3 = sources.get(key3);
            return source3 === void 0 || source3.v !== UNINITIALIZED;
          });
          for (var [key2, source2] of sources) {
            if (source2.v !== UNINITIALIZED && !(key2 in target)) {
              own_keys.push(key2);
            }
          }
          return own_keys;
        },
        setPrototypeOf() {
          state_prototype_fixed();
        }
      }
    );
  }
  function get_label(path, prop2) {
    if (typeof prop2 === "symbol") return `${path}[Symbol(${prop2.description ?? ""})]`;
    if (regex_is_valid_identifier.test(prop2)) return `${path}.${prop2}`;
    return /^\d+$/.test(prop2) ? `${path}[${prop2}]` : `${path}['${prop2}']`;
  }
  function get_proxied_value(value) {
    try {
      if (value !== null && typeof value === "object" && STATE_SYMBOL in value) {
        return value[STATE_SYMBOL];
      }
    } catch {
    }
    return value;
  }
  function inspectable_array(array) {
    return new Proxy(array, {
      get(target, prop2, receiver) {
        var value = Reflect.get(target, prop2, receiver);
        if (!ARRAY_MUTATING_METHODS.has(
          /** @type {string} */
          prop2
        )) {
          return value;
        }
        return function(...args) {
          set_eager_effects_deferred();
          var result = value.apply(this, args);
          flush_eager_effects();
          return result;
        };
      }
    });
  }
  var regex_is_valid_identifier, ARRAY_MUTATING_METHODS;
  var init_proxy = __esm({
    "node_modules/svelte/src/internal/client/proxy.js"() {
      init_esm_env();
      init_runtime();
      init_utils();
      init_sources();
      init_constants();
      init_constants2();
      init_errors2();
      init_tracing();
      init_dev();
      init_flags();
      regex_is_valid_identifier = /^[a-zA-Z_$][a-zA-Z_$0-9]*$/;
      ARRAY_MUTATING_METHODS = /* @__PURE__ */ new Set([
        "copyWithin",
        "fill",
        "pop",
        "push",
        "reverse",
        "shift",
        "sort",
        "splice",
        "unshift"
      ]);
    }
  });

  // node_modules/svelte/src/internal/client/dev/equality.js
  function init_array_prototype_warnings() {
    const array_prototype2 = Array.prototype;
    const cleanup = Array.__svelte_cleanup;
    if (cleanup) {
      cleanup();
    }
    const { indexOf, lastIndexOf, includes: includes2 } = array_prototype2;
    array_prototype2.indexOf = function(item, from_index) {
      const index2 = indexOf.call(this, item, from_index);
      if (index2 === -1) {
        for (let i = from_index ?? 0; i < this.length; i += 1) {
          if (get_proxied_value(this[i]) === item) {
            state_proxy_equality_mismatch("array.indexOf(...)");
            break;
          }
        }
      }
      return index2;
    };
    array_prototype2.lastIndexOf = function(item, from_index) {
      const index2 = lastIndexOf.call(this, item, from_index ?? this.length - 1);
      if (index2 === -1) {
        for (let i = 0; i <= (from_index ?? this.length - 1); i += 1) {
          if (get_proxied_value(this[i]) === item) {
            state_proxy_equality_mismatch("array.lastIndexOf(...)");
            break;
          }
        }
      }
      return index2;
    };
    array_prototype2.includes = function(item, from_index) {
      const has = includes2.call(this, item, from_index);
      if (!has) {
        for (let i = 0; i < this.length; i += 1) {
          if (get_proxied_value(this[i]) === item) {
            state_proxy_equality_mismatch("array.includes(...)");
            break;
          }
        }
      }
      return has;
    };
    Array.__svelte_cleanup = () => {
      array_prototype2.indexOf = indexOf;
      array_prototype2.lastIndexOf = lastIndexOf;
      array_prototype2.includes = includes2;
    };
  }
  var init_equality2 = __esm({
    "node_modules/svelte/src/internal/client/dev/equality.js"() {
      init_warnings();
      init_proxy();
    }
  });

  // node_modules/svelte/src/internal/client/dom/operations.js
  function init_operations2() {
    if ($window !== void 0) {
      return;
    }
    $window = window;
    $document = document;
    is_firefox = /Firefox/.test(navigator.userAgent);
    var element_prototype = Element.prototype;
    var node_prototype = Node.prototype;
    var text_prototype = Text.prototype;
    first_child_getter = get_descriptor(node_prototype, "firstChild").get;
    next_sibling_getter = get_descriptor(node_prototype, "nextSibling").get;
    if (is_extensible(element_prototype)) {
      element_prototype[CLASS_CACHE] = void 0;
      element_prototype[ATTRIBUTES_CACHE] = null;
      element_prototype[STYLE_CACHE] = void 0;
      element_prototype.__e = void 0;
    }
    if (is_extensible(text_prototype)) {
      text_prototype[TEXT_CACHE] = void 0;
    }
    if (dev_fallback_default) {
      element_prototype.__svelte_meta = null;
      init_array_prototype_warnings();
    }
  }
  function create_text(value = "") {
    return document.createTextNode(value);
  }
  // @__NO_SIDE_EFFECTS__
  function get_first_child(node) {
    return (
      /** @type {TemplateNode | null} */
      first_child_getter.call(node)
    );
  }
  // @__NO_SIDE_EFFECTS__
  function get_next_sibling(node) {
    return (
      /** @type {TemplateNode | null} */
      next_sibling_getter.call(node)
    );
  }
  function child(node, is_text) {
    if (!hydrating) {
      return /* @__PURE__ */ get_first_child(node);
    }
    var child2 = /* @__PURE__ */ get_first_child(hydrate_node);
    if (child2 === null) {
      child2 = hydrate_node.appendChild(create_text());
    } else if (is_text && child2.nodeType !== TEXT_NODE) {
      var text2 = create_text();
      child2?.before(text2);
      set_hydrate_node(text2);
      return text2;
    }
    if (is_text) {
      merge_text_nodes(
        /** @type {Text} */
        child2
      );
    }
    set_hydrate_node(child2);
    return child2;
  }
  function only_child(node, is_text = false) {
    if (!hydrating) {
      return /* @__PURE__ */ get_first_child(node);
    }
    var first = child(node, is_text);
    reset(node);
    return first;
  }
  function sibling(node, count = 1, is_text = false) {
    let next_sibling = hydrating ? hydrate_node : node;
    var last_sibling;
    while (count--) {
      last_sibling = next_sibling;
      next_sibling = /** @type {TemplateNode} */
      /* @__PURE__ */ get_next_sibling(next_sibling);
    }
    if (!hydrating) {
      return next_sibling;
    }
    if (is_text) {
      if (next_sibling?.nodeType !== TEXT_NODE) {
        var text2 = create_text();
        if (next_sibling === null) {
          last_sibling?.after(text2);
        } else {
          next_sibling.before(text2);
        }
        set_hydrate_node(text2);
        return text2;
      }
      merge_text_nodes(
        /** @type {Text} */
        next_sibling
      );
    }
    set_hydrate_node(next_sibling);
    return next_sibling;
  }
  function clear_text_content(node) {
    node.textContent = "";
  }
  function should_defer_append() {
    if (!async_mode_flag) return false;
    if (eager_block_effects !== null) return false;
    var flags2 = (
      /** @type {Effect} */
      active_effect.f
    );
    return (flags2 & REACTION_RAN) !== 0;
  }
  function create_element(tag2, namespace, is2) {
    if (namespace == null || namespace === NAMESPACE_HTML) {
      return (
        /** @type {T extends keyof HTMLElementTagNameMap ? HTMLElementTagNameMap[T] : Element} */
        is2 ? document.createElement(tag2, { is: is2 }) : document.createElement(tag2)
      );
    }
    return (
      /** @type {T extends keyof HTMLElementTagNameMap ? HTMLElementTagNameMap[T] : Element} */
      is2 ? document.createElementNS(namespace, tag2, { is: is2 }) : document.createElementNS(namespace, tag2)
    );
  }
  function merge_text_nodes(text2) {
    if (
      /** @type {string} */
      text2.nodeValue.length < 65536
    ) {
      return;
    }
    let next2 = text2.nextSibling;
    while (next2 !== null && next2.nodeType === TEXT_NODE) {
      next2.remove();
      text2.nodeValue += /** @type {string} */
      next2.nodeValue;
      next2 = text2.nextSibling;
    }
  }
  var $window, $document, is_firefox, first_child_getter, next_sibling_getter;
  var init_operations = __esm({
    "node_modules/svelte/src/internal/client/dom/operations.js"() {
      init_hydration();
      init_esm_env();
      init_equality2();
      init_utils();
      init_runtime();
      init_flags();
      init_constants();
      init_batch();
      init_constants2();
    }
  });

  // node_modules/svelte/src/internal/client/error-handling.js
  function handle_error(error) {
    var effect2 = active_effect;
    if (effect2 === null) {
      active_reaction.f |= ERROR_VALUE;
      return error;
    }
    if (dev_fallback_default && error instanceof Error && !adjustments.has(error)) {
      adjustments.set(error, get_adjustments(error, effect2));
    }
    if ((effect2.f & REACTION_RAN) === 0 && (effect2.f & EFFECT) === 0) {
      if (dev_fallback_default && !effect2.parent && error instanceof Error) {
        apply_adjustments(error);
      }
      throw error;
    }
    invoke_error_boundary(error, effect2);
  }
  function invoke_error_boundary(error, effect2) {
    if (effect2 !== null && (effect2.f & DESTROYED) !== 0) {
      return;
    }
    while (effect2 !== null) {
      if ((effect2.f & BOUNDARY_EFFECT) !== 0 && (effect2.f & (DESTROYED | DESTROYING)) === 0) {
        if ((effect2.f & REACTION_RAN) === 0) {
          throw error;
        }
        try {
          effect2.b.error(error);
          return;
        } catch (e) {
          error = e;
        }
      }
      effect2 = effect2.parent;
    }
    if (dev_fallback_default && error instanceof Error) {
      apply_adjustments(error);
    }
    throw error;
  }
  function get_adjustments(error, effect2) {
    const message_descriptor = get_descriptor(error, "message");
    if (message_descriptor && !message_descriptor.configurable) return;
    var indent = is_firefox ? "  " : "	";
    var component_stack = `
${indent}in ${effect2.fn?.name || "<unknown>"}`;
    var context = effect2.ctx;
    while (context !== null) {
      component_stack += `
${indent}in ${context.function?.[FILENAME].split("/").pop()}`;
      context = context.p;
    }
    return {
      message: error.message + `
${component_stack}
`,
      stack: error.stack?.split("\n").filter((line) => !line.includes("svelte/src/internal")).join("\n")
    };
  }
  function apply_adjustments(error) {
    const adjusted = adjustments.get(error);
    if (adjusted) {
      define_property(error, "message", {
        value: adjusted.message
      });
      define_property(error, "stack", {
        value: adjusted.stack
      });
    }
  }
  var adjustments;
  var init_error_handling = __esm({
    "node_modules/svelte/src/internal/client/error-handling.js"() {
      init_esm_env();
      init_constants2();
      init_operations();
      init_constants();
      init_utils();
      init_runtime();
      adjustments = /* @__PURE__ */ new WeakMap();
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/effects.js
  function validate_effect(rune) {
    if (active_effect === null) {
      if (active_reaction === null) {
        effect_orphan(rune);
      }
      effect_in_unowned_derived();
    }
    if (is_destroying_effect) {
      effect_in_teardown(rune);
    }
  }
  function push_effect(effect2, parent_effect) {
    var parent_last = parent_effect.last;
    if (parent_last === null) {
      parent_effect.last = parent_effect.first = effect2;
    } else {
      parent_last.next = effect2;
      effect2.prev = parent_last;
      parent_effect.last = effect2;
    }
  }
  function create_effect(type, fn) {
    var parent = active_effect;
    if (dev_fallback_default) {
      while (parent !== null && (parent.f & EAGER_EFFECT) !== 0) {
        parent = parent.parent;
      }
    }
    if (parent !== null && (parent.f & INERT) !== 0) {
      type |= INERT;
    }
    var effect2 = {
      ctx: component_context,
      deps: null,
      nodes: null,
      f: type | DIRTY | CONNECTED,
      first: null,
      fn,
      last: null,
      next: null,
      parent,
      b: parent && parent.b,
      prev: null,
      teardown: null,
      wv: 0,
      ac: null
    };
    if (dev_fallback_default) {
      effect2.component_function = dev_current_component_function;
    }
    current_batch?.register_created_effect(effect2);
    var e = effect2;
    if ((type & EFFECT) !== 0) {
      if (collected_effects !== null) {
        collected_effects.push(effect2);
      } else {
        Batch.ensure().schedule(effect2);
      }
    } else if (fn !== null) {
      try {
        update_effect(effect2);
      } catch (e2) {
        destroy_effect(effect2);
        throw e2;
      }
      if (e.deps === null && e.teardown === null && e.nodes === null && e.first === e.last && // either `null`, or a singular child
      (e.f & EFFECT_PRESERVED) === 0) {
        e = e.first;
        if ((type & BLOCK_EFFECT) !== 0 && (type & EFFECT_TRANSPARENT) !== 0 && e !== null) {
          e.f |= EFFECT_TRANSPARENT;
        }
      }
    }
    if (e !== null) {
      e.parent = parent;
      if (parent !== null) {
        push_effect(e, parent);
      }
      if (active_reaction !== null && (active_reaction.f & DERIVED) !== 0 && (type & ROOT_EFFECT) === 0) {
        var derived2 = (
          /** @type {Derived} */
          active_reaction
        );
        (derived2.effects ?? (derived2.effects = [])).push(e);
      }
    }
    return effect2;
  }
  function effect_tracking() {
    return active_reaction !== null && !untracking;
  }
  function teardown(fn) {
    const effect2 = create_effect(RENDER_EFFECT, null);
    set_signal_status(effect2, CLEAN);
    effect2.teardown = fn;
    return effect2;
  }
  function user_effect(fn) {
    validate_effect("$effect");
    if (dev_fallback_default) {
      define_property(fn, "name", {
        value: "$effect"
      });
    }
    var flags2 = (
      /** @type {Effect} */
      active_effect.f
    );
    var defer = !active_reaction && (flags2 & BRANCH_EFFECT) !== 0 && component_context !== null && !component_context.i;
    if (defer) {
      var context = (
        /** @type {ComponentContext} */
        component_context
      );
      (context.e ?? (context.e = [])).push(fn);
    } else {
      return create_user_effect(fn);
    }
  }
  function create_user_effect(fn) {
    return create_effect(EFFECT | USER_EFFECT, fn);
  }
  function effect_root(fn) {
    Batch.ensure();
    const effect2 = create_effect(ROOT_EFFECT | EFFECT_PRESERVED, fn);
    return () => {
      destroy_effect(effect2);
    };
  }
  function component_root(fn) {
    Batch.ensure();
    const effect2 = create_effect(ROOT_EFFECT | EFFECT_PRESERVED, fn);
    return (options = {}) => {
      return new Promise((fulfil) => {
        if (options.outro) {
          pause_effect(effect2, () => {
            destroy_effect(effect2);
            fulfil(void 0);
          });
        } else {
          destroy_effect(effect2);
          fulfil(void 0);
        }
      });
    };
  }
  function async_effect(fn) {
    return create_effect(ASYNC | EFFECT_PRESERVED, fn);
  }
  function render_effect(fn, flags2 = 0) {
    return create_effect(RENDER_EFFECT | flags2, fn);
  }
  function template_effect(fn, sync = [], async2 = [], blockers = []) {
    flatten(blockers, sync, async2, (values) => {
      create_effect(RENDER_EFFECT, () => {
        fn(...values.map(get2));
      });
    });
  }
  function block(fn, flags2 = 0) {
    var effect2 = create_effect(BLOCK_EFFECT | flags2, fn);
    if (dev_fallback_default) {
      effect2.dev_stack = dev_stack;
    }
    return effect2;
  }
  function branch(fn) {
    return create_effect(BRANCH_EFFECT | EFFECT_PRESERVED, fn);
  }
  function execute_effect_teardown(effect2) {
    var teardown2 = effect2.teardown;
    if (teardown2 !== null) {
      const previously_destroying_effect = is_destroying_effect;
      const previous_reaction = active_reaction;
      set_is_destroying_effect(true);
      set_active_reaction(null);
      try {
        teardown2.call(null);
      } catch (error) {
        invoke_error_boundary(error, effect2.parent);
      } finally {
        set_is_destroying_effect(previously_destroying_effect);
        set_active_reaction(previous_reaction);
      }
    }
  }
  function destroy_effect_children(signal, remove_dom = false) {
    var effect2 = signal.first;
    signal.first = signal.last = null;
    while (effect2 !== null) {
      const controller = effect2.ac;
      if (controller !== null) {
        without_reactive_context(() => {
          controller.abort(STALE_REACTION);
        });
      }
      var next2 = effect2.next;
      if ((effect2.f & ROOT_EFFECT) !== 0) {
        effect2.parent = null;
      } else {
        destroy_effect(effect2, remove_dom);
      }
      effect2 = next2;
    }
  }
  function destroy_block_effect_children(signal) {
    var effect2 = signal.first;
    while (effect2 !== null) {
      var next2 = effect2.next;
      if ((effect2.f & BRANCH_EFFECT) === 0) {
        destroy_effect(effect2);
      }
      effect2 = next2;
    }
  }
  function destroy_effect(effect2, remove_dom = true) {
    var removed = false;
    if ((remove_dom || (effect2.f & HEAD_EFFECT) !== 0) && effect2.nodes !== null && effect2.nodes.end !== null) {
      remove_effect_dom(
        effect2.nodes.start,
        /** @type {TemplateNode} */
        effect2.nodes.end
      );
      removed = true;
    }
    effect2.f |= DESTROYING;
    destroy_effect_children(effect2, remove_dom && !removed);
    remove_reactions(effect2, 0);
    var transitions = effect2.nodes && effect2.nodes.t;
    if (transitions !== null) {
      for (const transition2 of transitions) {
        transition2.stop();
      }
    }
    execute_effect_teardown(effect2);
    effect2.f ^= DESTROYING;
    effect2.f |= DESTROYED;
    var parent = effect2.parent;
    if (parent !== null && parent.first !== null) {
      unlink_effect(effect2);
    }
    if (dev_fallback_default) {
      effect2.component_function = null;
    }
    effect2.next = effect2.prev = effect2.teardown = effect2.ctx = effect2.deps = effect2.fn = effect2.nodes = effect2.ac = effect2.b = null;
  }
  function remove_effect_dom(node, end) {
    while (node !== null) {
      var next2 = node === end ? null : get_next_sibling(node);
      node.remove();
      node = next2;
    }
  }
  function unlink_effect(effect2) {
    var parent = effect2.parent;
    var prev = effect2.prev;
    var next2 = effect2.next;
    if (prev !== null) prev.next = next2;
    if (next2 !== null) next2.prev = prev;
    if (parent !== null) {
      if (parent.first === effect2) parent.first = next2;
      if (parent.last === effect2) parent.last = prev;
    }
  }
  function pause_effect(effect2, callback, destroy = true) {
    var transitions = [];
    effect2.f |= PAUSED;
    pause_children(effect2, transitions, true);
    var fn = () => {
      if (destroy) destroy_effect(effect2);
      if (callback) callback();
    };
    var remaining = transitions.length;
    if (remaining > 0) {
      var check = () => --remaining || fn();
      for (var transition2 of transitions) {
        transition2.out(check);
      }
    } else {
      fn();
    }
  }
  function pause_children(effect2, transitions, local) {
    if ((effect2.f & INERT) !== 0) return;
    effect2.f ^= INERT;
    var t = effect2.nodes && effect2.nodes.t;
    if (t !== null) {
      for (const transition2 of t) {
        if (transition2.is_global || local) {
          transitions.push(transition2);
        }
      }
    }
    var child2 = effect2.first;
    while (child2 !== null) {
      var sibling2 = child2.next;
      if ((child2.f & ROOT_EFFECT) === 0) {
        var transparent = (child2.f & EFFECT_TRANSPARENT) !== 0 || // If this is a branch effect without a block effect parent,
        // it means the parent block effect was pruned. In that case,
        // transparency information was transferred to the branch effect.
        (child2.f & BRANCH_EFFECT) !== 0 && (effect2.f & BLOCK_EFFECT) !== 0;
        pause_children(child2, transitions, transparent ? local : false);
      }
      child2 = sibling2;
    }
  }
  function resume_effect(effect2) {
    effect2.f &= ~PAUSED;
    resume_children(effect2, true);
  }
  function resume_children(effect2, local) {
    if ((effect2.f & PAUSED) !== 0) return;
    if ((effect2.f & INERT) === 0) return;
    effect2.f ^= INERT;
    if ((effect2.f & CLEAN) === 0) {
      set_signal_status(effect2, DIRTY);
      Batch.ensure().schedule(effect2);
    }
    var child2 = effect2.first;
    while (child2 !== null) {
      var sibling2 = child2.next;
      var transparent = (child2.f & EFFECT_TRANSPARENT) !== 0 || (child2.f & BRANCH_EFFECT) !== 0;
      resume_children(child2, transparent ? local : false);
      child2 = sibling2;
    }
    var t = effect2.nodes && effect2.nodes.t;
    if (t !== null) {
      for (const transition2 of t) {
        if (transition2.is_global || local) {
          transition2.in();
        }
      }
    }
  }
  function move_effect(effect2, fragment) {
    if (!effect2.nodes) return;
    var node = effect2.nodes.start;
    var end = effect2.nodes.end;
    while (node !== null) {
      var next2 = node === end ? null : get_next_sibling(node);
      fragment.append(node);
      node = next2;
    }
  }
  var init_effects = __esm({
    "node_modules/svelte/src/internal/client/reactivity/effects.js"() {
      init_runtime();
      init_constants();
      init_error_handling();
      init_errors2();
      init_esm_env();
      init_utils();
      init_operations();
      init_context2();
      init_batch();
      init_async();
      init_shared2();
      init_status();
    }
  });

  // node_modules/svelte/src/internal/client/legacy.js
  var captured_signals;
  var init_legacy = __esm({
    "node_modules/svelte/src/internal/client/legacy.js"() {
      init_sources();
      init_runtime();
      captured_signals = null;
    }
  });

  // node_modules/svelte/src/internal/client/runtime.js
  function set_is_destroying_effect(value) {
    is_destroying_effect = value;
  }
  function set_active_reaction(reaction) {
    active_reaction = reaction;
  }
  function set_active_effect(effect2) {
    active_effect = effect2;
  }
  function push_reaction_value(value) {
    if (active_reaction !== null && (!async_mode_flag || (active_reaction.f & DERIVED) !== 0)) {
      (current_sources ?? (current_sources = /* @__PURE__ */ new Set())).add(value);
    }
  }
  function set_untracked_writes(value) {
    untracked_writes = value;
  }
  function set_update_version(value) {
    update_version = value;
  }
  function increment_write_version() {
    return ++write_version;
  }
  function is_dirty(reaction) {
    var flags2 = reaction.f;
    if ((flags2 & DIRTY) !== 0) {
      return true;
    }
    if (flags2 & DERIVED) {
      reaction.f &= ~WAS_MARKED;
    }
    if ((flags2 & MAYBE_DIRTY) !== 0) {
      var dependencies = (
        /** @type {Value[]} */
        reaction.deps
      );
      var length = dependencies.length;
      for (var i = 0; i < length; i++) {
        var dependency = dependencies[i];
        if (is_dirty(
          /** @type {Derived} */
          dependency
        )) {
          update_derived(
            /** @type {Derived} */
            dependency
          );
        }
        if (dependency.wv > reaction.wv) {
          return true;
        }
      }
      if ((flags2 & CONNECTED) !== 0 && // During time traveling we don't want to reset the status so that
      // traversal of the graph in the other batches still happens
      batch_values === null) {
        set_signal_status(reaction, CLEAN);
      }
    }
    return false;
  }
  function schedule_possible_effect_self_invalidation(signal, effect2, root2 = true) {
    var reactions = signal.reactions;
    if (reactions === null) return;
    if (!async_mode_flag && current_sources !== null && current_sources.has(signal)) {
      return;
    }
    for (var i = 0; i < reactions.length; i++) {
      var reaction = reactions[i];
      if ((reaction.f & DERIVED) !== 0) {
        schedule_possible_effect_self_invalidation(
          /** @type {Derived} */
          reaction,
          effect2,
          false
        );
      } else if (effect2 === reaction) {
        if (root2) {
          set_signal_status(reaction, DIRTY);
        } else if ((reaction.f & CLEAN) !== 0) {
          set_signal_status(reaction, MAYBE_DIRTY);
        }
        schedule_effect(
          /** @type {Effect} */
          reaction
        );
      }
    }
  }
  function update_reaction(reaction) {
    var previous_deps = new_deps;
    var previous_skipped_deps = skipped_deps;
    var previous_untracked_writes = untracked_writes;
    var previous_reaction = active_reaction;
    var previous_sources = current_sources;
    var previous_component_context = component_context;
    var previous_untracking = untracking;
    var previous_update_version = update_version;
    var flags2 = reaction.f;
    new_deps = /** @type {null | Value[]} */
    null;
    skipped_deps = 0;
    untracked_writes = null;
    active_reaction = (flags2 & (BRANCH_EFFECT | ROOT_EFFECT)) === 0 ? reaction : null;
    current_sources = null;
    set_component_context(reaction.ctx);
    untracking = false;
    update_version = ++read_version;
    if (reaction.ac !== null) {
      without_reactive_context(() => {
        reaction.ac.abort(STALE_REACTION);
      });
      reaction.ac = null;
    }
    try {
      reaction.f |= REACTION_IS_UPDATING;
      var fn = (
        /** @type {Function} */
        reaction.fn
      );
      var result = fn();
      reaction.f |= REACTION_RAN;
      var deps = update_dependencies(reaction);
      if (is_runes() && untracked_writes !== null && !untracking && deps !== null && (reaction.f & (DERIVED | MAYBE_DIRTY | DIRTY)) === 0) {
        for (var i = 0; i < /** @type {Source[]} */
        untracked_writes.length; i++) {
          schedule_possible_effect_self_invalidation(
            untracked_writes[i],
            /** @type {Effect} */
            reaction
          );
        }
      }
      if (previous_reaction !== null && previous_reaction !== reaction) {
        read_version++;
        if (previous_reaction.deps !== null) {
          for (let i2 = 0; i2 < previous_skipped_deps; i2 += 1) {
            previous_reaction.deps[i2].rv = read_version;
          }
        }
        if (previous_deps !== null) {
          for (const dep of previous_deps) {
            dep.rv = read_version;
          }
        }
        if (untracked_writes !== null) {
          if (previous_untracked_writes === null) {
            previous_untracked_writes = untracked_writes;
          } else {
            previous_untracked_writes.push(.../** @type {Source[]} */
            untracked_writes);
          }
        }
      }
      if ((reaction.f & ERROR_VALUE) !== 0) {
        reaction.f ^= ERROR_VALUE;
      }
      return result;
    } catch (error) {
      update_dependencies(reaction);
      return handle_error(error);
    } finally {
      reaction.f ^= REACTION_IS_UPDATING;
      new_deps = previous_deps;
      skipped_deps = previous_skipped_deps;
      untracked_writes = previous_untracked_writes;
      active_reaction = previous_reaction;
      current_sources = previous_sources;
      set_component_context(previous_component_context);
      untracking = previous_untracking;
      update_version = previous_update_version;
    }
  }
  function update_dependencies(reaction) {
    var _a2;
    var deps = reaction.deps;
    var is_fork = current_batch?.is_fork;
    if (new_deps !== null) {
      var i;
      if (!is_fork) {
        remove_reactions(reaction, skipped_deps);
      }
      if (deps !== null && skipped_deps > 0) {
        deps.length = skipped_deps + new_deps.length;
        for (i = 0; i < new_deps.length; i++) {
          deps[skipped_deps + i] = new_deps[i];
        }
      } else {
        reaction.deps = deps = new_deps;
      }
      if (effect_tracking() && (reaction.f & CONNECTED) !== 0) {
        for (i = skipped_deps; i < deps.length; i++) {
          ((_a2 = deps[i]).reactions ?? (_a2.reactions = [])).push(reaction);
        }
      }
    } else if (!is_fork && deps !== null && skipped_deps < deps.length) {
      remove_reactions(reaction, skipped_deps);
      deps.length = skipped_deps;
    }
    return deps;
  }
  function remove_reaction(signal, dependency) {
    let reactions = dependency.reactions;
    if (reactions !== null) {
      var index2 = index_of.call(reactions, signal);
      if (index2 !== -1) {
        var new_length = reactions.length - 1;
        if (new_length === 0) {
          reactions = dependency.reactions = null;
        } else {
          reactions[index2] = reactions[new_length];
          reactions.pop();
        }
      }
    }
    if (reactions === null && (dependency.f & DERIVED) !== 0 && // Destroying a child effect while updating a parent effect can cause a dependency to appear
    // to be unused, when in fact it is used by the currently-updating parent. Checking `new_deps`
    // allows us to skip the expensive work of disconnecting and immediately reconnecting it
    (new_deps === null || !includes.call(new_deps, dependency))) {
      var derived2 = (
        /** @type {Derived} */
        dependency
      );
      if ((derived2.f & CONNECTED) !== 0) {
        derived2.f ^= CONNECTED;
        derived2.f &= ~WAS_MARKED;
      }
      if (derived2.v !== UNINITIALIZED) {
        update_derived_status(derived2);
      }
      if (derived2.ac !== null) {
        without_reactive_context(() => {
          derived2.ac.abort(STALE_REACTION);
          derived2.ac = null;
          set_signal_status(derived2, DIRTY);
        });
      }
      freeze_derived_effects(derived2);
      remove_reactions(derived2, 0);
    }
  }
  function remove_reactions(signal, start_index) {
    var dependencies = signal.deps;
    if (dependencies === null) return;
    for (var i = start_index; i < dependencies.length; i++) {
      remove_reaction(signal, dependencies[i]);
    }
  }
  function update_effect(effect2) {
    var flags2 = effect2.f;
    if ((flags2 & DESTROYED) !== 0) {
      return;
    }
    set_signal_status(effect2, CLEAN);
    var previous_effect = active_effect;
    var was_updating_effect = is_updating_effect;
    active_effect = effect2;
    is_updating_effect = (flags2 & (BRANCH_EFFECT | ROOT_EFFECT)) === 0;
    if (dev_fallback_default) {
      var previous_component_fn = dev_current_component_function;
      set_dev_current_component_function(effect2.component_function);
      var previous_stack = (
        /** @type {any} */
        dev_stack
      );
      set_dev_stack(effect2.dev_stack ?? dev_stack);
    }
    try {
      if ((flags2 & (BLOCK_EFFECT | MANAGED_EFFECT)) !== 0) {
        destroy_block_effect_children(effect2);
      } else {
        destroy_effect_children(effect2);
      }
      execute_effect_teardown(effect2);
      var teardown2 = update_reaction(effect2);
      effect2.teardown = typeof teardown2 === "function" ? teardown2 : null;
      effect2.wv = write_version;
      if (dev_fallback_default && tracing_mode_flag && (effect2.f & DIRTY) !== 0 && effect2.deps !== null) {
        for (var dep of effect2.deps) {
          if (dep.set_during_effect) {
            dep.wv = increment_write_version();
            dep.set_during_effect = false;
          }
        }
      }
    } finally {
      is_updating_effect = was_updating_effect;
      active_effect = previous_effect;
      if (dev_fallback_default) {
        set_dev_current_component_function(previous_component_fn);
        set_dev_stack(previous_stack);
      }
    }
  }
  async function tick() {
    if (async_mode_flag) {
      return new Promise((f) => {
        requestAnimationFrame(() => f());
        setTimeout(() => f());
      });
    }
    await Promise.resolve();
    flushSync();
  }
  function get2(signal) {
    var flags2 = signal.f;
    var is_derived = (flags2 & DERIVED) !== 0;
    captured_signals?.add(signal);
    if (active_reaction !== null && !untracking) {
      var destroyed = active_effect !== null && (active_effect.f & DESTROYED) !== 0;
      if (!destroyed && (current_sources === null || !current_sources.has(signal))) {
        var deps = active_reaction.deps;
        if ((active_reaction.f & REACTION_IS_UPDATING) !== 0) {
          if (signal.rv < read_version) {
            signal.rv = read_version;
            if (new_deps === null && deps !== null && deps[skipped_deps] === signal) {
              skipped_deps++;
            } else if (new_deps === null) {
              new_deps = [signal];
            } else {
              new_deps.push(signal);
            }
          }
        } else {
          active_reaction.deps ?? (active_reaction.deps = []);
          if (!includes.call(active_reaction.deps, signal)) {
            active_reaction.deps.push(signal);
          }
          var reactions = signal.reactions;
          if (reactions === null) {
            signal.reactions = [active_reaction];
          } else if (!includes.call(reactions, active_reaction)) {
            reactions.push(active_reaction);
          }
        }
      }
    }
    if (dev_fallback_default) {
      if (!untracking && reactivity_loss_tracker && // By checking that current/previous batch are null we filter out false positives.
      // reactivity_loss_tracker is only reset after a microtask, so if a flush happens
      // before that, we get warnings for things we shouldn't warn on.
      current_batch === null && previous_batch === null && !reactivity_loss_tracker.warned && (reactivity_loss_tracker.effect.f & REACTION_IS_UPDATING) === 0 && !reactivity_loss_tracker.effect_deps.has(signal)) {
        reactivity_loss_tracker.warned = true;
        await_reactivity_loss(
          /** @type {string} */
          signal.label
        );
        var trace2 = get_error("traced at");
        if (trace2) console.warn(trace2);
      }
      recent_async_deriveds.delete(signal);
      if (tracing_mode_flag && !untracking && tracing_expressions !== null && active_reaction !== null && tracing_expressions.reaction === active_reaction) {
        if (signal.trace) {
          signal.trace();
        } else {
          trace2 = get_error("traced at");
          if (trace2) {
            var entry = tracing_expressions.entries.get(signal);
            if (entry === void 0) {
              entry = { traces: [] };
              tracing_expressions.entries.set(signal, entry);
            }
            var last = entry.traces[entry.traces.length - 1];
            if (trace2.stack !== last?.stack) {
              entry.traces.push(trace2);
            }
          }
        }
      }
    }
    if (is_destroying_effect && old_values.has(signal)) {
      return old_values.get(signal);
    }
    if (is_derived) {
      var derived2 = (
        /** @type {Derived} */
        signal
      );
      if (is_destroying_effect) {
        var value = derived2.v;
        if ((derived2.f & CLEAN) === 0 && derived2.reactions !== null || depends_on_old_values(derived2)) {
          value = execute_derived(derived2);
        }
        old_values.set(derived2, value);
        return value;
      }
      var should_connect = (derived2.f & CONNECTED) === 0 && !untracking && active_reaction !== null && (is_updating_effect || (active_reaction.f & CONNECTED) !== 0);
      var is_new = (derived2.f & REACTION_RAN) === 0;
      if (is_dirty(derived2)) {
        if (should_connect) {
          derived2.f |= CONNECTED;
        }
        update_derived(derived2);
      }
      if (should_connect && !is_new) {
        unfreeze_derived_effects(derived2);
        reconnect(derived2);
      }
    }
    if (batch_values?.has(signal)) {
      return batch_values.get(signal);
    }
    if ((signal.f & ERROR_VALUE) !== 0) {
      throw signal.v;
    }
    return signal.v;
  }
  function reconnect(derived2) {
    derived2.f |= CONNECTED;
    if (derived2.deps === null) return;
    for (const dep of derived2.deps) {
      (dep.reactions ?? (dep.reactions = [])).push(derived2);
      if ((dep.f & DERIVED) !== 0 && (dep.f & CONNECTED) === 0) {
        unfreeze_derived_effects(
          /** @type {Derived} */
          dep
        );
        reconnect(
          /** @type {Derived} */
          dep
        );
      }
    }
  }
  function depends_on_old_values(derived2) {
    if (derived2.v === UNINITIALIZED) return true;
    if (derived2.deps === null) return false;
    for (const dep of derived2.deps) {
      if (old_values.has(dep)) {
        return true;
      }
      if ((dep.f & DERIVED) !== 0 && depends_on_old_values(
        /** @type {Derived} */
        dep
      )) {
        return true;
      }
    }
    return false;
  }
  function untrack(fn) {
    var previous_untracking = untracking;
    try {
      untracking = true;
      return fn();
    } finally {
      untracking = previous_untracking;
    }
  }
  var is_updating_effect, is_destroying_effect, active_reaction, untracking, active_effect, current_sources, new_deps, skipped_deps, untracked_writes, write_version, read_version, update_version;
  var init_runtime = __esm({
    "node_modules/svelte/src/internal/client/runtime.js"() {
      init_esm_env();
      init_utils();
      init_effects();
      init_constants();
      init_sources();
      init_deriveds();
      init_flags();
      init_tracing();
      init_dev();
      init_context2();
      init_batch();
      init_error_handling();
      init_constants2();
      init_legacy();
      init_shared2();
      init_status();
      init_warnings();
      is_updating_effect = false;
      is_destroying_effect = false;
      active_reaction = null;
      untracking = false;
      active_effect = null;
      current_sources = null;
      new_deps = null;
      skipped_deps = 0;
      untracked_writes = null;
      write_version = 1;
      read_version = 0;
      update_version = read_version;
    }
  });

  // node_modules/svelte/src/attachments/index.js
  var init_attachments = __esm({
    "node_modules/svelte/src/attachments/index.js"() {
      init_client();
      init_constants2();
      init_index_client();
      init_effects();
    }
  });

  // node_modules/svelte/src/utils.js
  function is_passive_event(name) {
    return PASSIVE_EVENTS.includes(name);
  }
  var DOM_BOOLEAN_ATTRIBUTES, DOM_PROPERTIES, PASSIVE_EVENTS, STATE_CREATION_RUNES, RUNES;
  var init_utils4 = __esm({
    "node_modules/svelte/src/utils.js"() {
      DOM_BOOLEAN_ATTRIBUTES = [
        "allowfullscreen",
        "async",
        "autofocus",
        "autoplay",
        "checked",
        "controls",
        "default",
        "disabled",
        "formnovalidate",
        "indeterminate",
        "inert",
        "ismap",
        "loop",
        "multiple",
        "muted",
        "nomodule",
        "novalidate",
        "open",
        "playsinline",
        "readonly",
        "required",
        "reversed",
        "seamless",
        "selected",
        "webkitdirectory",
        "defer",
        "disablepictureinpicture",
        "disableremoteplayback"
      ];
      DOM_PROPERTIES = [
        ...DOM_BOOLEAN_ATTRIBUTES,
        "formNoValidate",
        "isMap",
        "noModule",
        "playsInline",
        "readOnly",
        "value",
        "volume",
        "defaultValue",
        "defaultChecked",
        "srcObject",
        "noValidate",
        "allowFullscreen",
        "disablePictureInPicture",
        "disableRemotePlayback"
      ];
      PASSIVE_EVENTS = ["touchstart", "touchmove"];
      STATE_CREATION_RUNES = /** @type {const} */
      [
        "$state",
        "$state.raw",
        "$derived",
        "$derived.by"
      ];
      RUNES = /** @type {const} */
      [
        ...STATE_CREATION_RUNES,
        "$state.eager",
        "$state.snapshot",
        "$props",
        "$props.id",
        "$bindable",
        "$effect",
        "$effect.pre",
        "$effect.tracking",
        "$effect.root",
        "$effect.pending",
        "$inspect",
        "$inspect().with",
        "$inspect.trace",
        "$host"
      ];
    }
  });

  // node_modules/svelte/src/internal/client/dev/assign.js
  var init_assign = __esm({
    "node_modules/svelte/src/internal/client/dev/assign.js"() {
      init_constants();
      init_utils4();
      init_runtime();
      init_warnings();
    }
  });

  // node_modules/svelte/src/internal/client/dev/css.js
  var init_css = __esm({
    "node_modules/svelte/src/internal/client/dev/css.js"() {
    }
  });

  // node_modules/svelte/src/internal/client/dev/elements.js
  var init_elements = __esm({
    "node_modules/svelte/src/internal/client/dev/elements.js"() {
      init_constants();
      init_constants2();
      init_hydration();
      init_context2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/events.js
  function delegated(event_name, element2, handler) {
    (element2[event_symbol] ?? (element2[event_symbol] = {}))[event_name] = handler;
  }
  function delegate(events) {
    for (var i = 0; i < events.length; i++) {
      all_registered_events.add(events[i]);
    }
    for (var fn of root_event_handles) {
      fn(events);
    }
  }
  function handle_event_propagation(event2) {
    var handler_element = this;
    var owner_document = (
      /** @type {Node} */
      handler_element.ownerDocument
    );
    var event_name = event2.type;
    var path = event2.composedPath?.() || [];
    var current_target = (
      /** @type {null | Element} */
      path[0] || event2.target
    );
    last_propagated_event = event2;
    if (!last_propagated_event_clear_scheduled) {
      last_propagated_event_clear_scheduled = true;
      setTimeout(() => {
        last_propagated_event_clear_scheduled = false;
        last_propagated_event = null;
      });
    }
    var path_idx = 0;
    var handled_at = last_propagated_event === event2 && event2[event_symbol];
    if (handled_at) {
      var at_idx = path.indexOf(handled_at);
      if (at_idx !== -1 && (handler_element === document || handler_element === /** @type {any} */
      window)) {
        event2[event_symbol] = handler_element;
        return;
      }
      var handler_idx = path.indexOf(handler_element);
      if (handler_idx === -1) {
        return;
      }
      if (at_idx <= handler_idx) {
        path_idx = at_idx;
      }
    }
    current_target = /** @type {Element} */
    path[path_idx] || event2.target;
    if (current_target === handler_element) return;
    define_property(event2, "currentTarget", {
      configurable: true,
      get() {
        return current_target || owner_document;
      }
    });
    var previous_reaction = active_reaction;
    var previous_effect = active_effect;
    set_active_reaction(null);
    set_active_effect(null);
    try {
      var throw_error;
      var other_errors = [];
      while (current_target !== null) {
        if (current_target === handler_element) break;
        try {
          var delegated2 = current_target[event_symbol]?.[event_name];
          if (delegated2 != null && (!/** @type {any} */
          current_target.disabled || // DOM could've been updated already by the time this is reached, so we check this as well
          // -> the target could not have been disabled because it emits the event in the first place
          event2.target === current_target)) {
            delegated2.call(current_target, event2);
          }
        } catch (error) {
          if (throw_error) {
            other_errors.push(error);
          } else {
            throw_error = error;
          }
        }
        if (event2.cancelBubble) break;
        path_idx++;
        current_target = path_idx < path.length ? (
          /** @type {Element} */
          path[path_idx]
        ) : null;
      }
      if (throw_error) {
        for (let error of other_errors) {
          queueMicrotask(() => {
            throw error;
          });
        }
        throw throw_error;
      }
    } finally {
      event2[event_symbol] = handler_element;
      delete event2.currentTarget;
      set_active_reaction(previous_reaction);
      set_active_effect(previous_effect);
    }
  }
  var event_symbol, all_registered_events, root_event_handles, last_propagated_event, last_propagated_event_clear_scheduled;
  var init_events = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/events.js"() {
      init_effects();
      init_utils();
      init_hydration();
      init_task();
      init_constants2();
      init_warnings();
      init_runtime();
      init_shared2();
      event_symbol = /* @__PURE__ */ Symbol("events");
      all_registered_events = /* @__PURE__ */ new Set();
      root_event_handles = /* @__PURE__ */ new Set();
      last_propagated_event = null;
      last_propagated_event_clear_scheduled = false;
    }
  });

  // node_modules/svelte/src/internal/client/dom/reconciler.js
  function create_trusted_html(html2) {
    return (
      /** @type {string} */
      policy?.createHTML(html2) ?? html2
    );
  }
  function create_fragment_from_html(html2) {
    var elem = create_element("template");
    elem.innerHTML = create_trusted_html(html2.replaceAll("<!>", "<!---->"));
    return elem.content;
  }
  var policy;
  var init_reconciler = __esm({
    "node_modules/svelte/src/internal/client/dom/reconciler.js"() {
      init_operations();
      policy = // We gotta write it like this because after downleveling the pure comment may end up in the wrong location
      globalThis?.window?.trustedTypes && /* @__PURE__ */ globalThis.window.trustedTypes.createPolicy("svelte-trusted-html", {
        /** @param {string} html */
        createHTML: (html2) => {
          return html2;
        }
      });
    }
  });

  // node_modules/svelte/src/internal/client/dom/template.js
  function assign_nodes(start, end) {
    var effect2 = (
      /** @type {Effect} */
      active_effect
    );
    if (effect2.nodes === null) {
      effect2.nodes = { start, end, a: null, t: null };
    }
  }
  // @__NO_SIDE_EFFECTS__
  function from_html(content, flags2) {
    var is_fragment = (flags2 & TEMPLATE_FRAGMENT) !== 0;
    var use_import_node = (flags2 & TEMPLATE_USE_IMPORT_NODE) !== 0;
    var node;
    var has_start = !content.startsWith("<!>");
    return () => {
      if (hydrating) {
        assign_nodes(hydrate_node, null);
        return hydrate_node;
      }
      if (node === void 0) {
        node = create_fragment_from_html(has_start ? content : "<!>" + content);
        if (!is_fragment) node = /** @type {TemplateNode} */
        get_first_child(node);
      }
      var clone = (
        /** @type {TemplateNode} */
        use_import_node || is_firefox ? document.importNode(node, true) : node.cloneNode(true)
      );
      if (is_fragment) {
        var start = (
          /** @type {TemplateNode} */
          get_first_child(clone)
        );
        var end = (
          /** @type {TemplateNode} */
          clone.lastChild
        );
        assign_nodes(start, end);
      } else {
        assign_nodes(clone, clone);
      }
      return clone;
    };
  }
  function append(anchor, dom) {
    if (hydrating) {
      var effect2 = (
        /** @type {Effect & { nodes: EffectNodes }} */
        active_effect
      );
      if ((effect2.f & REACTION_RAN) === 0 || effect2.nodes.end === null) {
        effect2.nodes.end = hydrate_node;
      }
      hydrate_next();
      return;
    }
    if (anchor === null) {
      return;
    }
    anchor.before(
      /** @type {Node} */
      dom
    );
  }
  var init_template = __esm({
    "node_modules/svelte/src/internal/client/dom/template.js"() {
      init_hydration();
      init_operations();
      init_reconciler();
      init_runtime();
      init_constants2();
      init_constants();
    }
  });

  // node_modules/svelte/src/reactivity/create-subscriber.js
  function createSubscriber(start) {
    let subscribers = 0;
    let version = source(0);
    let stop;
    if (dev_fallback_default) {
      tag(version, "createSubscriber version");
    }
    return () => {
      if (effect_tracking()) {
        get2(version);
        render_effect(() => {
          if (subscribers === 0) {
            stop = untrack(() => start(() => increment(version)));
          }
          subscribers += 1;
          return () => {
            queue_micro_task(() => {
              subscribers -= 1;
              if (subscribers === 0) {
                stop?.();
                stop = void 0;
                increment(version);
              }
            });
          };
        });
      }
    };
  }
  var init_create_subscriber = __esm({
    "node_modules/svelte/src/reactivity/create-subscriber.js"() {
      init_runtime();
      init_effects();
      init_sources();
      init_tracing();
      init_esm_env();
      init_task();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/boundary.js
  function boundary(node, props, children, transform_error) {
    new Boundary(node, props, children, transform_error);
  }
  var flags, _anchor, _hydrate_open, _props, _children, _effect, _main_effect, _pending_effect, _failed_effect, _offscreen_fragment, _local_pending_count, _pending_count, _pending_count_update_queued, _dirty_effects2, _maybe_dirty_effects2, _effect_pending, _effect_pending_subscriber, _Boundary_instances, hydrate_resolved_content_fn, hydrate_failed_content_fn, create_reset_fn, hydrate_pending_content_fn, render_fn, resolve_fn, run_fn, update_pending_count_fn, handle_error_fn, Boundary;
  var init_boundary = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/boundary.js"() {
      init_constants();
      init_constants2();
      init_context2();
      init_error_handling();
      init_effects();
      init_runtime();
      init_hydration();
      init_task();
      init_errors2();
      init_warnings();
      init_esm_env();
      init_batch();
      init_sources();
      init_tracing();
      init_create_subscriber();
      init_operations();
      init_utils2();
      flags = EFFECT_TRANSPARENT | EFFECT_PRESERVED;
      Boundary = class {
        /**
         * @param {TemplateNode} node
         * @param {BoundaryProps} props
         * @param {((anchor: Node) => void)} children
         * @param {((error: unknown) => unknown) | undefined} [transform_error]
         */
        constructor(node, props, children, transform_error) {
          __privateAdd(this, _Boundary_instances);
          /** @type {Boundary | null} */
          __publicField(this, "parent");
          __publicField(this, "is_pending", false);
          /**
           * API-level transformError transform function. Transforms errors before they reach the `failed` snippet.
           * Inherited from parent boundary, or defaults to identity.
           * @type {(error: unknown) => unknown}
           */
          __publicField(this, "transform_error");
          /** @type {TemplateNode} */
          __privateAdd(this, _anchor);
          /** @type {TemplateNode | null} */
          __privateAdd(this, _hydrate_open, hydrating ? hydrate_node : null);
          /** @type {BoundaryProps} */
          __privateAdd(this, _props);
          /** @type {((anchor: Node) => void)} */
          __privateAdd(this, _children);
          /** @type {Effect} */
          __privateAdd(this, _effect);
          /** @type {Effect | null} */
          __privateAdd(this, _main_effect, null);
          /** @type {Effect | null} */
          __privateAdd(this, _pending_effect, null);
          /** @type {Effect | null} */
          __privateAdd(this, _failed_effect, null);
          /** @type {DocumentFragment | null} */
          __privateAdd(this, _offscreen_fragment, null);
          __privateAdd(this, _local_pending_count, 0);
          __privateAdd(this, _pending_count, 0);
          __privateAdd(this, _pending_count_update_queued, false);
          /** @type {Set<Effect>} */
          __privateAdd(this, _dirty_effects2, /* @__PURE__ */ new Set());
          /** @type {Set<Effect>} */
          __privateAdd(this, _maybe_dirty_effects2, /* @__PURE__ */ new Set());
          /**
           * A source containing the number of pending async deriveds/expressions.
           * Only created if `$effect.pending()` is used inside the boundary,
           * otherwise updating the source results in needless `Batch.ensure()`
           * calls followed by no-op flushes
           * @type {Source<number> | null}
           */
          __privateAdd(this, _effect_pending, null);
          __privateAdd(this, _effect_pending_subscriber, createSubscriber(() => {
            __privateSet(this, _effect_pending, source(__privateGet(this, _local_pending_count)));
            if (dev_fallback_default) {
              tag(__privateGet(this, _effect_pending), "$effect.pending()");
            }
            return () => {
              __privateSet(this, _effect_pending, null);
            };
          }));
          __privateSet(this, _anchor, node);
          __privateSet(this, _props, props);
          __privateSet(this, _children, (anchor) => {
            var effect2 = (
              /** @type {Effect} */
              active_effect
            );
            effect2.b = this;
            effect2.f |= BOUNDARY_EFFECT;
            children(anchor);
          });
          this.parent = /** @type {Effect} */
          active_effect.b;
          this.transform_error = transform_error ?? this.parent?.transform_error ?? ((e) => e);
          __privateSet(this, _effect, block(() => {
            if (hydrating) {
              const comment2 = (
                /** @type {Comment} */
                __privateGet(this, _hydrate_open)
              );
              hydrate_next();
              const server_rendered_pending = comment2.data === HYDRATION_START_ELSE;
              const server_rendered_failed = comment2.data.startsWith(HYDRATION_START_FAILED);
              if (server_rendered_failed) {
                const serialized_error = JSON.parse(comment2.data.slice(HYDRATION_START_FAILED.length));
                __privateMethod(this, _Boundary_instances, hydrate_failed_content_fn).call(this, serialized_error);
              } else if (server_rendered_pending) {
                __privateMethod(this, _Boundary_instances, hydrate_pending_content_fn).call(this);
              } else {
                __privateMethod(this, _Boundary_instances, hydrate_resolved_content_fn).call(this);
              }
            } else {
              __privateMethod(this, _Boundary_instances, render_fn).call(this);
            }
          }, flags));
          if (hydrating) {
            __privateSet(this, _anchor, hydrate_node);
          }
        }
        /**
         * Defer an effect inside a pending boundary until the boundary resolves
         * @param {Effect} effect
         */
        defer_effect(effect2) {
          defer_effect(effect2, __privateGet(this, _dirty_effects2), __privateGet(this, _maybe_dirty_effects2));
        }
        /**
         * Returns `false` if the effect exists inside a boundary whose pending snippet is shown
         * @returns {boolean}
         */
        is_rendered() {
          return !this.is_pending && (!this.parent || this.parent.is_rendered());
        }
        has_pending_snippet() {
          return !!__privateGet(this, _props).pending;
        }
        /**
         * Update the source that powers `$effect.pending()` inside this boundary,
         * and controls when the current `pending` snippet (if any) is removed.
         * Do not call from inside the class
         * @param {1 | -1} d
         * @param {Batch} batch
         */
        update_pending_count(d, batch) {
          __privateMethod(this, _Boundary_instances, update_pending_count_fn).call(this, d, batch);
          __privateSet(this, _local_pending_count, __privateGet(this, _local_pending_count) + d);
          if (!__privateGet(this, _effect_pending) || __privateGet(this, _pending_count_update_queued)) return;
          __privateSet(this, _pending_count_update_queued, true);
          queue_micro_task(() => {
            __privateSet(this, _pending_count_update_queued, false);
            if (__privateGet(this, _effect_pending)) {
              internal_set(__privateGet(this, _effect_pending), __privateGet(this, _local_pending_count));
            }
          });
        }
        get_effect_pending() {
          __privateGet(this, _effect_pending_subscriber).call(this);
          return get2(
            /** @type {Source<number>} */
            __privateGet(this, _effect_pending)
          );
        }
        /** @param {unknown} error */
        error(error) {
          if (!__privateGet(this, _props).onerror && !__privateGet(this, _props).failed) {
            throw error;
          }
          if (current_batch?.is_fork) {
            if (__privateGet(this, _main_effect)) current_batch.skip_effect(__privateGet(this, _main_effect));
            if (__privateGet(this, _pending_effect)) current_batch.skip_effect(__privateGet(this, _pending_effect));
            if (__privateGet(this, _failed_effect)) current_batch.skip_effect(__privateGet(this, _failed_effect));
            current_batch.oncommit(() => {
              __privateMethod(this, _Boundary_instances, handle_error_fn).call(this, error);
            });
          } else {
            __privateMethod(this, _Boundary_instances, handle_error_fn).call(this, error);
          }
        }
      };
      _anchor = new WeakMap();
      _hydrate_open = new WeakMap();
      _props = new WeakMap();
      _children = new WeakMap();
      _effect = new WeakMap();
      _main_effect = new WeakMap();
      _pending_effect = new WeakMap();
      _failed_effect = new WeakMap();
      _offscreen_fragment = new WeakMap();
      _local_pending_count = new WeakMap();
      _pending_count = new WeakMap();
      _pending_count_update_queued = new WeakMap();
      _dirty_effects2 = new WeakMap();
      _maybe_dirty_effects2 = new WeakMap();
      _effect_pending = new WeakMap();
      _effect_pending_subscriber = new WeakMap();
      _Boundary_instances = new WeakSet();
      hydrate_resolved_content_fn = function() {
        try {
          __privateSet(this, _main_effect, branch(() => __privateGet(this, _children).call(this, __privateGet(this, _anchor))));
        } catch (error) {
          this.error(error);
        }
      };
      /**
       * @param {unknown} error The deserialized error from the server's hydration comment
       */
      hydrate_failed_content_fn = function(error) {
        const failed = __privateGet(this, _props).failed;
        const { reset: reset2, invoke_onerror } = __privateMethod(this, _Boundary_instances, create_reset_fn).call(this, error);
        queue_micro_task(invoke_onerror);
        if (!failed) return;
        __privateSet(this, _failed_effect, branch(() => {
          failed(
            __privateGet(this, _anchor),
            () => error,
            () => reset2
          );
        }));
      };
      /**
       * Creates the `reset` function for a failed boundary, along with a function
       * that invokes `onerror` with it (if provided)
       * @param {unknown} error
       * @returns {{ reset: () => void, invoke_onerror: () => void }}
       */
      create_reset_fn = function(error) {
        var did_reset = false;
        var calling_on_error = false;
        const reset2 = () => {
          if (did_reset) {
            svelte_boundary_reset_noop();
            return;
          }
          did_reset = true;
          if (calling_on_error) {
            svelte_boundary_reset_onerror();
          }
          if (__privateGet(this, _failed_effect) !== null) {
            pause_effect(__privateGet(this, _failed_effect), () => {
              __privateSet(this, _failed_effect, null);
            });
          }
          __privateMethod(this, _Boundary_instances, run_fn).call(this, () => {
            __privateMethod(this, _Boundary_instances, render_fn).call(this);
          });
        };
        const invoke_onerror = () => {
          try {
            calling_on_error = true;
            __privateGet(this, _props).onerror?.(error, reset2);
            calling_on_error = false;
          } catch (err) {
            invoke_error_boundary(err, __privateGet(this, _effect) && __privateGet(this, _effect).parent);
          }
        };
        return { reset: reset2, invoke_onerror };
      };
      hydrate_pending_content_fn = function() {
        const pending2 = __privateGet(this, _props).pending;
        if (!pending2) return;
        this.is_pending = true;
        __privateSet(this, _pending_effect, branch(() => pending2(__privateGet(this, _anchor))));
        queue_micro_task(() => {
          var fragment = __privateSet(this, _offscreen_fragment, document.createDocumentFragment());
          var anchor = create_text();
          var handled = false;
          fragment.append(anchor);
          __privateSet(this, _main_effect, __privateMethod(this, _Boundary_instances, run_fn).call(this, () => {
            try {
              return branch(() => __privateGet(this, _children).call(this, anchor));
            } catch (error) {
              try {
                this.error(error);
                handled = true;
              } catch (error2) {
                invoke_error_boundary(error2, __privateGet(this, _effect).parent);
              }
              return null;
            }
          }));
          if (__privateGet(this, _main_effect) === null) {
            __privateSet(this, _offscreen_fragment, null);
            if (handled) __privateMethod(this, _Boundary_instances, resolve_fn).call(
              this,
              /** @type {Batch} */
              current_batch
            );
            return;
          }
          if (__privateGet(this, _pending_count) === 0) {
            __privateGet(this, _anchor).before(fragment);
            __privateSet(this, _offscreen_fragment, null);
            pause_effect(
              /** @type {Effect} */
              __privateGet(this, _pending_effect),
              () => {
                __privateSet(this, _pending_effect, null);
              }
            );
            __privateMethod(this, _Boundary_instances, resolve_fn).call(
              this,
              /** @type {Batch} */
              current_batch
            );
          }
        });
      };
      render_fn = function() {
        try {
          this.is_pending = this.has_pending_snippet();
          __privateSet(this, _pending_count, 0);
          __privateSet(this, _local_pending_count, 0);
          __privateSet(this, _main_effect, branch(() => {
            __privateGet(this, _children).call(this, __privateGet(this, _anchor));
          }));
          if (__privateGet(this, _pending_count) > 0) {
            var fragment = __privateSet(this, _offscreen_fragment, document.createDocumentFragment());
            move_effect(__privateGet(this, _main_effect), fragment);
            const pending2 = (
              /** @type {(anchor: Node) => void} */
              __privateGet(this, _props).pending
            );
            __privateSet(this, _pending_effect, branch(() => pending2(__privateGet(this, _anchor))));
          } else {
            __privateMethod(this, _Boundary_instances, resolve_fn).call(
              this,
              /** @type {Batch} */
              current_batch
            );
          }
        } catch (error) {
          this.error(error);
        }
      };
      /**
       * @param {Batch} batch
       */
      resolve_fn = function(batch) {
        this.is_pending = false;
        batch.transfer_effects(__privateGet(this, _dirty_effects2), __privateGet(this, _maybe_dirty_effects2));
      };
      /**
       * @template T
       * @param {() => T} fn
       */
      run_fn = function(fn) {
        var previous_effect = active_effect;
        var previous_reaction = active_reaction;
        var previous_ctx = component_context;
        set_active_effect(__privateGet(this, _effect));
        set_active_reaction(__privateGet(this, _effect));
        set_component_context(__privateGet(this, _effect).ctx);
        try {
          Batch.ensure();
          return fn();
        } finally {
          set_active_effect(previous_effect);
          set_active_reaction(previous_reaction);
          set_component_context(previous_ctx);
        }
      };
      /**
       * Updates the pending count associated with the currently visible pending snippet,
       * if any, such that we can replace the snippet with content once work is done
       * @param {1 | -1} d
       * @param {Batch} batch
       */
      update_pending_count_fn = function(d, batch) {
        var _a2;
        if (!this.has_pending_snippet()) {
          if (this.parent) {
            __privateMethod(_a2 = this.parent, _Boundary_instances, update_pending_count_fn).call(_a2, d, batch);
          }
          return;
        }
        __privateSet(this, _pending_count, __privateGet(this, _pending_count) + d);
        if (__privateGet(this, _pending_count) === 0) {
          __privateMethod(this, _Boundary_instances, resolve_fn).call(this, batch);
          if (__privateGet(this, _pending_effect)) {
            pause_effect(__privateGet(this, _pending_effect), () => {
              __privateSet(this, _pending_effect, null);
            });
          }
          if (__privateGet(this, _offscreen_fragment)) {
            __privateGet(this, _anchor).before(__privateGet(this, _offscreen_fragment));
            __privateSet(this, _offscreen_fragment, null);
          }
        }
      };
      /**
       * @param {unknown} error
       */
      handle_error_fn = function(error) {
        if (__privateGet(this, _main_effect)) {
          destroy_effect(__privateGet(this, _main_effect));
          __privateSet(this, _main_effect, null);
        }
        if (__privateGet(this, _pending_effect)) {
          destroy_effect(__privateGet(this, _pending_effect));
          __privateSet(this, _pending_effect, null);
        }
        if (__privateGet(this, _failed_effect)) {
          destroy_effect(__privateGet(this, _failed_effect));
          __privateSet(this, _failed_effect, null);
        }
        if (hydrating) {
          set_hydrate_node(
            /** @type {TemplateNode} */
            __privateGet(this, _hydrate_open)
          );
          next();
          set_hydrate_node(skip_nodes());
        }
        let failed = __privateGet(this, _props).failed;
        const handle_error_result = (transformed_error) => {
          const { reset: reset2, invoke_onerror } = __privateMethod(this, _Boundary_instances, create_reset_fn).call(this, transformed_error);
          invoke_onerror();
          if (failed) {
            __privateSet(this, _failed_effect, __privateMethod(this, _Boundary_instances, run_fn).call(this, () => {
              try {
                return branch(() => {
                  var effect2 = (
                    /** @type {Effect} */
                    active_effect
                  );
                  effect2.b = this;
                  effect2.f |= BOUNDARY_EFFECT;
                  failed(
                    __privateGet(this, _anchor),
                    () => transformed_error,
                    () => reset2
                  );
                });
              } catch (error2) {
                invoke_error_boundary(
                  error2,
                  /** @type {Effect} */
                  __privateGet(this, _effect).parent
                );
                return null;
              }
            }));
          }
        };
        queue_micro_task(() => {
          var result;
          try {
            result = this.transform_error(error);
          } catch (e) {
            invoke_error_boundary(e, __privateGet(this, _effect) && __privateGet(this, _effect).parent);
            return;
          }
          if (result !== null && typeof result === "object" && typeof /** @type {any} */
          result.then === "function") {
            result.then(
              handle_error_result,
              /** @param {unknown} e */
              (e) => invoke_error_boundary(e, __privateGet(this, _effect) && __privateGet(this, _effect).parent)
            );
          } else {
            handle_error_result(result);
          }
        });
      };
    }
  });

  // node_modules/svelte/src/internal/client/render.js
  function set_text(text2, value) {
    var _a2;
    var str = value == null ? "" : typeof value === "object" ? `${value}` : value;
    if (str !== /** @type {any} */
    (text2[_a2 = TEXT_CACHE] ?? (text2[_a2] = text2.nodeValue))) {
      text2[TEXT_CACHE] = str;
      text2.nodeValue = `${str}`;
    }
  }
  function mount(component2, options) {
    return _mount(component2, options);
  }
  function hydrate(component2, options) {
    init_operations2();
    options.intro = options.intro ?? false;
    const target = options.target;
    const was_hydrating = hydrating;
    const previous_hydrate_node = hydrate_node;
    try {
      var anchor = get_first_child(target);
      while (anchor && (anchor.nodeType !== COMMENT_NODE || /** @type {Comment} */
      anchor.data !== HYDRATION_START)) {
        anchor = get_next_sibling(anchor);
      }
      if (!anchor) {
        throw HYDRATION_ERROR;
      }
      set_hydrating(true);
      set_hydrate_node(
        /** @type {Comment} */
        anchor
      );
      const instance = _mount(component2, { ...options, anchor });
      set_hydrating(false);
      return (
        /**  @type {Exports} */
        instance
      );
    } catch (error) {
      if (error instanceof Error && error.message.split("\n").some((line) => line.startsWith("https://svelte.dev/e/"))) {
        throw error;
      }
      if (error !== HYDRATION_ERROR) {
        console.warn("Failed to hydrate: ", error);
      }
      if (options.recover === false) {
        hydration_failed();
      }
      init_operations2();
      clear_text_content(target);
      set_hydrating(false);
      return mount(component2, options);
    } finally {
      set_hydrating(was_hydrating);
      set_hydrate_node(previous_hydrate_node);
    }
  }
  function _mount(Component, { target, anchor, props = {}, events, context, intro = true, transformError }) {
    init_operations2();
    var component2 = void 0;
    var unmount2 = component_root(() => {
      var anchor_node = anchor ?? target.appendChild(create_text());
      boundary(
        /** @type {TemplateNode} */
        anchor_node,
        {
          pending: () => {
          }
        },
        (anchor_node2) => {
          push({});
          var ctx = (
            /** @type {ComponentContext} */
            component_context
          );
          if (context) ctx.c = context;
          if (events) {
            props.$$events = events;
          }
          if (hydrating) {
            assign_nodes(
              /** @type {TemplateNode} */
              anchor_node2,
              null
            );
          }
          should_intro = intro;
          component2 = Component(anchor_node2, props) || mark_as_component();
          should_intro = true;
          if (hydrating) {
            active_effect.nodes.end = hydrate_node;
            if (hydrate_node === null || hydrate_node.nodeType !== COMMENT_NODE || /** @type {Comment} */
            hydrate_node.data !== HYDRATION_END) {
              hydration_mismatch();
              throw HYDRATION_ERROR;
            }
          }
          pop();
        },
        transformError
      );
      var registered_events = /* @__PURE__ */ new Set();
      var event_handle = (events2) => {
        for (var i = 0; i < events2.length; i++) {
          var event_name = events2[i];
          if (registered_events.has(event_name)) continue;
          registered_events.add(event_name);
          var passive2 = is_passive_event(event_name);
          for (const node of [target, document]) {
            var counts = listeners.get(node);
            if (counts === void 0) {
              counts = /* @__PURE__ */ new Map();
              listeners.set(node, counts);
            }
            var count = counts.get(event_name);
            if (count === void 0) {
              node.addEventListener(event_name, handle_event_propagation, { passive: passive2 });
              counts.set(event_name, 1);
            } else {
              counts.set(event_name, count + 1);
            }
          }
        }
      };
      event_handle(array_from(all_registered_events));
      root_event_handles.add(event_handle);
      return () => {
        for (var event_name of registered_events) {
          for (const node of [target, document]) {
            var counts = (
              /** @type {Map<string, number>} */
              listeners.get(node)
            );
            var count = (
              /** @type {number} */
              counts.get(event_name)
            );
            if (--count == 0) {
              node.removeEventListener(event_name, handle_event_propagation);
              counts.delete(event_name);
              if (counts.size === 0) {
                listeners.delete(node);
              }
            } else {
              counts.set(event_name, count);
            }
          }
        }
        root_event_handles.delete(event_handle);
        if (anchor_node !== anchor) {
          anchor_node.parentNode?.removeChild(anchor_node);
        }
      };
    });
    mounted_components.set(component2, unmount2);
    return component2;
  }
  function unmount(component2, options) {
    const fn = mounted_components.get(component2);
    if (fn) {
      mounted_components.delete(component2);
      return fn(options);
    }
    if (dev_fallback_default) {
      lifecycle_double_unmount();
    }
    return Promise.resolve();
  }
  var should_intro, listeners, mounted_components;
  var init_render = __esm({
    "node_modules/svelte/src/internal/client/render.js"() {
      init_esm_env();
      init_operations();
      init_constants2();
      init_runtime();
      init_context2();
      init_effects();
      init_hydration();
      init_utils();
      init_events();
      init_warnings();
      init_errors2();
      init_template();
      init_utils4();
      init_constants();
      init_boundary();
      should_intro = true;
      listeners = /* @__PURE__ */ new Map();
      mounted_components = /* @__PURE__ */ new WeakMap();
    }
  });

  // node_modules/svelte/src/internal/client/dev/hmr.js
  var init_hmr = __esm({
    "node_modules/svelte/src/internal/client/dev/hmr.js"() {
      init_constants2();
      init_constants();
      init_hydration();
      init_effects();
      init_sources();
      init_render();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dev/ownership.js
  var init_ownership = __esm({
    "node_modules/svelte/src/internal/client/dev/ownership.js"() {
      init_utils();
      init_constants();
      init_constants2();
      init_context2();
      init_warnings();
      init_utils4();
    }
  });

  // node_modules/svelte/src/internal/client/dev/legacy.js
  var init_legacy2 = __esm({
    "node_modules/svelte/src/internal/client/dev/legacy.js"() {
      init_errors2();
      init_context2();
      init_constants2();
    }
  });

  // node_modules/svelte/src/internal/client/dev/inspect.js
  var init_inspect = __esm({
    "node_modules/svelte/src/internal/client/dev/inspect.js"() {
      init_constants2();
      init_clone();
      init_effects();
      init_runtime();
      init_dev();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/async.js
  var init_async2 = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/async.js"() {
      init_async();
      init_runtime();
      init_hydration();
      init_template();
    }
  });

  // node_modules/svelte/src/internal/client/dev/validation.js
  var init_validation = __esm({
    "node_modules/svelte/src/internal/client/dev/validation.js"() {
      init_errors2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/branches.js
  var init_branches = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/branches.js"() {
      init_batch();
      init_effects();
      init_constants();
      init_hydration();
      init_operations();
      init_esm_env();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/await.js
  var init_await = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/await.js"() {
      init_utils();
      init_effects();
      init_sources();
      init_hydration();
      init_task();
      init_constants2();
      init_context2();
      init_batch();
      init_branches();
      init_async();
      init_esm_env();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/if.js
  var init_if = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/if.js"() {
      init_constants();
      init_hydration();
      init_effects();
      init_branches();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/key.js
  var init_key = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/key.js"() {
      init_context2();
      init_effects();
      init_hydration();
      init_branches();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/css-props.js
  var init_css_props = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/css-props.js"() {
      init_effects();
      init_hydration();
      init_operations();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/each.js
  function pause_effects(state2, to_destroy, controlled_anchor) {
    var transitions = [];
    var length = to_destroy.length;
    var group;
    var remaining = to_destroy.length;
    for (var i = 0; i < length; i++) {
      let effect2 = to_destroy[i];
      pause_effect(
        effect2,
        () => {
          if (group) {
            group.pending.delete(effect2);
            group.done.add(effect2);
            if (group.pending.size === 0) {
              var groups = (
                /** @type {Set<EachOutroGroup>} */
                state2.outrogroups
              );
              destroy_effects(state2, array_from(group.done));
              groups.delete(group);
              if (groups.size === 0) {
                state2.outrogroups = null;
              }
            }
          } else {
            remaining -= 1;
          }
        },
        false
      );
    }
    if (remaining === 0) {
      var fast_path = transitions.length === 0 && controlled_anchor !== null && state2.pending.size === 0;
      if (fast_path) {
        var anchor = (
          /** @type {Element} */
          controlled_anchor
        );
        var parent_node = (
          /** @type {Element} */
          anchor.parentNode
        );
        clear_text_content(parent_node);
        parent_node.append(anchor);
        state2.items.clear();
      }
      destroy_effects(state2, to_destroy, !fast_path);
    } else {
      group = {
        pending: new Set(to_destroy),
        done: /* @__PURE__ */ new Set()
      };
      (state2.outrogroups ?? (state2.outrogroups = /* @__PURE__ */ new Set())).add(group);
    }
  }
  function destroy_effects(state2, to_destroy, remove_dom = true) {
    var preserved_effects;
    if (state2.pending.size > 0) {
      preserved_effects = /* @__PURE__ */ new Set();
      for (const keys of state2.pending.values()) {
        for (const key2 of keys) {
          preserved_effects.add(
            /** @type {EachItem} */
            state2.items.get(key2).e
          );
        }
      }
    }
    for (var i = 0; i < to_destroy.length; i++) {
      var e = to_destroy[i];
      if (preserved_effects?.has(e)) {
        e.f |= EFFECT_OFFSCREEN;
        const fragment = document.createDocumentFragment();
        move_effect(e, fragment);
      } else {
        destroy_effect(to_destroy[i], remove_dom);
      }
    }
  }
  function each(node, flags2, get_collection, get_key, render_fn2, fallback_fn = null) {
    var anchor = node;
    var items = /* @__PURE__ */ new Map();
    var is_controlled = (flags2 & EACH_IS_CONTROLLED) !== 0;
    if (is_controlled) {
      var parent_node = (
        /** @type {Element} */
        node
      );
      anchor = hydrating ? set_hydrate_node(get_first_child(parent_node)) : parent_node.appendChild(create_text());
    }
    if (hydrating) {
      hydrate_next();
    }
    var fallback2 = null;
    var each_array = derived_safe_equal(() => {
      var collection = get_collection();
      return (
        /** @type {V[]} */
        is_array(collection) ? collection : collection == null ? [] : array_from(collection)
      );
    });
    if (dev_fallback_default) {
      tag(each_array, "{#each ...}");
    }
    var array;
    var pending2 = /* @__PURE__ */ new Map();
    var first_run = true;
    function commit(batch) {
      if ((state2.effect.f & DESTROYED) !== 0) {
        return;
      }
      state2.pending.delete(batch);
      state2.fallback = fallback2;
      reconcile(state2, array, anchor, flags2, get_key);
      if (fallback2 !== null) {
        if (array.length === 0) {
          if ((fallback2.f & EFFECT_OFFSCREEN) === 0) {
            resume_effect(fallback2);
          } else {
            fallback2.f ^= EFFECT_OFFSCREEN;
            move(fallback2, null, anchor);
          }
        } else {
          pause_effect(fallback2, () => {
            fallback2 = null;
          });
        }
      }
    }
    function discard(batch) {
      state2.pending.delete(batch);
    }
    var effect2 = block(() => {
      array = /** @type {V[]} */
      get2(each_array);
      var length = array.length;
      let mismatch = false;
      if (hydrating) {
        var is_else = read_hydration_instruction(anchor) === HYDRATION_START_ELSE;
        if (is_else !== (length === 0)) {
          anchor = skip_nodes();
          set_hydrate_node(anchor);
          set_hydrating(false);
          mismatch = true;
        }
      }
      var keys = /* @__PURE__ */ new Set();
      var batch = (
        /** @type {Batch} */
        current_batch
      );
      var defer = should_defer_append();
      for (var index2 = 0; index2 < length; index2 += 1) {
        if (hydrating && hydrate_node.nodeType === COMMENT_NODE && /** @type {Comment} */
        hydrate_node.data === HYDRATION_END) {
          anchor = /** @type {Comment} */
          hydrate_node;
          mismatch = true;
          set_hydrating(false);
        }
        var value = array[index2];
        var key2 = get_key(value, index2);
        if (dev_fallback_default) {
          var key_again = get_key(value, index2);
          if (key2 !== key_again) {
            each_key_volatile(String(index2), String(key2), String(key_again));
          }
        }
        var item = first_run ? null : items.get(key2);
        if (item) {
          if (item.v) internal_set(item.v, value);
          if (item.i) internal_set(item.i, index2);
          if (defer) {
            batch.unskip_effect(item.e);
          }
        } else {
          item = create_item(
            items,
            first_run ? anchor : offscreen_anchor ?? (offscreen_anchor = create_text()),
            value,
            key2,
            index2,
            render_fn2,
            flags2,
            get_collection
          );
          if (!first_run) {
            item.e.f |= EFFECT_OFFSCREEN;
          }
          items.set(key2, item);
        }
        keys.add(key2);
      }
      if (length === 0 && fallback_fn && !fallback2) {
        if (first_run) {
          fallback2 = branch(() => fallback_fn(anchor));
        } else {
          fallback2 = branch(() => fallback_fn(offscreen_anchor ?? (offscreen_anchor = create_text())));
          fallback2.f |= EFFECT_OFFSCREEN;
        }
      }
      if (length > keys.size) {
        if (dev_fallback_default) {
          validate_each_keys(array, get_key);
        } else {
          each_key_duplicate("", "", "");
        }
      }
      if (hydrating && length > 0) {
        set_hydrate_node(skip_nodes());
      }
      if (!first_run) {
        pending2.set(batch, keys);
        if (defer) {
          for (const [key3, item2] of items) {
            if (!keys.has(key3)) {
              batch.skip_effect(item2.e);
            }
          }
          batch.oncommit(commit);
          batch.ondiscard(discard);
        } else {
          commit(batch);
        }
      }
      if (mismatch) {
        set_hydrating(true);
      }
      get2(each_array);
    });
    var state2 = { effect: effect2, flags: flags2, items, pending: pending2, outrogroups: null, fallback: fallback2 };
    first_run = false;
    if (hydrating) {
      anchor = hydrate_node;
    }
  }
  function skip_to_branch(effect2) {
    while (effect2 !== null && (effect2.f & BRANCH_EFFECT) === 0) {
      effect2 = effect2.next;
    }
    return effect2;
  }
  function reconcile(state2, array, anchor, flags2, get_key) {
    var is_animated = (flags2 & EACH_IS_ANIMATED) !== 0;
    var length = array.length;
    var items = state2.items;
    var current = skip_to_branch(state2.effect.first);
    var seen;
    var prev = null;
    var to_animate;
    var matched = [];
    var stashed = [];
    var value;
    var key2;
    var effect2;
    var i;
    if (is_animated) {
      for (i = 0; i < length; i += 1) {
        value = array[i];
        key2 = get_key(value, i);
        effect2 = /** @type {EachItem} */
        items.get(key2).e;
        if ((effect2.f & EFFECT_OFFSCREEN) === 0) {
          effect2.nodes?.a?.measure();
          (to_animate ?? (to_animate = /* @__PURE__ */ new Set())).add(effect2);
        }
      }
    }
    for (i = 0; i < length; i += 1) {
      value = array[i];
      key2 = get_key(value, i);
      effect2 = /** @type {EachItem} */
      items.get(key2).e;
      if (state2.outrogroups !== null) {
        for (const group of state2.outrogroups) {
          group.pending.delete(effect2);
          group.done.delete(effect2);
        }
      }
      if ((effect2.f & INERT) !== 0) {
        resume_effect(effect2);
        if (is_animated) {
          effect2.nodes?.a?.unfix();
          (to_animate ?? (to_animate = /* @__PURE__ */ new Set())).delete(effect2);
        }
      }
      if ((effect2.f & EFFECT_OFFSCREEN) !== 0) {
        effect2.f ^= EFFECT_OFFSCREEN;
        if (effect2 === current) {
          move(effect2, null, anchor);
        } else {
          var next2 = prev ? prev.next : current;
          if (effect2 === state2.effect.last) {
            state2.effect.last = effect2.prev;
          }
          if (effect2.prev) effect2.prev.next = effect2.next;
          if (effect2.next) effect2.next.prev = effect2.prev;
          link(state2, prev, effect2);
          link(state2, effect2, next2);
          move(effect2, next2, anchor);
          prev = effect2;
          matched = [];
          stashed = [];
          current = skip_to_branch(prev.next);
          continue;
        }
      }
      if (effect2 !== current) {
        if (seen !== void 0 && seen.has(effect2)) {
          if (matched.length < stashed.length) {
            var start = stashed[0];
            var j;
            prev = start.prev;
            var a = matched[0];
            var b = matched[matched.length - 1];
            for (j = 0; j < matched.length; j += 1) {
              move(matched[j], start, anchor);
            }
            for (j = 0; j < stashed.length; j += 1) {
              seen.delete(stashed[j]);
            }
            link(state2, a.prev, b.next);
            link(state2, prev, a);
            link(state2, b, start);
            current = start;
            prev = b;
            i -= 1;
            matched = [];
            stashed = [];
          } else {
            seen.delete(effect2);
            move(effect2, current, anchor);
            link(state2, effect2.prev, effect2.next);
            link(state2, effect2, prev === null ? state2.effect.first : prev.next);
            link(state2, prev, effect2);
            prev = effect2;
          }
          continue;
        }
        matched = [];
        stashed = [];
        while (current !== null && current !== effect2) {
          (seen ?? (seen = /* @__PURE__ */ new Set())).add(current);
          stashed.push(current);
          current = skip_to_branch(current.next);
        }
        if (current === null) {
          continue;
        }
      }
      if ((effect2.f & EFFECT_OFFSCREEN) === 0) {
        matched.push(effect2);
      }
      prev = effect2;
      current = skip_to_branch(effect2.next);
    }
    if (state2.outrogroups !== null) {
      for (const group of state2.outrogroups) {
        if (group.pending.size === 0) {
          destroy_effects(state2, array_from(group.done));
          state2.outrogroups?.delete(group);
        }
      }
      if (state2.outrogroups.size === 0) {
        state2.outrogroups = null;
      }
    }
    if (current !== null || seen !== void 0) {
      var to_destroy = [];
      if (seen !== void 0) {
        for (effect2 of seen) {
          if ((effect2.f & INERT) === 0) {
            to_destroy.push(effect2);
          }
        }
      }
      while (current !== null) {
        if ((current.f & INERT) === 0 && current !== state2.fallback) {
          to_destroy.push(current);
        }
        current = skip_to_branch(current.next);
      }
      var destroy_length = to_destroy.length;
      if (destroy_length > 0) {
        var controlled_anchor = (flags2 & EACH_IS_CONTROLLED) !== 0 && length === 0 ? anchor : null;
        if (is_animated) {
          for (i = 0; i < destroy_length; i += 1) {
            to_destroy[i].nodes?.a?.measure();
          }
          for (i = 0; i < destroy_length; i += 1) {
            to_destroy[i].nodes?.a?.fix();
          }
        }
        pause_effects(state2, to_destroy, controlled_anchor);
      }
    }
    if (is_animated) {
      queue_micro_task(() => {
        if (to_animate === void 0) return;
        for (effect2 of to_animate) {
          effect2.nodes?.a?.apply();
        }
      });
    }
  }
  function create_item(items, anchor, value, key2, index2, render_fn2, flags2, get_collection) {
    var v = (flags2 & EACH_ITEM_REACTIVE) !== 0 ? (flags2 & EACH_ITEM_IMMUTABLE) === 0 ? mutable_source(value, false, false) : source(value) : null;
    var i = (flags2 & EACH_INDEX_REACTIVE) !== 0 ? source(index2) : null;
    if (dev_fallback_default && v) {
      v.trace = () => {
        get_collection()[i?.v ?? index2];
      };
    }
    return {
      v,
      i,
      e: branch(() => {
        render_fn2(anchor, v ?? value, i ?? index2, get_collection);
        return () => {
          items.delete(key2);
        };
      })
    };
  }
  function move(effect2, next2, anchor) {
    if (!effect2.nodes) return;
    var node = effect2.nodes.start;
    var end = effect2.nodes.end;
    var dest = next2 && (next2.f & EFFECT_OFFSCREEN) === 0 ? (
      /** @type {EffectNodes} */
      next2.nodes.start
    ) : anchor;
    while (node !== null) {
      var next_node = (
        /** @type {TemplateNode} */
        get_next_sibling(node)
      );
      dest.before(node);
      if (node === end) {
        return;
      }
      node = next_node;
    }
  }
  function link(state2, prev, next2) {
    if (prev === null) {
      state2.effect.first = next2;
    } else {
      prev.next = next2;
    }
    if (next2 === null) {
      state2.effect.last = prev;
    } else {
      next2.prev = prev;
    }
  }
  function validate_each_keys(array, key_fn) {
    const keys = /* @__PURE__ */ new Map();
    const length = array.length;
    for (let i = 0; i < length; i++) {
      const key2 = key_fn(array[i], i);
      if (keys.has(key2)) {
        const a = String(keys.get(key2));
        const b = String(i);
        let k = String(key2);
        if (k.startsWith("[object ")) k = null;
        each_key_duplicate(a, b, k);
      }
      keys.set(key2, i);
    }
  }
  var offscreen_anchor;
  var init_each = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/each.js"() {
      init_constants2();
      init_hydration();
      init_operations();
      init_effects();
      init_sources();
      init_utils();
      init_constants();
      init_task();
      init_runtime();
      init_esm_env();
      init_deriveds();
      init_batch();
      init_errors2();
      init_tracing();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/html.js
  var init_html = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/html.js"() {
      init_constants2();
      init_effects();
      init_hydration();
      init_template();
      init_warnings();
      init_utils4();
      init_esm_env();
      init_context2();
      init_operations();
      init_runtime();
      init_constants();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/slot.js
  var init_slot = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/slot.js"() {
      init_hydration();
      init_operations();
      init_template();
    }
  });

  // node_modules/svelte/src/internal/shared/validate.js
  var init_validate = __esm({
    "node_modules/svelte/src/internal/shared/validate.js"() {
      init_utils4();
      init_warnings2();
      init_errors();
      init_errors();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/snippet.js
  var init_snippet = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/snippet.js"() {
      init_constants();
      init_effects();
      init_context2();
      init_hydration();
      init_reconciler();
      init_template();
      init_warnings();
      init_errors2();
      init_esm_env();
      init_operations();
      init_validate();
      init_branches();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/svelte-component.js
  var init_svelte_component = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/svelte-component.js"() {
      init_constants();
      init_effects();
      init_hydration();
      init_branches();
      init_constants2();
    }
  });

  // node_modules/svelte/src/internal/client/timing.js
  var init_timing = __esm({
    "node_modules/svelte/src/internal/client/timing.js"() {
      init_utils();
      init_esm_env();
    }
  });

  // node_modules/svelte/src/internal/client/loop.js
  var init_loop = __esm({
    "node_modules/svelte/src/internal/client/loop.js"() {
      init_timing();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/transitions.js
  var init_transitions = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/transitions.js"() {
      init_utils();
      init_effects();
      init_runtime();
      init_loop();
      init_render();
      init_constants2();
      init_constants();
      init_task();
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/svelte-element.js
  var init_svelte_element = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/svelte-element.js"() {
      init_constants2();
      init_hydration();
      init_operations();
      init_effects();
      init_render();
      init_runtime();
      init_context2();
      init_esm_env();
      init_constants();
      init_template();
      init_utils4();
      init_branches();
      init_transitions();
    }
  });

  // node_modules/svelte/src/internal/client/dom/blocks/svelte-head.js
  var init_svelte_head = __esm({
    "node_modules/svelte/src/internal/client/dom/blocks/svelte-head.js"() {
      init_hydration();
      init_operations();
      init_effects();
      init_constants();
    }
  });

  // node_modules/svelte/src/internal/client/dom/css.js
  var init_css2 = __esm({
    "node_modules/svelte/src/internal/client/dom/css.js"() {
      init_esm_env();
      init_css();
      init_effects();
      init_operations();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/actions.js
  var init_actions = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/actions.js"() {
      init_effects();
      init_equality();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/attachments.js
  var init_attachments2 = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/attachments.js"() {
      init_effects();
    }
  });

  // node_modules/svelte/src/escaping.js
  var init_escaping = __esm({
    "node_modules/svelte/src/escaping.js"() {
    }
  });

  // node_modules/clsx/dist/clsx.mjs
  var init_clsx = __esm({
    "node_modules/clsx/dist/clsx.mjs"() {
    }
  });

  // node_modules/svelte/src/internal/shared/attributes.js
  function to_class(value, hash2, directives) {
    var classname = value == null ? "" : "" + value;
    if (hash2) {
      classname = classname ? classname + " " + hash2 : hash2;
    }
    if (directives) {
      for (var key2 of Object.keys(directives)) {
        if (directives[key2]) {
          classname = classname ? classname + " " + key2 : key2;
        } else if (classname.length) {
          var len = key2.length;
          var a = 0;
          while ((a = classname.indexOf(key2, a)) >= 0) {
            var b = a + len;
            if ((a === 0 || whitespace.includes(classname[a - 1])) && (b === classname.length || whitespace.includes(classname[b]))) {
              classname = (a === 0 ? "" : classname.substring(0, a)) + classname.substring(b + 1);
            } else {
              a = b;
            }
          }
        }
      }
    }
    return classname === "" ? null : classname;
  }
  var whitespace;
  var init_attributes = __esm({
    "node_modules/svelte/src/internal/shared/attributes.js"() {
      init_escaping();
      init_clsx();
      init_utils();
      whitespace = [..." 	\n\r\f\xA0\v\uFEFF"];
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/class.js
  function set_class(dom, is_html, value, hash2, prev_classes, next_classes) {
    var prev = (
      /** @type {any} */
      dom[CLASS_CACHE]
    );
    if (hydrating || prev !== value || prev === void 0) {
      var next_class_name = to_class(value, hash2, next_classes);
      if (!hydrating || next_class_name !== dom.getAttribute("class")) {
        if (next_class_name == null) {
          dom.removeAttribute("class");
        } else if (is_html) {
          dom.className = next_class_name;
        } else {
          dom.setAttribute("class", next_class_name);
        }
      }
      dom[CLASS_CACHE] = value;
    } else if (next_classes && prev_classes !== next_classes) {
      for (var key2 in next_classes) {
        var is_present = !!next_classes[key2];
        if (prev_classes == null || is_present !== !!prev_classes[key2]) {
          dom.classList.toggle(key2, is_present);
        }
      }
    }
    return next_classes;
  }
  var init_class = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/class.js"() {
      init_attributes();
      init_constants();
      init_hydration();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/style.js
  var init_style = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/style.js"() {
      init_attributes();
      init_constants();
      init_hydration();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/select.js
  var init_select = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/select.js"() {
      init_effects();
      init_shared2();
      init_proxy();
      init_utils();
      init_warnings();
      init_batch();
      init_flags();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/attributes.js
  function remove_input_defaults(input) {
    if (!hydrating) return;
    var already_removed = false;
    var remove_defaults = () => {
      if (already_removed) return;
      already_removed = true;
      if (input.hasAttribute("value")) {
        var value = input.value;
        set_attribute2(input, "value", null);
        input.value = value;
      }
      if (input.hasAttribute("checked")) {
        var checked = input.checked;
        set_attribute2(input, "checked", null);
        input.checked = checked;
      }
    };
    input[FORM_RESET_HANDLER] = remove_defaults;
    queue_micro_task(remove_defaults);
    add_form_reset_listener();
  }
  function set_attribute2(element2, attribute, value, skip_warning) {
    var attributes = get_attributes(element2);
    if (hydrating) {
      attributes[attribute] = element2.getAttribute(attribute);
      if (attribute === "src" || attribute === "srcset" || attribute === "href" && element2.nodeName === LINK_TAG) {
        if (!skip_warning) {
          check_src_in_dev_hydration(element2, attribute, value ?? "");
        }
        return;
      }
    }
    if (attributes[attribute] === (attributes[attribute] = value)) return;
    if (attribute === "loading") {
      element2[LOADING_ATTR_SYMBOL] = value;
    }
    if (value == null) {
      element2.removeAttribute(attribute);
    } else if (typeof value !== "string" && get_setters(element2).has(attribute)) {
      element2[attribute] = value;
    } else {
      element2.setAttribute(attribute, value);
    }
  }
  function get_attributes(element2) {
    var _a2;
    return (
      /** @type {Record<string | symbol, unknown>} **/
      /** @type {any} */
      element2[_a2 = ATTRIBUTES_CACHE] ?? (element2[_a2] = {
        [IS_CUSTOM_ELEMENT]: element2.nodeName.includes("-"),
        [IS_HTML]: element2.namespaceURI === NAMESPACE_HTML
      })
    );
  }
  function get_setters(element2) {
    var cache_key = element2.getAttribute("is") || element2.nodeName;
    var setters = setters_cache.get(cache_key);
    if (setters) return setters;
    setters_cache.set(cache_key, setters = /* @__PURE__ */ new Set());
    var descriptors;
    var proto = element2;
    var element_proto = Element.prototype;
    while (element_proto !== proto) {
      descriptors = get_descriptors(proto);
      for (var key2 in descriptors) {
        if (descriptors[key2].set && // better safe than sorry, we don't want spread attributes to mess with HTML content
        key2 !== "innerHTML" && key2 !== "textContent" && key2 !== "innerText") {
          setters.add(key2);
        }
      }
      proto = get_prototype_of(proto);
    }
    return setters;
  }
  function check_src_in_dev_hydration(element2, attribute, value) {
    if (!dev_fallback_default) return;
    if (attribute === "srcset" && srcset_url_equal(element2, value)) return;
    if (src_url_equal(element2.getAttribute(attribute) ?? "", value)) return;
    hydration_attribute_changed(
      attribute,
      element2.outerHTML.replace(element2.innerHTML, element2.innerHTML && "..."),
      String(value)
    );
  }
  function src_url_equal(element_src, url) {
    if (element_src === url) return true;
    return new URL(element_src, document.baseURI).href === new URL(url, document.baseURI).href;
  }
  function split_srcset(srcset) {
    return srcset.split(",").map((src) => src.trim().split(" ").filter(Boolean));
  }
  function srcset_url_equal(element2, srcset) {
    var element_urls = split_srcset(element2.srcset);
    var urls = split_srcset(srcset);
    return urls.length === element_urls.length && urls.every(
      ([url, width], i) => width === element_urls[i][1] && // We need to test both ways because Vite will create an a full URL with
      // `new URL(asset, import.meta.url).href` for the client when `base: './'`, and the
      // relative URLs inside srcset are not automatically resolved to absolute URLs by
      // browsers (in contrast to img.src). This means both SSR and DOM code could
      // contain relative or absolute URLs.
      (src_url_equal(element_urls[i][0], url) || src_url_equal(url, element_urls[i][0]))
    );
  }
  var IS_CUSTOM_ELEMENT, IS_HTML, LINK_TAG, setters_cache;
  var init_attributes2 = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/attributes.js"() {
      init_esm_env();
      init_hydration();
      init_utils();
      init_events();
      init_misc();
      init_warnings();
      init_constants();
      init_task();
      init_utils4();
      init_runtime();
      init_attachments2();
      init_attributes();
      init_class();
      init_style();
      init_constants2();
      init_effects();
      init_select();
      init_async();
      IS_CUSTOM_ELEMENT = /* @__PURE__ */ Symbol("is custom element");
      IS_HTML = /* @__PURE__ */ Symbol("is html");
      LINK_TAG = IS_XHTML ? "link" : "LINK";
      setters_cache = /* @__PURE__ */ new Map();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/customizable-select.js
  var init_customizable_select = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/customizable-select.js"() {
      init_hydration();
      init_operations();
      init_reconciler();
      init_attachments2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/document.js
  var init_document = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/document.js"() {
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/input.js
  function bind_value(input, get3, set2 = get3) {
    var batches = /* @__PURE__ */ new WeakSet();
    listen_to_event_and_reset_event(input, "input", async (is_reset) => {
      if (dev_fallback_default && input.type === "checkbox") {
        bind_invalid_checkbox_value();
      }
      var value = is_reset ? input.defaultValue : input.value;
      value = is_numberlike_input(input) ? to_number(value) : value;
      set2(value);
      if (current_batch !== null) {
        batches.add(current_batch);
      }
      await tick();
      if (value !== (value = get3())) {
        var start = input.selectionStart;
        var end = input.selectionEnd;
        var length = input.value.length;
        input.value = value ?? "";
        if (end !== null) {
          var new_length = input.value.length;
          if (start === end && end === length && new_length > length) {
            input.selectionStart = new_length;
            input.selectionEnd = new_length;
          } else {
            input.selectionStart = start;
            input.selectionEnd = Math.min(end, new_length);
          }
        }
      }
    });
    if (
      // If we are hydrating and the value has since changed,
      // then use the updated value from the input instead.
      hydrating && input.defaultValue !== input.value || // If defaultValue is set, then value == defaultValue
      // TODO Svelte 6: remove input.value check and set to empty string?
      untrack(get3) == null && input.value
    ) {
      set2(is_numberlike_input(input) ? to_number(input.value) : input.value);
      if (current_batch !== null) {
        batches.add(current_batch);
      }
    }
    render_effect(() => {
      if (dev_fallback_default && input.type === "checkbox") {
        bind_invalid_checkbox_value();
      }
      var value = get3();
      if (input === document.activeElement) {
        var batch = (
          /** @type {Batch} */
          async_mode_flag ? previous_batch : current_batch
        );
        if (batches.has(batch)) {
          return;
        }
      }
      if (is_numberlike_input(input) && value === to_number(input.value)) {
        return;
      }
      if (input.type === "date" && !value && !input.value) {
        return;
      }
      if (value !== input.value) {
        input.value = value ?? "";
      }
    });
  }
  function bind_checked(input, get3, set2 = get3) {
    listen_to_event_and_reset_event(input, "change", (is_reset) => {
      var value = is_reset ? input.defaultChecked : input.checked;
      set2(value);
    });
    if (
      // If we are hydrating and the value has since changed,
      // then use the update value from the input instead.
      hydrating && input.defaultChecked !== input.checked || // If defaultChecked is set, then checked == defaultChecked
      untrack(get3) == null
    ) {
      set2(input.checked);
    }
    render_effect(() => {
      var value = get3();
      input.checked = Boolean(value);
    });
  }
  function is_numberlike_input(input) {
    var type = input.type;
    return type === "number" || type === "range";
  }
  function to_number(value) {
    return value === "" ? null : +value;
  }
  var init_input = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/input.js"() {
      init_esm_env();
      init_effects();
      init_shared2();
      init_errors2();
      init_proxy();
      init_task();
      init_hydration();
      init_runtime();
      init_batch();
      init_flags();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/media.js
  var init_media = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/media.js"() {
      init_effects();
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/navigator.js
  var init_navigator = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/navigator.js"() {
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/props.js
  var init_props = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/props.js"() {
      init_effects();
      init_utils();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/size.js
  var init_size = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/size.js"() {
      init_effects();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/this.js
  var init_this = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/this.js"() {
      init_constants();
      init_context2();
      init_effects();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/universal.js
  var init_universal = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/universal.js"() {
      init_effects();
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/bindings/window.js
  var init_window = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/bindings/window.js"() {
      init_effects();
      init_shared2();
    }
  });

  // node_modules/svelte/src/internal/client/dom/legacy/event-modifiers.js
  var init_event_modifiers = __esm({
    "node_modules/svelte/src/internal/client/dom/legacy/event-modifiers.js"() {
      init_utils();
      init_effects();
      init_events();
    }
  });

  // node_modules/svelte/src/internal/client/dom/legacy/lifecycle.js
  var init_lifecycle = __esm({
    "node_modules/svelte/src/internal/client/dom/legacy/lifecycle.js"() {
      init_utils();
      init_context2();
      init_deriveds();
      init_effects();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/dom/legacy/misc.js
  var init_misc2 = __esm({
    "node_modules/svelte/src/internal/client/dom/legacy/misc.js"() {
      init_sources();
      init_runtime();
      init_utils();
    }
  });

  // node_modules/svelte/src/internal/client/reactivity/props.js
  var init_props2 = __esm({
    "node_modules/svelte/src/internal/client/reactivity/props.js"() {
      init_esm_env();
      init_constants2();
      init_utils();
      init_sources();
      init_deriveds();
      init_runtime();
      init_errors2();
      init_constants();
      init_proxy();
      init_store();
      init_flags();
      init_effects();
    }
  });

  // node_modules/svelte/src/internal/client/validate.js
  var init_validate2 = __esm({
    "node_modules/svelte/src/internal/client/validate.js"() {
      init_context2();
      init_constants2();
      init_effects();
      init_warnings();
      init_store();
      init_async();
    }
  });

  // node_modules/svelte/src/legacy/legacy-client.js
  function createClassComponent(options) {
    return new Svelte4Component(options);
  }
  var _events, _instance, Svelte4Component;
  var init_legacy_client = __esm({
    "node_modules/svelte/src/legacy/legacy-client.js"() {
      init_constants();
      init_effects();
      init_sources();
      init_render();
      init_runtime();
      init_batch();
      init_utils();
      init_errors2();
      init_warnings();
      init_esm_env();
      init_constants2();
      init_context2();
      init_flags();
      init_status();
      init_event_modifiers();
      Svelte4Component = class {
        /**
         * @param {ComponentConstructorOptions & {
         *  component: any;
         * }} options
         */
        constructor(options) {
          /** @type {any} */
          __privateAdd(this, _events);
          /** @type {Record<string, any>} */
          __privateAdd(this, _instance);
          var sources = /* @__PURE__ */ new Map();
          var add_source = (key2, value) => {
            var s = mutable_source(value, false, false);
            sources.set(key2, s);
            return s;
          };
          const props = new Proxy(
            { ...options.props || {}, $$events: {} },
            {
              get(target, prop2) {
                return get2(sources.get(prop2) ?? add_source(prop2, Reflect.get(target, prop2)));
              },
              has(target, prop2) {
                if (prop2 === LEGACY_PROPS) return true;
                get2(sources.get(prop2) ?? add_source(prop2, Reflect.get(target, prop2)));
                return Reflect.has(target, prop2);
              },
              set(target, prop2, value) {
                set(sources.get(prop2) ?? add_source(prop2, value), value);
                return Reflect.set(target, prop2, value);
              }
            }
          );
          __privateSet(this, _instance, (options.hydrate ? hydrate : mount)(options.component, {
            target: options.target,
            anchor: options.anchor,
            props,
            context: options.context,
            intro: options.intro ?? false,
            recover: options.recover,
            transformError: options.transformError
          }));
          if (!async_mode_flag && (!options?.props?.$$host || options.sync === false)) {
            flushSync();
          }
          __privateSet(this, _events, props.$$events);
          for (const key2 of Object.keys(__privateGet(this, _instance))) {
            if (key2 === "$set" || key2 === "$destroy" || key2 === "$on") continue;
            define_property(this, key2, {
              get() {
                return __privateGet(this, _instance)[key2];
              },
              /** @param {any} value */
              set(value) {
                __privateGet(this, _instance)[key2] = value;
              },
              enumerable: true
            });
          }
          __privateGet(this, _instance).$set = /** @param {Record<string, any>} next */
          (next2) => {
            Object.assign(props, next2);
          };
          __privateGet(this, _instance).$destroy = () => {
            unmount(__privateGet(this, _instance));
          };
        }
        /** @param {Record<string, any>} props */
        $set(props) {
          __privateGet(this, _instance).$set(props);
        }
        /**
         * @param {string} event
         * @param {(...args: any[]) => any} callback
         * @returns {any}
         */
        $on(event2, callback) {
          __privateGet(this, _events)[event2] = __privateGet(this, _events)[event2] || [];
          const cb = (...args) => callback.call(this, ...args);
          __privateGet(this, _events)[event2].push(cb);
          return () => {
            __privateGet(this, _events)[event2] = __privateGet(this, _events)[event2].filter(
              /** @param {any} fn */
              (fn) => fn !== cb
            );
          };
        }
        $destroy() {
          __privateGet(this, _instance).$destroy();
        }
      };
      _events = new WeakMap();
      _instance = new WeakMap();
    }
  });

  // node_modules/svelte/src/internal/client/dom/elements/custom-element.js
  function get_custom_element_value(prop2, value, props_definition, transform) {
    const type = props_definition[prop2]?.type;
    value = type === "Boolean" && typeof value !== "boolean" ? value != null : value;
    if (!transform || !props_definition[prop2]) {
      return value;
    } else if (transform === "toAttribute") {
      switch (type) {
        case "Object":
        case "Array":
          return value == null ? null : JSON.stringify(value);
        case "Boolean":
          return value ? "" : null;
        case "Number":
          return value == null ? null : value;
        default:
          return value;
      }
    } else {
      switch (type) {
        case "Object":
        case "Array":
          return value && JSON.parse(value);
        case "Boolean":
          return value;
        // conversion already handled above
        case "Number":
          return value != null ? +value : value;
        default:
          return value;
      }
    }
  }
  function get_custom_elements_slots(element2) {
    const result = {};
    element2.childNodes.forEach((node) => {
      result[
        /** @type {Element} node */
        node.slot || "default"
      ] = true;
    });
    return result;
  }
  var SvelteElement;
  var init_custom_element = __esm({
    "node_modules/svelte/src/internal/client/dom/elements/custom-element.js"() {
      init_legacy_client();
      init_effects();
      init_template();
      init_utils();
      init_operations();
      if (typeof HTMLElement === "function") {
        SvelteElement = class extends HTMLElement {
          /**
           * @param {*} $$componentCtor
           * @param {*} $$slots
           * @param {ShadowRootInit | undefined} shadow_root_init
           */
          constructor($$componentCtor, $$slots, shadow_root_init) {
            super();
            /** The Svelte component constructor */
            __publicField(this, "$$ctor");
            /** Slots */
            __publicField(this, "$$s");
            /** @type {any} The Svelte component instance */
            __publicField(this, "$$c");
            /** Whether or not the custom element is connected */
            __publicField(this, "$$cn", false);
            /** @type {Record<string, any>} Component props data */
            __publicField(this, "$$d", {});
            /** `true` if currently in the process of reflecting component props back to attributes */
            __publicField(this, "$$r", false);
            /** @type {Record<string, CustomElementPropDefinition>} Props definition (name, reflected, type etc) */
            __publicField(this, "$$p_d", {});
            /** @type {Record<string, EventListenerOrEventListenerObject[]>} Event listeners */
            __publicField(this, "$$l", {});
            /** @type {Map<EventListenerOrEventListenerObject, Function>} Event listener unsubscribe functions */
            __publicField(this, "$$l_u", /* @__PURE__ */ new Map());
            /** @type {any} The managed render effect for reflecting attributes */
            __publicField(this, "$$me");
            /** @type {ShadowRoot | null} The ShadowRoot of the custom element */
            __publicField(this, "$$shadowRoot", null);
            this.$$ctor = $$componentCtor;
            this.$$s = $$slots;
            if (shadow_root_init) {
              this.$$shadowRoot = this.attachShadow(shadow_root_init);
            }
          }
          /**
           * @param {string} type
           * @param {EventListenerOrEventListenerObject} listener
           * @param {boolean | AddEventListenerOptions} [options]
           */
          addEventListener(type, listener, options) {
            this.$$l[type] = this.$$l[type] || [];
            this.$$l[type].push(listener);
            if (this.$$c) {
              const unsub = this.$$c.$on(type, listener);
              this.$$l_u.set(listener, unsub);
            }
            super.addEventListener(type, listener, options);
          }
          /**
           * @param {string} type
           * @param {EventListenerOrEventListenerObject} listener
           * @param {boolean | AddEventListenerOptions} [options]
           */
          removeEventListener(type, listener, options) {
            super.removeEventListener(type, listener, options);
            if (this.$$c) {
              const unsub = this.$$l_u.get(listener);
              if (unsub) {
                unsub();
                this.$$l_u.delete(listener);
              }
            }
          }
          async connectedCallback() {
            this.$$cn = true;
            if (!this.$$c) {
              let create_slot = function(name) {
                return (anchor) => {
                  const slot2 = create_element("slot");
                  if (name !== "default") slot2.name = name;
                  append(anchor, slot2);
                };
              };
              await Promise.resolve();
              if (!this.$$cn || this.$$c) {
                return;
              }
              const $$slots = {};
              const existing_slots = get_custom_elements_slots(this);
              for (const name of this.$$s) {
                if (name in existing_slots) {
                  if (name === "default" && !this.$$d.children) {
                    this.$$d.children = create_slot(name);
                    $$slots.default = true;
                  } else {
                    $$slots[name] = create_slot(name);
                  }
                }
              }
              for (const attribute of this.attributes) {
                const name = this.$$g_p(attribute.name);
                if (!(name in this.$$d)) {
                  this.$$d[name] = get_custom_element_value(name, attribute.value, this.$$p_d, "toProp");
                }
              }
              for (const key2 in this.$$p_d) {
                if (!(key2 in this.$$d) && this[key2] !== void 0) {
                  this.$$d[key2] = this[key2];
                  delete this[key2];
                }
              }
              this.$$c = createClassComponent({
                component: this.$$ctor,
                target: this.$$shadowRoot || this,
                props: {
                  ...this.$$d,
                  $$slots,
                  $$host: this
                }
              });
              this.$$me = effect_root(() => {
                render_effect(() => {
                  this.$$r = true;
                  for (const key2 of object_keys(this.$$c)) {
                    if (!this.$$p_d[key2]?.reflect) continue;
                    this.$$d[key2] = this.$$c[key2];
                    const attribute_value = get_custom_element_value(
                      key2,
                      this.$$d[key2],
                      this.$$p_d,
                      "toAttribute"
                    );
                    if (attribute_value == null) {
                      this.removeAttribute(this.$$p_d[key2].attribute || key2);
                    } else {
                      this.setAttribute(this.$$p_d[key2].attribute || key2, attribute_value);
                    }
                  }
                  this.$$r = false;
                });
              });
              for (const type in this.$$l) {
                for (const listener of this.$$l[type]) {
                  const unsub = this.$$c.$on(type, listener);
                  this.$$l_u.set(listener, unsub);
                }
              }
              this.$$l = {};
            }
          }
          // We don't need this when working within Svelte code, but for compatibility of people using this outside of Svelte
          // and setting attributes through setAttribute etc, this is helpful
          /**
           * @param {string} attr
           * @param {string} _oldValue
           * @param {string} newValue
           */
          attributeChangedCallback(attr2, _oldValue, newValue) {
            if (this.$$r) return;
            attr2 = this.$$g_p(attr2);
            this.$$d[attr2] = get_custom_element_value(attr2, newValue, this.$$p_d, "toProp");
            this.$$c?.$set({ [attr2]: this.$$d[attr2] });
          }
          disconnectedCallback() {
            this.$$cn = false;
            Promise.resolve().then(() => {
              if (!this.$$cn && this.$$c) {
                this.$$c.$destroy();
                this.$$me();
                this.$$c = void 0;
              }
            });
          }
          /**
           * @param {string} attribute_name
           */
          $$g_p(attribute_name) {
            return object_keys(this.$$p_d).find(
              (key2) => this.$$p_d[key2].attribute === attribute_name || !this.$$p_d[key2].attribute && key2.toLowerCase() === attribute_name
            ) || attribute_name;
          }
        };
      }
    }
  });

  // node_modules/svelte/src/internal/client/dev/console-log.js
  var init_console_log = __esm({
    "node_modules/svelte/src/internal/client/dev/console-log.js"() {
      init_constants();
      init_clone();
      init_warnings();
      init_runtime();
    }
  });

  // node_modules/svelte/src/internal/client/index.js
  var init_client = __esm({
    "node_modules/svelte/src/internal/client/index.js"() {
      init_attachments();
      init_constants2();
      init_context2();
      init_assign();
      init_css();
      init_elements();
      init_hmr();
      init_ownership();
      init_legacy2();
      init_tracing();
      init_inspect();
      init_async2();
      init_validation();
      init_await();
      init_if();
      init_key();
      init_css_props();
      init_each();
      init_html();
      init_slot();
      init_snippet();
      init_svelte_component();
      init_svelte_element();
      init_svelte_head();
      init_css2();
      init_actions();
      init_attachments2();
      init_attributes2();
      init_class();
      init_events();
      init_misc();
      init_customizable_select();
      init_style();
      init_transitions();
      init_document();
      init_input();
      init_media();
      init_navigator();
      init_props();
      init_select();
      init_size();
      init_this();
      init_universal();
      init_window();
      init_hydration();
      init_event_modifiers();
      init_lifecycle();
      init_misc2();
      init_template();
      init_async();
      init_batch();
      init_deriveds();
      init_effects();
      init_sources();
      init_props2();
      init_store();
      init_boundary();
      init_legacy();
      init_render();
      init_runtime();
      init_validate2();
      init_timing();
      init_proxy();
      init_custom_element();
      init_operations();
      init_attributes();
      init_clone();
      init_utils();
      init_validate();
      init_equality2();
      init_console_log();
    }
  });

  // node_modules/svelte/src/internal/client/hydratable.js
  var init_hydratable = __esm({
    "node_modules/svelte/src/internal/client/hydratable.js"() {
      init_flags();
      init_hydration();
      init_warnings();
      init_errors2();
      init_esm_env();
    }
  });

  // node_modules/svelte/src/index-client.js
  var init_index_client = __esm({
    "node_modules/svelte/src/index-client.js"() {
      init_runtime();
      init_utils();
      init_client();
      init_errors2();
      init_flags();
      init_context2();
      init_esm_env();
      init_batch();
      init_context2();
      init_hydratable();
      init_render();
      init_runtime();
      init_snippet();
      if (dev_fallback_default) {
        let throw_rune_error = function(rune) {
          if (!(rune in globalThis)) {
            let value;
            Object.defineProperty(globalThis, rune, {
              configurable: true,
              // eslint-disable-next-line getter-return
              get: () => {
                if (value !== void 0) {
                  return value;
                }
                rune_outside_svelte(rune);
              },
              set: (v) => {
                value = v;
              }
            });
          }
        };
        throw_rune_error("$state");
        throw_rune_error("$effect");
        throw_rune_error("$derived");
        throw_rune_error("$inspect");
        throw_rune_error("$props");
        throw_rune_error("$bindable");
      }
    }
  });

  // node_modules/svelte/src/version.js
  var PUBLIC_VERSION;
  var init_version = __esm({
    "node_modules/svelte/src/version.js"() {
      PUBLIC_VERSION = "5";
    }
  });

  // node_modules/svelte/src/internal/disclose-version.js
  var _a;
  var init_disclose_version = __esm({
    "node_modules/svelte/src/internal/disclose-version.js"() {
      init_version();
      if (typeof window !== "undefined") {
        ((_a = window.__svelte ?? (window.__svelte = {})).v ?? (_a.v = /* @__PURE__ */ new Set())).add(PUBLIC_VERSION);
      }
    }
  });

  // src/App.svelte
  function App($$anchor, $$props) {
    push($$props, true);
    let todos = proxy([]);
    let draft = state("");
    let filter = state(proxy(readFilter()));
    let nextId = 1;
    function readFilter() {
      const hash2 = (typeof location === "undefined" ? "" : location.hash).replace(/^#\/?/, "");
      return hash2 === "active" || hash2 === "completed" ? hash2 : "all";
    }
    user_effect(() => {
      const onHashChange = () => {
        set(filter, readFilter(), true);
      };
      window.addEventListener("hashchange", onHashChange);
      return () => window.removeEventListener("hashchange", onHashChange);
    });
    const visible = user_derived(() => todos.filter((t) => get2(filter) === "all" || (get2(filter) === "active" ? !t.done : t.done)));
    const remaining = user_derived(() => todos.filter((t) => !t.done).length);
    function add(event2) {
      if (event2.key !== "Enter") return;
      const text2 = get2(draft).trim();
      if (text2.length === 0) return;
      todos.push({ id: nextId++, title: text2, done: false });
      set(draft, "");
    }
    function destroy(id) {
      const at = todos.findIndex((t) => t.id === id);
      if (at >= 0) todos.splice(at, 1);
    }
    var section = root_1();
    var header = child(section);
    var input = sibling(child(header), 2);
    remove_input_defaults(input);
    reset(header);
    var section_1 = sibling(header, 2);
    var ul = child(section_1);
    each(ul, 21, () => get2(visible), (todo) => todo.id, ($$anchor2, todo, $$index) => {
      var li = root();
      let classes;
      var input_1 = child(li);
      remove_input_defaults(input_1);
      var label = sibling(input_1, 2);
      var text_1 = only_child(label, true);
      var button = sibling(label, 2);
      reset(li);
      template_effect(() => {
        set_attribute2(li, "data-id", get2(todo).id);
        classes = set_class(li, 1, "", null, classes, { completed: get2(todo).done });
        set_text(text_1, get2(todo).title);
      });
      bind_checked(input_1, () => get2(todo).done, ($$value) => get2(todo).done = $$value);
      delegated("click", button, () => destroy(get2(todo).id));
      append($$anchor2, li);
    });
    reset(ul);
    reset(section_1);
    var footer = sibling(section_1, 2);
    var span = child(footer);
    var strong = child(span);
    var text_2 = only_child(strong, true);
    next();
    reset(span);
    var ul_1 = sibling(span, 2);
    var li_1 = child(ul_1);
    var a = child(li_1);
    let classes_1;
    reset(li_1);
    var li_2 = sibling(li_1, 2);
    var a_1 = child(li_2);
    let classes_2;
    reset(li_2);
    var li_3 = sibling(li_2, 2);
    var a_2 = child(li_3);
    let classes_3;
    reset(li_3);
    reset(ul_1);
    reset(footer);
    reset(section);
    template_effect(() => {
      set_text(text_2, get2(remaining));
      classes_1 = set_class(a, 1, "", null, classes_1, { selected: get2(filter) === "all" });
      classes_2 = set_class(a_1, 1, "", null, classes_2, { selected: get2(filter) === "active" });
      classes_3 = set_class(a_2, 1, "", null, classes_3, { selected: get2(filter) === "completed" });
    });
    delegated("keydown", input, add);
    bind_value(input, () => get2(draft), ($$value) => set(draft, $$value));
    append($$anchor, section);
    pop();
  }
  var root, root_1;
  var init_App = __esm({
    "src/App.svelte"() {
      init_disclose_version();
      init_client();
      root = from_html(`<li><input class="toggle" type="checkbox"/> <label> </label> <button class="destroy">x</button></li>`);
      root_1 = from_html(`<section class="todoapp"><header class="header"><h1>todos</h1> <input class="new-todo" placeholder="What needs to be done?"/></header> <section class="main"><ul class="todo-list"></ul></section> <footer class="footer"><span class="todo-count"><strong> </strong> items left</span> <ul class="filters"><li><a href="#/">All</a></li> <li><a href="#/active">Active</a></li> <li><a href="#/completed">Completed</a></li></ul></footer></section>`);
      delegate(["keydown", "click"]);
    }
  });

  // src/main.js
  var require_main = __commonJS({
    "src/main.js"() {
      init_index_client();
      init_App();
      mount(App, { target: document.getElementById("app") });
    }
  });
  require_main();
})();

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-58gs.js
 * @description Strict - checking 'this' from a global scope (Injected getter defined within strict mode)
 * @onlyStrict
 */

"use strict";
var o = {};
Object.defineProperty(o, "foo",  { get : function() { return this; } });
if (o.foo!==o) {
    throw "'this' had incorrect value!";
}
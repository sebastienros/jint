/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-59-s.js
 * @description Strict Mode - checking 'this' (Injected getter includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var o = {};
Object.defineProperty(o, "foo", { get: function() { "use strict"; return this; } });
return o.foo===o;
}
runTestCase(testcase);
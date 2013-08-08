/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-58-s.js
 * @description Strict Mode - checking 'this' (Injected getter defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var o = {};
Object.defineProperty(o, "foo",  { get: function() { return this; } });
return o.foo===o;
}
runTestCase(testcase);
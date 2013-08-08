/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-54-s.js
 * @description Strict Mode - checking 'this' (Literal getter defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var o = { get foo() { return this; } }
return o.foo===o;
}
runTestCase(testcase);
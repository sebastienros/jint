/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-60-s.js
 * @description Strict Mode - checking 'this' (Injected setter defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var o = {};
var x = 2;
Object.defineProperty(o, "foo", { set: function(stuff) { x=this; } });
o.foo = 3;
return x===o;
}
runTestCase(testcase);
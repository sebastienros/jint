/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-56-s.js
 * @description Strict Mode - checking 'this' (Literal setter defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var x = 2;
var o = { set foo(stuff) { x=this; } }
o.foo = 3;
return x===o;
}
runTestCase(testcase);
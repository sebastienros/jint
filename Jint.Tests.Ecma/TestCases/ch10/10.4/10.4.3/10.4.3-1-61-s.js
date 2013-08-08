/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-61-s.js
 * @description Strict Mode - checking 'this' (Injected setter includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var o = {};
var x = 2;
Object.defineProperty(o, "foo", { set: function(stuff) { "use strict"; x=this; } });
o.foo = 3;
return x===o;
}
runTestCase(testcase);
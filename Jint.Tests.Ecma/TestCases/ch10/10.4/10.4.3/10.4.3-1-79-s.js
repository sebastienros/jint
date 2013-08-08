/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-79-s.js
 * @description Strict Mode - checking 'this' (strict function declaration called by Function.prototype.bind(someObject)())
 * @onlyStrict
 */
    
function testcase() {
var o = {};
function f() { "use strict"; return this===o;};
return f.bind(o)();
}
runTestCase(testcase);
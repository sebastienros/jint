/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-93-s.js
 * @description Strict Mode - checking 'this' (non-strict function declaration called by strict Function.prototype.call(someObject))
 * @onlyStrict
 */
    
function testcase() {
var o = {};
function f() { return this===o;};
return (function () {"use strict"; return f.call(o); })();
}
runTestCase(testcase);
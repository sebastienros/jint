/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-82-s.js
 * @description Strict Mode - checking 'this' (non-strict function declaration called by strict eval)
 * @noStrict
 */
    
function testcase() {
function f() { return this!==undefined;};
return (function () {"use strict"; return eval("f();");})();
}
runTestCase(testcase);
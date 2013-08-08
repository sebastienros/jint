/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-81-s.js
 * @description Strict Mode - checking 'this' (non-strict function declaration called by strict function declaration)
 * @noStrict
 */
    
function testcase() {
function f() { return this!==undefined;};
function foo() { "use strict"; return f();}
return foo();
}
runTestCase(testcase);
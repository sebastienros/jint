/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-62-s.js
 * @description Strict Mode - checking 'this' (strict function declaration called by non-strict function declaration)
 * @onlyStrict
 */
    
function testcase() {
function f() { "use strict"; return this;};
function foo() { return f();}
return foo()===undefined;
}
runTestCase(testcase);
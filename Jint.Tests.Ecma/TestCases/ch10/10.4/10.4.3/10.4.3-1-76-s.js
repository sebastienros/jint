/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-76-s.js
 * @description Strict Mode - checking 'this' (strict function declaration called by Function.prototype.bind()())
 * @onlyStrict
 */
    
function testcase() {
function f() { "use strict"; return this===undefined;};
return f.bind()();
}
runTestCase(testcase);
/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-84-s.js
 * @description Strict Mode - checking 'this' (non-strict function declaration called by strict new'ed Function constructor)
 * @noStrict
 */
    
function testcase() {
fnGlobalObject().f = function()  { return this!==undefined;};
return (function () {return new Function("\"use strict\";return f();")();})();
}
runTestCase(testcase);
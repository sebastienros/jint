/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-83-s.js
 * @description Strict Mode - checking 'this' (non-strict function declaration called by strict Function constructor)
 * @noStrict
 */
    
function testcase() {
fnGlobalObject().f = function() {return this!==undefined;};
return (function () {return Function("\"use strict\";return f();")();})();
}
runTestCase(testcase);
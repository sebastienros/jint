/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-20-s.js
 * @description Strict Mode - checking 'this' (indirect eval includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var my_eval = eval;
return my_eval("\"use strict\";\nthis") === fnGlobalObject();
}
runTestCase(testcase);
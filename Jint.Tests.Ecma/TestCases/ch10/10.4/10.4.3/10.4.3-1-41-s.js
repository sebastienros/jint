/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-41-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression defined within a FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var f1 = function () {
    "use strict";
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
}
return f1();
}
runTestCase(testcase);
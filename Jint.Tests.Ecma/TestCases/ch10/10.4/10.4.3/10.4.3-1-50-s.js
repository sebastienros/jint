/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-50-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression with a strict directive prologue defined within a FunctionExpression)
 * @noStrict
 */
    
function testcase() {
var f1 = function () {
    return ((function () {
        "use strict";
        return typeof this;
    })()==="undefined") && (this===fnGlobalObject());
}
return f1();
}
runTestCase(testcase);
/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-49-s.js
 * @description Strict Mode - checking 'this' (FunctionExpression with a strict directive prologue defined within a FunctionExpression)
 * @noStrict
 */
    
function testcase() {
var f1 = function () {
    var f = function () {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
}
return f1();
}
runTestCase(testcase);
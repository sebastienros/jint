/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-37-s.js
 * @description Strict Mode - checking 'this' (FunctionExpression defined within a FunctionDeclaration with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
function f1() {
    "use strict";
    var f = function () {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
}
return f1();
}
runTestCase(testcase);
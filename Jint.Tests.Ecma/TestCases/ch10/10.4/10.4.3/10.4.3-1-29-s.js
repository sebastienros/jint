/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-29-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression defined within a FunctionDeclaration inside strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
function f1() {
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
}
return f1();
}
runTestCase(testcase);
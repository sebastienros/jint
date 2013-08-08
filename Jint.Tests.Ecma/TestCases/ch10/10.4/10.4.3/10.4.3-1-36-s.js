/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-36-s.js
 * @description Strict Mode - checking 'this' (FunctionDeclaration defined within a FunctionDeclaration with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
function f1() {
    "use strict";
    function f() {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
}
return f1();
}
runTestCase(testcase);
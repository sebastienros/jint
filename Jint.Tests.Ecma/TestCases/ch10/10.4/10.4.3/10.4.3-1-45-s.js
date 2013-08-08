/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-45-s.js
 * @description Strict Mode - checking 'this' (FunctionDeclaration with a strict directive prologue defined within a FunctionDeclaration)
 * @noStrict
 */
    
function testcase() {
function f1() {
    function f() {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
}
return f1();
}
runTestCase(testcase);
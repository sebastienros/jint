/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-42-s.js
 * @description Strict Mode - checking 'this' (FunctionDeclaration defined within an Anonymous FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
return (function () {
    "use strict";
    function f() {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
})();
}
runTestCase(testcase);
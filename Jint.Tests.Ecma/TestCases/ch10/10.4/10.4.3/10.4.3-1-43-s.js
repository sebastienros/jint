/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-43-s.js
 * @description Strict Mode - checking 'this' (FunctionExpression defined within an Anonymous FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
return (function () {
    "use strict";
    var f = function () {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
})();
}
runTestCase(testcase);
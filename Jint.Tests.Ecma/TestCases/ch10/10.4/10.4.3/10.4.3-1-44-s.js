/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-44-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression defined within an Anonymous FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
return (function () {
    "use strict";
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
})();
}
runTestCase(testcase);
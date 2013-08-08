/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-35-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression defined within an Anonymous FunctionExpression inside strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
return (function () {
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
})();
}
runTestCase(testcase);
/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-53-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 */
    
function testcase() {
return (function () {
    return ((function () {
        "use strict";
        return typeof this;
    })()==="undefined") && (this===fnGlobalObject());
})();
}
runTestCase(testcase);
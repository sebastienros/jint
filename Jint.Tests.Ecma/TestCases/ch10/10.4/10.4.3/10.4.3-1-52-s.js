/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-52-s.js
 * @description Strict Mode - checking 'this' (FunctionExpression with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 */
    
function testcase() {
return (function () {
    var f = function () {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
})();
}
runTestCase(testcase);
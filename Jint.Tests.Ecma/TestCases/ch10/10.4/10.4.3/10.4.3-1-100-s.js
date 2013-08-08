/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-100-s.js
 * @description Strict Mode - checking 'this' (strict function passed as arg to String.prototype.replace from non-strict context)
 * @onlyStrict
 */
    
function testcase() {
var x = 3;

function f() {
    "use strict";
    x = this;
    return "a";
}
return ("ab".replace("b", f)==="aa") && (x===undefined);
}
runTestCase(testcase);
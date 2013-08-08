/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-101-s.js
 * @description Strict Mode - checking 'this' (non-strict function passed as arg to String.prototype.replace from strict context)
 * @noStrict
 */
    
function testcase() {
var x = 3;

function f() {
    x = this;
    return "a";
}

return (function() {"use strict"; return "ab".replace("b", f)==="aa";}()) && (x===fnGlobalObject());
}
runTestCase(testcase);
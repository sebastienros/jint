/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-102-s.js
 * @description Strict Mode - checking 'this' (strict anonymous function passed as arg to String.prototype.replace from non-strict context)
 * @onlyStrict
 */
    
function testcase() {
var x = 3;

return ("ab".replace("b", (function () { 
                                "use strict";
                                return function () {
                                    x = this;
                                    return "a";
                                }
                           })())==="aa") && (x===undefined);
}
runTestCase(testcase);
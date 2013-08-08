/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-7.js
 * @description Array.prototype.forEach - built-in functions can be used as thisArg
 */


function testcase() {

        var result = false;

        function callbackfn(val, idx, obj) {
            result = (this === eval);
        }

        [11].forEach(callbackfn, eval);
        return result;
    }
runTestCase(testcase);

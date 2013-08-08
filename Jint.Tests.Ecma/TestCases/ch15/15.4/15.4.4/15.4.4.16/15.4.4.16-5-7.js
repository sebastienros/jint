/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-7.js
 * @description Array.prototype.every - built-in functions can be used as thisArg
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === eval;
        }

        return [11].every(callbackfn, eval) && accessed;
    }
runTestCase(testcase);

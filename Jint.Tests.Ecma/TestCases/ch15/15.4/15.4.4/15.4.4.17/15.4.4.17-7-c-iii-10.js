/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-10.js
 * @description Array.prototype.some - return value of callbackfn is a number (value is Infinity)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return Infinity;
        }

        return [11].some(callbackfn);
    }
runTestCase(testcase);

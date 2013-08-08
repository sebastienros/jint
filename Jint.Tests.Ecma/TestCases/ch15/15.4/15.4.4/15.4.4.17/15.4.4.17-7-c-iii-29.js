/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-29.js
 * @description Array.prototype.some - return value (new Boolean(false)) of callbackfn is treated as true value
 */


function testcase() {

        function callbackfn() {
            return new Boolean(false);
        }

        return [11].some(callbackfn);
    }
runTestCase(testcase);

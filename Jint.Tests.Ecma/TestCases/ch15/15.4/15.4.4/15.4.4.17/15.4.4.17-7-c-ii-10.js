/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-10.js
 * @description Array.prototype.some - callbackfn is called with 1 formal parameter
 */


function testcase() {

        function callbackfn(val) {
            return val > 10;
        }

        return [11, 12].some(callbackfn);
    }
runTestCase(testcase);

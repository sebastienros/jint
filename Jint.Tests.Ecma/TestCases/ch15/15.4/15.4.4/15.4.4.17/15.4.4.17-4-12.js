/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-12.js
 * @description Array.prototype.some - 'callbackfn' is a function
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        return [9, 11].some(callbackfn);
    }
runTestCase(testcase);

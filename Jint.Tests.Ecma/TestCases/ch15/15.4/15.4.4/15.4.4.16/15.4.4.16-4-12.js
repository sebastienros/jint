/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-12.js
 * @description Array.prototype.every - 'callbackfn' is a function
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        return ![11, 9].every(callbackfn);
    }
runTestCase(testcase);

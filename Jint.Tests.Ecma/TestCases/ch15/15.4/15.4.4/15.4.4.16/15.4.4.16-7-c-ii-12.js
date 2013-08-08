/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-12.js
 * @description Array.prototype.every - callbackfn is called with 3 formal parameter
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val > 10 && obj[idx] === val;
        }

        return [11, 12, 13].every(callbackfn) && 3 === called;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-17.js
 * @description Array.prototype.reduce - 'accumulator' used for current iteration is the result of previous iteration on an Array
 */


function testcase() {

        var result = true;
        var accessed = false;
        var preIteration = 1;
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            if (preIteration !== prevVal) {
                result = false;
            }
            preIteration = curVal;
            return curVal;
        }

        [11, 12, 13].reduce(callbackfn, 1);
        return result && accessed;
    }
runTestCase(testcase);

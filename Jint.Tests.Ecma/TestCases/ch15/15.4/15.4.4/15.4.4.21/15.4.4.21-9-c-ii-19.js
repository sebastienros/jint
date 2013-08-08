/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-19.js
 * @description Array.prototype.reduce - value of 'accumulator' used for first iteration is the value of least index property which is not undefined when 'initialValue' is not present on an Array
 */


function testcase() {

        var called = 0;
        var result = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            called++;
            if (idx === 1) {
                result = (prevVal === 11) && curVal === 9;
            }
        }

        [11, 9].reduce(callbackfn);
        return result && called === 1;
    }
runTestCase(testcase);

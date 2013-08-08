/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-12.js
 * @description Array.prototype.reduce - callbackfn is called with 3 formal parameter
 */


function testcase() {

        var result = false;
        function callbackfn(prevVal, curVal, idx) {
            result = (prevVal === 1 && arguments[3][idx] === curVal);
        }

        [11].reduce(callbackfn, 1);
        return result;
    }
runTestCase(testcase);

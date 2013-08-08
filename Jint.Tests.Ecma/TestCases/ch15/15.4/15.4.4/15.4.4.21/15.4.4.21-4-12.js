/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-12.js
 * @description Array.prototype.reduce - 'callbackfn' is a function
 */


function testcase() {

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return curVal > 10;
        }

        return [11, 9].reduce(callbackfn, 1) === false && accessed;
    }
runTestCase(testcase);

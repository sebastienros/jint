/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-13.js
 * @description Array.prototype.filter return value of callbackfn is a number (value is NaN)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return NaN;
        }

        var newArr = [11].filter(callbackfn);
        return newArr.length === 0 && accessed;
    }
runTestCase(testcase);

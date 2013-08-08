/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-14.js
 * @description Array.prototype.filter applied to the Array-like object that 'length property doesn't exist
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return true;
        }

        var obj = { 0: 11, 1: 12 };

        var newArr = Array.prototype.filter.call(obj, callbackfn);
        return newArr.length === 0 && !accessed;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-1.js
 * @description Array.prototype.filter - value of 'length' is undefined
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return true;
        }

        var obj = { 0: 0, 1: 1, length: undefined };
        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 0 && !accessed;
    }
runTestCase(testcase);

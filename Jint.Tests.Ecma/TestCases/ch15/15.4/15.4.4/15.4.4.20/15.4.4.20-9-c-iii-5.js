/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-5.js
 * @description Array.prototype.filter - return value of callbackfn is a boolean (value is true)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { 0: 11, length: 1 };

        var newArr = Array.prototype.filter.call(obj, callbackfn);
        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);

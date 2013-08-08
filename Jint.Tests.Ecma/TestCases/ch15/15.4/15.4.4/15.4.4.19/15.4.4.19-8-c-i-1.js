/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-1.js
 * @description Array.prototype.map - element to be retrieved is own data property on an Array-like object
 */


function testcase() {

        var kValue = {};

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                return val === kValue;
            }
            return false;
        }

        var obj = { 5: kValue, length: 100 };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr[5] === true;
    }
runTestCase(testcase);

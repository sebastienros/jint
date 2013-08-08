/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-2.js
 * @description Array.prototype.map - element to be retrieved is own data property on an Array
 */


function testcase() {

        var kValue = {};

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === kValue;
            }
            return false;
        }

        var arr = [kValue];

        var newArr = arr.map(callbackfn);

        return newArr[0] === true;
    }
runTestCase(testcase);

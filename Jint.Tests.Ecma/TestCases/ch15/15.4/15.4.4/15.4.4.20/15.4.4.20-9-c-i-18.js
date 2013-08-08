/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-18.js
 * @description Array.prototype.filter - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return undefined === val && idx === 0;
        }

        var arr = [];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        var newArr = arr.filter(callbackfn);

        return newArr.length === 1 && newArr[0] === undefined;
    }
runTestCase(testcase);

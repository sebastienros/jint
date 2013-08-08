/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-16.js
 * @description Array.prototype.filter - decreasing length of array does not delete non-configurable properties
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var arr = [0, 1, 2];

        Object.defineProperty(arr, "2", {
            get: function () {
                return "unconfigurable";
            },
            configurable: false
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                arr.length = 2;
                return 1;
            },
            configurable: true
        });

        var newArr = arr.filter(callbackfn);

        return newArr.length === 3 && newArr[2] === "unconfigurable";
    }
runTestCase(testcase);

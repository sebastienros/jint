/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-14.js
 * @description Array.prototype.filter - decreasing length of array causes index property not to be visited
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }
        var arr = [0, 1, 2, "last"];

        Object.defineProperty(arr, "0", {
            get: function () {
                arr.length = 3;
                return 0;
            },
            configurable: true
        });

        var newArr = arr.filter(callbackfn);


        return newArr.length === 3 && newArr[2] === 2;
    }
runTestCase(testcase);

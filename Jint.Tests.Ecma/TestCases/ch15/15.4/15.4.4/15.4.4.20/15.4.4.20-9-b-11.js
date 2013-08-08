/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-11.js
 * @description Array.prototype.filter - deleting property of prototype causes prototype index property not to be visited on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }
        var arr = [0, , 2];

        Object.defineProperty(arr, "0", {
            get: function () {
                delete Array.prototype[1];
                return 0;
            },
            configurable: true
        });

        try {
            Array.prototype[1] = 1;
            var newArr = arr.filter(callbackfn);
            return newArr.length === 2 && newArr[1] !== 1;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);

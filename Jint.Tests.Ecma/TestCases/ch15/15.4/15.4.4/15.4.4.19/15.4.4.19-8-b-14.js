/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-14.js
 * @description Array.prototype.map - decreasing length of array causes index property not to be visited
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return idx === 3 && typeof val === "undefined";
        }

        var arr = [0, 1, 2, "last"];

        Object.defineProperty(arr, "0", {
            get: function () {
                arr.length = 3;
                return 0;
            },
            configurable: true
        });

        var testResult = arr.map(callbackfn);
        return typeof testResult[3] === "undefined";
    }
runTestCase(testcase);

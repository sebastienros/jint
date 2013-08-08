/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-17.js
 * @description Array.prototype.lastIndexOf -  decreasing length of array causes index property not to be visited
 */


function testcase() {

        var arr = [0, 1, 2, "last", 4];

        Object.defineProperty(arr, "4", {
            get: function () {
                arr.length = 3;
                return 0;
            },
            configurable: true
        });

        return -1 === arr.lastIndexOf("last");
    }
runTestCase(testcase);

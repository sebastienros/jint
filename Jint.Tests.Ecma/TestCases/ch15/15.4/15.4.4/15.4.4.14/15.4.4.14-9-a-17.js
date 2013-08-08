/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-17.js
 * @description Array.prototype.indexOf - decreasing length of array causes index property not to be visited
 */


function testcase() {

        var arr = [0, 1, 2, "last"];

        Object.defineProperty(arr, "0", {
            get: function () {
                arr.length = 3;
                return 0;
            },
            configurable: true
        });

        return -1 === arr.indexOf("last");
    }
runTestCase(testcase);

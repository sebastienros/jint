/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-11.js
 * @description Array.prototype.indexOf - deleting own property causes index property not to be visited on an Array-like object
 */


function testcase() {

        var arr = { length: 2 };

        Object.defineProperty(arr, "1", {
            get: function () {
                return 6.99;
            },
            configurable: true
        });

        Object.defineProperty(arr, "0", {
            get: function () {
                delete arr[1];
                return 0;
            },
            configurable: true
        });

        return -1 === Array.prototype.indexOf.call(arr, 6.99);
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-4.js
 * @description Array.prototype.indexOf - deleted properties in step 2 are visible here
 */


function testcase() {

        var arr = { 2: 6.99 };

        Object.defineProperty(arr, "length", {
            get: function () {
                delete arr[2];
                return 3;
            },
            configurable: true
        });

        return -1 === Array.prototype.indexOf.call(arr, 6.99);
    }
runTestCase(testcase);

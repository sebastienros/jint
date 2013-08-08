/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-1.js
 * @description Array.prototype.lastIndexOf - added properties in step 2 are visible here
 */


function testcase() {

        var arr = { };

        Object.defineProperty(arr, "length", {
            get: function () {
                arr[2] = "length";
                return 3;
            },
            configurable: true
        });

        return 2 === Array.prototype.lastIndexOf.call(arr, "length");
    }
runTestCase(testcase);

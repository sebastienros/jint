/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-11.js
 * @description Array.prototype.indexOf - the length of iteration isn't changed by adding elements to the array during iteration
 */


function testcase() {

        var arr = [20];

        Object.defineProperty(arr, "0", {
            get: function () {
                arr[1] = 1;
                return 0;
            },
            configurable: true
        });

        return arr.indexOf(1) === -1;
    }
runTestCase(testcase);

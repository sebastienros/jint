/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-5.js
 * @description Array.prototype.indexOf - deleted properties in step 5 are visible here on an Array-like object
 */


function testcase() {

        var arr = { 10: false, length: 30 };

        var fromIndex = {
            valueOf: function () {
                delete arr[10];
                return 3;
            }
        };

        return -1 === Array.prototype.indexOf.call(arr, false, fromIndex);
    }
runTestCase(testcase);

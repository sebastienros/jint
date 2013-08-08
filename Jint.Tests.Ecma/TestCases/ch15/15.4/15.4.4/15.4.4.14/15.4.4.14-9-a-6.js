/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-6.js
 * @description Array.prototype.indexOf - deleted properties in step 5 are visible here on an Array
 */


function testcase() {

        var arr = [];
        arr[10] = "10";
        arr.length = 20;

        var fromIndex = {
            valueOf: function () {
                delete arr[10];
                return 3;
            }
        };

        return -1 === arr.indexOf("10", fromIndex);
    }
runTestCase(testcase);

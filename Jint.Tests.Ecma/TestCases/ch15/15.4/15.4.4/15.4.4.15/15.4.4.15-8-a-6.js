/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-6.js
 * @description Array.prototype.lastIndexOf -  deleted properties of step 5 are visible here on an Array
 */


function testcase() {

        var arr = [];
        arr[10] = "10";
        arr.length = 20;

        var fromIndex = {
            valueOf: function () {
                delete arr[10];
                return 11;
            }
        };

        return -1 === arr.lastIndexOf("10", fromIndex);
    }
runTestCase(testcase);

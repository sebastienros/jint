/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-3.js
 * @description Array.prototype.lastIndexOf -  added properties in step 5 are visible here on an Array
 */


function testcase() {

        var arr = [];
        arr.length = 30;
        var targetObj = function () { };

        var fromIndex = {
            valueOf: function () {
                arr[4] = targetObj;
                return 11;
            }
        };

        return 4 === arr.lastIndexOf(targetObj, fromIndex);
    }
runTestCase(testcase);

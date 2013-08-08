/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-2.js
 * @description Array.prototype.lastIndexOf -  added properties in step 5 are visible here on an Array-like object
 */


function testcase() {

        var arr = { length: 30 };
        var targetObj = function () { };

        var fromIndex = {
            valueOf: function () {
                arr[4] = targetObj;
                return 10;
            }
        };
        
        return 4 === Array.prototype.lastIndexOf.call(arr, targetObj, fromIndex);
    }
runTestCase(testcase);

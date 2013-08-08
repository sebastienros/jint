/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-3.js
 * @description Array.prototype.indexOf - added properties in step 5 are visible here on an Array
 */


function testcase() {

        var arr = [];
        arr.length = 30;
        var targetObj = function () { };

        var fromIndex = {
            valueOf: function () {
                arr[4] = targetObj;
                return 3;
            }
        };

        return 4 === arr.indexOf(targetObj, fromIndex);
    }
runTestCase(testcase);

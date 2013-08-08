/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-14.js
 * @description Array.prototype.filter applied to Error object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj instanceof Error;
        }

        var obj = new Error();
        obj.length = 1;
        obj[0] = 1;

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr[0] === 1;
    }
runTestCase(testcase);

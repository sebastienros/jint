/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-11.js
 * @description Array.prototype.map - applied to Date object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Date;
        }

        var obj = new Date();
        obj.length = 1;
        obj[0] = 1;

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);

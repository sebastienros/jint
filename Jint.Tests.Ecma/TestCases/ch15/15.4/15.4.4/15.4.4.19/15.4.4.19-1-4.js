/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-4.js
 * @description Array.prototype.map - applied to Boolean object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Boolean;
        }

        var obj = new Boolean(true);
        obj.length = 2;
        obj[0] = 11;
        obj[1] = 12;

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult[0] === true && testResult[1] === true;
    }
runTestCase(testcase);

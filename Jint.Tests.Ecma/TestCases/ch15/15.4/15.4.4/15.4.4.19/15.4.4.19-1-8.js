/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-8.js
 * @description Array.prototype.map - applied to String object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof String;
        }

        var obj = new String("abc");
        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult[0] === true && testResult[1] === true && testResult[2] === true;
    }
runTestCase(testcase);

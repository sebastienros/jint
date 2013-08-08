/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-3.js
 * @description Array.prototype.map - applied to boolean primitive
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj instanceof Boolean;
        }

        try {
            Boolean.prototype[0] = true;
            Boolean.prototype.length = 1;

            var testResult = Array.prototype.map.call(false, callbackfn);
            return testResult[0] === true;
        } finally {
            delete Boolean.prototype[0];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);

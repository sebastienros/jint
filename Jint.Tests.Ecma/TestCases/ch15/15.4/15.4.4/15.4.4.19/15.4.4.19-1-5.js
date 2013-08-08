/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-5.js
 * @description Array.prototype.map - applied to number primitive
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Number;
        }

        try {
            Number.prototype[0] = 1;
            Number.prototype.length = 1;

            var testResult = Array.prototype.map.call(2.5, callbackfn);
            return testResult[0] === true;
        } finally {
            delete Number.prototype[0];
            delete Number.prototype.length;
        }
    }
runTestCase(testcase);

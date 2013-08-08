/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-3.js
 * @description Array.prototype.forEach applied to boolean primitive
 */


function testcase() {
        var result = false;

        function callbackfn(val, idx, obj) {
            result = obj instanceof Boolean;
        }

        try {
            Boolean.prototype[0] = true;
            Boolean.prototype.length = 1;

            Array.prototype.forEach.call(false, callbackfn);
            return result;

        } finally {
            delete Boolean.prototype[0];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);

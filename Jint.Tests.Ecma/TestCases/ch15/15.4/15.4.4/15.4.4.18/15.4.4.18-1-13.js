/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-13.js
 * @description Array.prototype.forEach applied to the JSON object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = ('[object JSON]' === Object.prototype.toString.call(obj));
        }

        try {
            JSON.length = 1;
            JSON[0] = 1;
            Array.prototype.forEach.call(JSON, callbackfn);
            return result;
        } finally {
            delete JSON.length;
            delete JSON[0];
        }
    }
runTestCase(testcase);

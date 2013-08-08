/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-13.js
 * @description Array.prototype.every applied to the JSON object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return ('[object JSON]' !== Object.prototype.toString.call(obj));
        }

        try {
            JSON.length = 1;
            JSON[0] = 1;
            return !Array.prototype.every.call(JSON, callbackfn);
        } finally {
            delete JSON.length;
            delete JSON[0];
        }
    }
runTestCase(testcase);

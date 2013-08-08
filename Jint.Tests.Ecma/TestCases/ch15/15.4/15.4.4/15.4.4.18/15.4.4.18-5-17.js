/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-17.js
 * @description Array.prototype.forEach - the JSON object can be used as thisArg
 */


function testcase() {

        var result = false;
        function callbackfn(val, idx, obj) {
            result = (this === JSON);
        }

        [11].forEach(callbackfn, JSON);
        return result;
    }
runTestCase(testcase);

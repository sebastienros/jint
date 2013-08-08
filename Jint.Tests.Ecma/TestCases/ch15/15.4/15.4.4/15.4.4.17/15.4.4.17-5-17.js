/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-17.js
 * @description Array.prototype.some - the JSON object can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this === JSON;
        }

        return [11].some(callbackfn, JSON);
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-13.js
 * @description Array.prototype.filter applied to the JSON object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return '[object JSON]' === Object.prototype.toString.call(JSON);
        }

        try {
            JSON.length = 1;
            JSON[0] = 1;
            var newArr = Array.prototype.filter.call(JSON, callbackfn);
            return newArr[0] === 1;
        } finally {
            delete JSON.length;
            delete JSON[0];
        }
    }
runTestCase(testcase);

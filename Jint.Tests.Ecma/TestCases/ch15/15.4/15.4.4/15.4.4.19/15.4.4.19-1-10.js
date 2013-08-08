/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-10.js
 * @description Array.prototype.map - applied to the Math object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return ('[object Math]' === Object.prototype.toString.call(obj));
        }
       
        try {
            Math.length = 1;
            Math[0] = 1;
            var testResult = Array.prototype.map.call(Math, callbackfn);
            return testResult[0] === true;
        } finally {
            delete Math[0];
            delete Math.length;
        }
    }
runTestCase(testcase);

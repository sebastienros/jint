/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-10.js
 * @description Array.prototype.forEach applied to the Math object
 */


function testcase() {
        var result = false;

        function callbackfn(val, idx, obj) {
            result = ('[object Math]' === Object.prototype.toString.call(obj));
        }

        try {
            Math.length = 1;
            Math[0] = 1;
            Array.prototype.forEach.call(Math, callbackfn);
            return result;
        } finally {
            delete Math[0];
            delete Math.length;
        }
    }
runTestCase(testcase);

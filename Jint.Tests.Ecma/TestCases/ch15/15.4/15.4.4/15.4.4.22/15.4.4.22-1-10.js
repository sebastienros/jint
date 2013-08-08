/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-10.js
 * @description Array.prototype.reduceRight applied to the Math object
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return '[object Math]' === Object.prototype.toString.call(obj);
        }

        try {
            Math.length = 1;
            Math[0] = 1;
            return Array.prototype.reduceRight.call(Math, callbackfn, 1) && accessed;
        } finally {
            delete Math[0];
            delete Math.length;
        }
    }
runTestCase(testcase);

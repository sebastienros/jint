/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-3.js
 * @description Array.prototype.reduceRight applied to boolean primitive
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return obj instanceof Boolean;
        }

        try {
            Boolean.prototype[0] = 1;
            Boolean.prototype.length = 1;
            return Array.prototype.reduceRight.call(false, callbackfn, 1) && accessed;
        } finally {
            delete Boolean.prototype[0];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);

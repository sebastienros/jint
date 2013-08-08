/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-28.js
 * @description Array.prototype.reduceRight - value of 'length' is boundary value (2^32)
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = {
            0: 12,
            length: 4294967296
        };

        return Array.prototype.reduceRight.call(obj, callbackfn, 1) === 1 && !accessed;
    }
runTestCase(testcase);

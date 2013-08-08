/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-5.js
 * @description Array.prototype.reduceRight - value of 'length' is a number (value is -0)
 */


function testcase() {

        var accessed = false;

        function callbackfn(preVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, length: -0 };

        return Array.prototype.reduceRight.call(obj, callbackfn, 1) === 1 && !accessed;
    }
runTestCase(testcase);

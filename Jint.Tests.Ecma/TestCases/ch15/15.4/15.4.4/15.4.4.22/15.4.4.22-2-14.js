/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-14.js
 * @description Array.prototype.reduceRight applied to the Array-like object that 'length' property doesn't exist
 */


function testcase() {

        var obj = { 0: 11, 1: 12 };
        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return curVal > 10;
        }

        return Array.prototype.reduceRight.call(obj, callbackfn, 111) === 111 && !accessed;
    }
runTestCase(testcase);

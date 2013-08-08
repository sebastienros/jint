/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-2.js
 * @description Array.prototype.reduceRight applied to an Array-like object, 'length' is 0 (length overridden to false(type conversion))
 */


function testcase() {

        var accessed = false;

        function callbackfn(preVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, length: false };

        return Array.prototype.reduceRight.call(obj, callbackfn, 1) === 1 && !accessed;
    }
runTestCase(testcase);

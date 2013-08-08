/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-1.js
 * @description Array.prototype.reduceRight - value of 'length' is undefined
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, length: undefined };

        return Array.prototype.reduceRight.call(obj, callbackfn, 1) === 1 && !accessed;
    }
runTestCase(testcase);

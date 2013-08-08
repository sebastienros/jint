/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-12.js
 * @description Array.prototype.reduceRight applied to RegExp object
 */


function testcase() {

        var obj = new RegExp();
        obj.length = 1;
        obj[0] = 1;
        var accessed = false;

        function callbackfn(prevVal, curVal, idx, o) {
            accessed = true;
            return o instanceof RegExp;
        }

        return Array.prototype.reduceRight.call(obj, callbackfn, 1) && accessed;
    }
runTestCase(testcase);

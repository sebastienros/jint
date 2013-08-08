/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-4.js
 * @description Array.prototype.reduceRight applied to Boolean object
 */


function testcase() {

        var obj = new Boolean(true);
        obj.length = 2;
        obj[0] = 11;
        obj[1] = 12;
        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return obj instanceof Boolean;
        }

        return Array.prototype.reduceRight.call(obj, callbackfn, 11) && accessed;
    }
runTestCase(testcase);

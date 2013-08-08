/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-7.js
 * @description Array.prototype.reduceRight applied to string primitive
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return obj instanceof String;
        }

        return Array.prototype.reduceRight.call("hello\nworld\\!", callbackfn, "h") && accessed;
    }
runTestCase(testcase);

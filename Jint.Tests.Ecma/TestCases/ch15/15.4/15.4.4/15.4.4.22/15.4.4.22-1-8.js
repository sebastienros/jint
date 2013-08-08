/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-8.js
 * @description Array.prototype.reduceRight applied to String object
 */


function testcase() {

        var obj = new String("hello\nworld\\!");
        var accessed = false;

        function callbackfn(prevVal, curVal, idx, o) {
            accessed = true;
            return o instanceof String;
        }

        return Array.prototype.reduceRight.call(obj, callbackfn, "h") && accessed;
    }
runTestCase(testcase);

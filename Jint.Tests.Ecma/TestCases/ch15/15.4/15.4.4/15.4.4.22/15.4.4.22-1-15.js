/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-15.js
 * @description Array.prototype.reduceRight applied to the Arguments object
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return '[object Arguments]' === Object.prototype.toString.call(obj);
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        return Array.prototype.reduceRight.call(obj, callbackfn, "a") && accessed;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-1-9.js
 * @description Array.prototype.reduceRight applied to Function object
 */


function testcase() {

        var obj = function (a, b) {
            return a + b;
        };
        obj[0] = 11;
        obj[1] = 9;
        var accessed = false;

        function callbackfn(prevVal, curVal, idx, o) {
            accessed = true;
            return o instanceof Function;
        }

        return Array.prototype.reduceRight.call(obj, callbackfn, 11) && accessed;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-2-7.js
 * @description Array.prototype.reduceRight applied to Array-like object, 'length' is an own accessor property
 */


function testcase() {

        var accessed = true;
        var obj = {};
        obj[0] = 12;
        obj[1] = 11;
        obj[2] = 9;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return obj.length === 2;
        }

        Object.defineProperty(obj, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        return Array.prototype.reduceRight.call(obj, callbackfn, 11) && accessed;
    }
runTestCase(testcase);

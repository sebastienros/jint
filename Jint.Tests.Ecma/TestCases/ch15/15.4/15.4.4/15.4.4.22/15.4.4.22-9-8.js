/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-8.js
 * @description Array.prototype.reduceRight - no observable effects occur if 'len' is 0
 */


function testcase() {

        var accessed = false;
        function callbackfn() {
            accessed = true;
        }

        var obj = { length: 0 };

        Object.defineProperty(obj, "5", {
            get: function () {
                accessed = true;
                return 10;
            },
            configurable: true
        });

        Array.prototype.reduceRight.call(obj, function () { }, "initialValue");
        return !accessed;
    }
runTestCase(testcase);

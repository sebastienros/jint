/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-8.js
 * @description Array.prototype.reduce - no observable effects occur if 'len' is 0
 */


function testcase() {

        var accessed = false;
        var callbackAccessed = false;
        function callbackfn() {
            callbackAccessed = true;
        }

        var obj = { length: 0 };

        Object.defineProperty(obj, "0", {
            get: function () {
                accessed = true;
                return 10;
            },
            configurable: true
        });

        Array.prototype.reduce.call(obj, function () { }, "initialValue");
        return !accessed && !callbackAccessed;
    }
runTestCase(testcase);

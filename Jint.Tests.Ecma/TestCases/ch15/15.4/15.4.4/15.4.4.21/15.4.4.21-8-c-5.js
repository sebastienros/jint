/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-c-5.js
 * @description Array.prototype.reduce - if exception occurs, it occurs after any side-effects that might be produced by step 2
 */


function testcase() {

        var obj = { };

        var accessed = false;

        Object.defineProperty(obj, "length", {
            get: function () {
                accessed = true;
                return 2;
            },
            configurable: true
        });

        try {
            Array.prototype.reduce.call(obj, function () { });
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) && accessed;
        }
    }
runTestCase(testcase);

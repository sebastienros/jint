/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-10.js
 * @description Array.prototype.some - the exception is not thrown if exception was thrown by step 2
 */


function testcase() {

        var obj = { 0: 11, 1: 12 };

        Object.defineProperty(obj, "length", {
            get: function () {
                throw new SyntaxError();
            },
            configurable: true
        });

        try {
            Array.prototype.some.call(obj, undefined);
            return false;
        } catch (ex) {
            return !(ex instanceof TypeError);
        }
    }
runTestCase(testcase);

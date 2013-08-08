/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-4-11.js
 * @description Array.prototype.reduce - the exception is not thrown if exception was thrown by step 3
 */


function testcase() {

        var obj = { 0: 11, 1: 12 };

        Object.defineProperty(obj, "length", {
            get: function () {
                return {
                    toString: function () {
                        throw new SyntaxError();
                    }
                };
            },
            configurable: true
        });

        try {
            Array.prototype.reduce.call(obj, undefined);
            return false;
        } catch (ex) {
            return !(ex instanceof TypeError);
        }
    }
runTestCase(testcase);

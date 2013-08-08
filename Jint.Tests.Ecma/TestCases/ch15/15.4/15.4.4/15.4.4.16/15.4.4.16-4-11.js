/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-11.js
 * @description Array.prototype.every - the exception is not thrown if exception was thrown by step 3
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
            Array.prototype.every.call(obj, undefined);
            return false;
        } catch (ex) {
            return !(ex instanceof TypeError);
        }
    }
runTestCase(testcase);

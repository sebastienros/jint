/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-11.js
 * @description Array.prototype.reduceRight - the exception is not thrown if exception was thrown by step 3
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
            Array.prototype.reduceRight.call(obj, undefined);
            return false;
        } catch (ex) {
            return !(ex instanceof TypeError);
        }
    }
runTestCase(testcase);

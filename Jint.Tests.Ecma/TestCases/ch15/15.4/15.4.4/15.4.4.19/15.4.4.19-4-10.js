/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-10.js
 * @description Array.prototype.map - the exception is not thrown if exception was thrown by step 2
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
            Array.prototype.map.call(obj, undefined);
            return false;
        } catch (ex) {
            return !(ex instanceof TypeError);
        }
    }
runTestCase(testcase);

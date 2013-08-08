/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-31.js
 * @description Array.prototype.lastIndexOf terminates iteration on unhandled exception on an Array-like object
 */


function testcase() {

        var accessed = false;
        var obj = { length: 3 };

        Object.defineProperty(obj, "2", {
            get: function () {
                throw new TypeError();
            },
            configurable: true
        });

        Object.defineProperty(obj, "1", {
            get: function () {
                accessed = true;
                return true;
            },
            configurable: true
        });

        try {
            Array.prototype.lastIndexOf.call(obj, true);
            return false;
        } catch (e) {
            return (e instanceof TypeError) && !accessed;
        }

    }
runTestCase(testcase);

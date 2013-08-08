/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-30.js
 * @description Array.prototype.indexOf - terminates iteration on unhandled exception on an Array
 */


function testcase() {

        var accessed = false;
        var arr = [];

        Object.defineProperty(arr, "0", {
            get: function () {
                throw new TypeError();
            },
            configurable: true
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                accessed = true;
                return true;
            },
            configurable: true
        });

        try {
            arr.indexOf(true);
            return false;
        } catch (e) {
            return (e instanceof TypeError) && !accessed;
        }
    }
runTestCase(testcase);

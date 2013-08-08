/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-ii-1.js
 * @description Array.prototype.reduce - added properties in step 2 are visible here
 */


function testcase() {

        var obj = { };

        Object.defineProperty(obj, "length", {
            get: function () {
                obj[1] = "accumulator";
                return 3;
            },
            configurable: true
        });

        return Array.prototype.reduce.call(obj, function () { }) === "accumulator";
    }
runTestCase(testcase);

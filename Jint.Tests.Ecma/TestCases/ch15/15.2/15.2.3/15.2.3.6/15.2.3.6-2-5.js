/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-5.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is NaN)
 */
function testcase() {
        var obj = {};
        Object.defineProperty(obj, NaN, {});

        return obj.hasOwnProperty("NaN");

    }
runTestCase(testcase);

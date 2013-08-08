/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-22.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 0.00000001)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 0.00000001, {});

        return obj.hasOwnProperty("1e-8");

    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-23.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 1e-7)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 1e-7, {});

        return obj.hasOwnProperty("1e-7");

    }
runTestCase(testcase);

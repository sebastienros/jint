/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-17-1.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 1(trailing 5 zeros))
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 000001, {});

        return obj.hasOwnProperty("1");

    }
runTestCase(testcase);

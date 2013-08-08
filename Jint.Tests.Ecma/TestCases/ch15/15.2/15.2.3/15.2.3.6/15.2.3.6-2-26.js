/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-26.js
 * @description Object.defineProperty - argument 'P' is an integer that converts to a string (value is 123)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 123, {});

        return obj.hasOwnProperty("123");

    }
runTestCase(testcase);

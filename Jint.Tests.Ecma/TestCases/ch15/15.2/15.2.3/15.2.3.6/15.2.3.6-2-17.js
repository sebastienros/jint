/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-17.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 1e+20)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 1e+20, {});

        return obj.hasOwnProperty("100000000000000000000");

    }
runTestCase(testcase);

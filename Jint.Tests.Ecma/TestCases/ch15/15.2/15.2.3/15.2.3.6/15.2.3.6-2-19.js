/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-19.js
 * @description Object.defineProperty - argument 'P' is a number that converts to string (value is 1e+22)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 1e+22, {});

        return obj.hasOwnProperty("1e+22");

    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-20.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 0.000001)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 0.000001, {});

        return obj.hasOwnProperty("0.000001");

    }
runTestCase(testcase);

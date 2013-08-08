/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-12.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is +Infinity)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, +Infinity, {});

        return obj.hasOwnProperty("Infinity");

    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-39.js
 * @description Object.defineProperty - argument 'P' is an array that converts to a string
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, [1, 2], {});

        return obj.hasOwnProperty("1,2");

    }
runTestCase(testcase);

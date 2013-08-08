/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-30.js
 * @description Object.defineProperty - argument 'P' is a number that converts to a string (value is 1(following 21 zeros).1)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, 1000000000000000000000.1, {});

        return obj.hasOwnProperty("1e+21");

    }
runTestCase(testcase);

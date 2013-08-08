/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-42.js
 * @description Object.defineProperty - argument 'P' is a Number Object that converts to a string
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, new Number(123), {});

        return obj.hasOwnProperty("123");

    }
runTestCase(testcase);

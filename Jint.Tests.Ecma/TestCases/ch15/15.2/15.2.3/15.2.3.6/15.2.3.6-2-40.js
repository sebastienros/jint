/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-40.js
 * @description Object.defineProperty - argument 'P' is a String Object that converts to a string
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, new String("Hello"), {});

        return obj.hasOwnProperty("Hello");

    }
runTestCase(testcase);

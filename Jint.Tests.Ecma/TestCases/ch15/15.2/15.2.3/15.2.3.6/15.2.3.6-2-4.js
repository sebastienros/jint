/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-4.js
 * @description Object.defineProperty - argument 'P' is a boolean whose value is true
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, true, {});

        return obj.hasOwnProperty("true");

    }
runTestCase(testcase);

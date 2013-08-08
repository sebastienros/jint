/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-3.js
 * @description Object.defineProperty - argument 'P' is a boolean whose value is false
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, false, {});

        return obj.hasOwnProperty("false");

    }
runTestCase(testcase);

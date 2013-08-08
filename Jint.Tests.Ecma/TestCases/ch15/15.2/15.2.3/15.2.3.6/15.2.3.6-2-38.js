/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-38.js
 * @description Object.defineProperty - argument 'P' is applied to string '1' 
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "1", {});

        return obj.hasOwnProperty("1");

    }
runTestCase(testcase);

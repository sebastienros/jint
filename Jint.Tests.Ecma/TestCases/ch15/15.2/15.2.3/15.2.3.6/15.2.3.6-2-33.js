/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-33.js
 * @description Object.defineProperty - argument 'P' is applied to an empty string 
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "", {});

        return obj.hasOwnProperty("");

    }
runTestCase(testcase);

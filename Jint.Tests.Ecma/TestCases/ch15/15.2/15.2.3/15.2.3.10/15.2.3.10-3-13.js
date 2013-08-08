/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-13.js
 * @description Object.preventExtensions - named properties cannot be added into a Function object
 */


function testcase() {
        var funObj = function () { };
        var preCheck = Object.isExtensible(funObj);
        Object.preventExtensions(funObj);

        funObj.exName = 2;
        return preCheck && !funObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-20.js
 * @description Object.preventExtensions - named properties cannot be added into an Error object
 */


function testcase() {
        var errObj = new Error();
        var preCheck = Object.isExtensible(errObj);
        Object.preventExtensions(errObj);

        errObj.exName = 2;
        return preCheck && !errObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

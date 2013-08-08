/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-14.js
 * @description Object.preventExtensions - named properties cannot be added into an Array object
 */


function testcase() {
        var arrObj = [];
        var preCheck = Object.isExtensible(arrObj);
        Object.preventExtensions(arrObj);

        arrObj.exName = 2;
        return preCheck && !arrObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

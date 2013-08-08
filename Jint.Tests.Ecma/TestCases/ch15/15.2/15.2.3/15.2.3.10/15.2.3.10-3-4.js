/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-4.js
 * @description Object.preventExtensions - indexed properties cannot be added into an Array object
 */


function testcase() {
        var arrObj = [];
        var preCheck = Object.isExtensible(arrObj);
        Object.preventExtensions(arrObj);

        arrObj[0] = 12;
        return preCheck && !arrObj.hasOwnProperty("0");
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-9.js
 * @description Object.preventExtensions - indexed properties cannot be added into a RegExp object
 */


function testcase() {
        var regObj = new RegExp();
        var preCheck = Object.isExtensible(regObj);
        Object.preventExtensions(regObj);

        regObj[0] = 12;
        return preCheck && !regObj.hasOwnProperty("0");
    }
runTestCase(testcase);

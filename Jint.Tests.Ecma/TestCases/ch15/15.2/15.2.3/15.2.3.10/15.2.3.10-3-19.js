/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-19.js
 * @description Object.preventExtensions - named properties cannot be added into a RegExp object
 */


function testcase() {
        var regObj = new RegExp();
        var preCheck = Object.isExtensible(regObj);
        Object.preventExtensions(regObj);

        regObj.exName = 2;
        return preCheck && !regObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

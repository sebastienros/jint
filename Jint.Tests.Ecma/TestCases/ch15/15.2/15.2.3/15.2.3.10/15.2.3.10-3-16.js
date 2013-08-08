/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-16.js
 * @description Object.preventExtensions - named properties cannot be added into a Boolean object
 */


function testcase() {
        var boolObj = new Boolean(true);
        var preCheck = Object.isExtensible(boolObj);
        Object.preventExtensions(boolObj);

        boolObj.exName = 2;
        return preCheck && !boolObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

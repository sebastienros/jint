/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-15.js
 * @description Object.preventExtensions - named properties cannot be added into a String object
 */


function testcase() {
        var strObj = new String("bbq");
        var preCheck = Object.isExtensible(strObj);
        Object.preventExtensions(strObj);

        strObj.exName = 2;
        return preCheck && !strObj.hasOwnProperty("exName");
    }
runTestCase(testcase);

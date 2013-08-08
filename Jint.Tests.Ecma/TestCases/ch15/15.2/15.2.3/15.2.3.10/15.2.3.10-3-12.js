/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-12.js
 * @description Object.preventExtensions - named properties cannot be added into the returned object
 */


function testcase() {
        var obj = {};
        var preCheck = Object.isExtensible(obj);
        Object.preventExtensions(obj);

        obj.exName = 2;
        return preCheck && !Object.hasOwnProperty("exName");
    }
runTestCase(testcase);

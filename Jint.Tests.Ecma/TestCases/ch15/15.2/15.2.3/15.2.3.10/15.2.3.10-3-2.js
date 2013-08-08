/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-2.js
 * @description Object.preventExtensions - indexed properties cannot be added into the returned object
 */


function testcase() {

        var obj = {};
        var preCheck = Object.isExtensible(obj);
        Object.preventExtensions(obj);

        obj[0] = 12;
        return preCheck && !obj.hasOwnProperty("0");
    }
runTestCase(testcase);

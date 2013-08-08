/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-1.js
 * @description Object.preventExtensions - Object.isExtensible(arg) returns false if arg is the returned object
 */


function testcase() {
        var obj = {};
        var preCheck = Object.isExtensible(obj);
        Object.preventExtensions(obj);

        return preCheck && !Object.isExtensible(obj);
    }
runTestCase(testcase);

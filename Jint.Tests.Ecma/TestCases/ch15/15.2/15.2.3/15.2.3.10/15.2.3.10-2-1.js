/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-2-1.js
 * @description Object.preventExtensions - repeated calls to preventExtensions have no side effects
 */


function testcase() {
        var obj = {};
        var testResult1 = true;
        var testResult2 = true;

        var preCheck = Object.isExtensible(obj);

        Object.preventExtensions(obj);
        testResult1 = Object.isExtensible(obj);
        Object.preventExtensions(obj);
        testResult2 = Object.isExtensible(obj);

        return preCheck && !testResult1 && !testResult2;

    }
runTestCase(testcase);

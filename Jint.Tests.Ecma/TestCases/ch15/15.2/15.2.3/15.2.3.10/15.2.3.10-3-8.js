/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-8.js
 * @description Object.preventExtensions - indexed properties cannot be added into a Date object
 */


function testcase() {
        var dateObj = new Date();
        var preCheck = Object.isExtensible(dateObj);
        Object.preventExtensions(dateObj);

        dateObj[0] = 12;
        return preCheck && !dateObj.hasOwnProperty("0");
    }
runTestCase(testcase);

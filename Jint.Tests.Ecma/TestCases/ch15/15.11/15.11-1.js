/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11-1.js
 * @description Error - ConversionError has been removed from IE9 standard mode
 */


function testcase() {
        return typeof ConversionError === "undefined";
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11-2.js
 * @description Error - RegExpError has been removed from IE9 standard mode
 */


function testcase() {
        return typeof RegExpError === "undefined";
    }
runTestCase(testcase);

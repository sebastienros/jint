/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-49.js
 * @description String.prototype.trim - 'this' is a RegExp Object that converts to a string
 */


function testcase() {
        var regObj = new RegExp(/test/);
        return String.prototype.trim.call(regObj) === "/test/";
    }
runTestCase(testcase);

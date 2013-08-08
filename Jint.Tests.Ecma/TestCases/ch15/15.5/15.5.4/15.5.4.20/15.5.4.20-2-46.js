/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-46.js
 * @description String.prototype.trim - 'this' is a Function Object that converts to a string
 */


function testcase() {
        var funObj = function () { return arguments; };
        return typeof(String.prototype.trim.call(funObj)) === "string";
    }
runTestCase(testcase);

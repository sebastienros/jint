/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-51.js
 * @description String.prototype.trim - 'this' is a Arguments Object that converts to a string
 */


function testcase() {
        var argObj = function () { return arguments; } (1, 2, true);
        return String.prototype.trim.call(argObj) === "[object Arguments]";
    }
runTestCase(testcase);

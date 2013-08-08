/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-27.js
 * @description Array.prototype.filter - Array.isArray(arg) returns true when arg is the returned array
 */


function testcase() {

        var newArr = [11].filter(function () { });

        return Array.isArray(newArr);
    }
runTestCase(testcase);

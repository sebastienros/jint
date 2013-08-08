/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-6-1.js
 * @description Array.prototype.map - Array.isArray returns true when input argument is the ourput array
 */


function testcase() {

        var newArr = [11].map(function () { });

        return Array.isArray(newArr);

    }
runTestCase(testcase);

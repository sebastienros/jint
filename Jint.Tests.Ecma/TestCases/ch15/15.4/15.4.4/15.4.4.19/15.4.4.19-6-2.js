/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-6-2.js
 * @description Array.prototype.map - the returned array is instanceof Array
 */


function testcase() {

        var newArr = [11].map(function () { });

        return newArr instanceof Array;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-28.js
 * @description Array.prototype.filter - the returned array is instanceof Array
 */


function testcase() {

        var newArr = [11].filter(function () { });

        return newArr instanceof Array;
    }
runTestCase(testcase);

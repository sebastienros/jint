/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-29.js
 * @description Array.prototype.filter - returns an array whose length is 0
 */


function testcase() {

        var newArr = [11].filter(function () { });

        return newArr.length === 0;
    }
runTestCase(testcase);

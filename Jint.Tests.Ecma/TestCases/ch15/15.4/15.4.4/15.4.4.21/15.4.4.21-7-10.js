/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-10.js
 * @description Array.prototype.reduce - 'initialValue' is present
 */


function testcase() {

        var str = "initialValue is present";
        return str === [].reduce(function () { }, str);
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-11.js
 * @description Array.prototype.reduce - 'initialValue' is not present
 */


function testcase() {

        var str = "initialValue is not present";
        return str === [str].reduce(function () { });
    }
runTestCase(testcase);

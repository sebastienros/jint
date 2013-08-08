/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-10.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a number (value is NaN)
 */


function testcase() {

        var obj = { 0: 0, length: NaN };

        return Array.prototype.lastIndexOf.call(obj, 0) === -1;
    }
runTestCase(testcase);

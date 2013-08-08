/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-9.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a number (value is -Infinity)
 */


function testcase() {

        var obj = { 0: 0, length: -Infinity };

        return Array.prototype.lastIndexOf.call(obj, 0) === -1;
    }
runTestCase(testcase);

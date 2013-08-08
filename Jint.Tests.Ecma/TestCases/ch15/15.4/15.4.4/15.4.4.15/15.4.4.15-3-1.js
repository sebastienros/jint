/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-1.js
 * @description Array.prototype.lastIndexOf - value of 'length' is undefined
 */


function testcase() {

        var obj = { 0: 1, 1: 1, length: undefined };

        return Array.prototype.lastIndexOf.call(obj, 1) === -1;
    }
runTestCase(testcase);

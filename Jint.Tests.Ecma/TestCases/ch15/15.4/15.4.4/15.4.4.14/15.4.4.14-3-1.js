/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-1.js
 * @description Array.prototype.indexOf - value of 'length' is undefined
 */


function testcase() {

        var obj = { 0: 1, 1: 1, length: undefined };

        return Array.prototype.indexOf.call(obj, 1) === -1;
    }
runTestCase(testcase);

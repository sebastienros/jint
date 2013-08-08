/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-2.js
 * @description Array.prototype.indexOf return -1 when 'length' is a boolean (value is true)
 */


function testcase() {
        var obj = { 0: 0, 1: 1, length: true };
        return Array.prototype.indexOf.call(obj, 0) === 0 &&
            Array.prototype.indexOf.call(obj, 1) === -1;
    }
runTestCase(testcase);

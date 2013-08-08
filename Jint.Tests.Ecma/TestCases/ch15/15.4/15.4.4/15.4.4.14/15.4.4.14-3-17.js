/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-17.js
 * @description Array.prototype.indexOf - 'length' is a string containing a number with leading zeros
 */


function testcase() {

        var obj = { 1: true, 2: "0002.0", length: "0002.0" };

        return Array.prototype.indexOf.call(obj, true) === 1 &&
            Array.prototype.indexOf.call(obj, "0002.0") === -1;
    }
runTestCase(testcase);

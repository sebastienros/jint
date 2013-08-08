/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-12.js
 * @description Array.prototype.indexOf - 'length' is a string containing a negative number
 */


function testcase() {

        var obj = { 1: "true", 2: "2", length: "-4294967294" };

        return Array.prototype.indexOf.call(obj, "true") === 1 &&
        Array.prototype.indexOf.call(obj, "2") === -1;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-14.js
 * @description Array.prototype.indexOf - 'length' is undefined property
 */


function testcase() {

        var obj = { 0: true, 1: true };

        return Array.prototype.indexOf.call(obj, true) === -1;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-14.js
 * @description Array.prototype.lastIndexOf - 'length' is undefined property on an Array-like object
 */


function testcase() {

        var obj = { 0: null, 1: undefined };

        return Array.prototype.lastIndexOf.call(obj, null) === -1;
    }
runTestCase(testcase);

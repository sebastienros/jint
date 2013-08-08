/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-8.js
 * @description Array.prototype.lastIndexOf applied to String object
 */


function testcase() {

        var obj = new String("undefined");

        return Array.prototype.lastIndexOf.call(obj, "f") === 4;
    }
runTestCase(testcase);

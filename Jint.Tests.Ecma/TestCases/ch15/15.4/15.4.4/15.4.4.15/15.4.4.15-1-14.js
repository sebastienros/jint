/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-14.js
 * @description Array.prototype.lastIndexOf applied to Error object
 */


function testcase() {

        var obj = new SyntaxError();
        obj.length = 2;
        obj[1] = Infinity;

        return Array.prototype.lastIndexOf.call(obj, Infinity) === 1;
    }
runTestCase(testcase);

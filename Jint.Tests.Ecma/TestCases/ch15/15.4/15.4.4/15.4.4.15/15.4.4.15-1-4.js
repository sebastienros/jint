/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-4.js
 * @description Array.prototype.lastIndexOf applied to Boolean object
 */


function testcase() {

        var obj = new Boolean(false);
        obj.length = 2;
        obj[1] = true;

        return Array.prototype.lastIndexOf.call(obj, true) === 1;
    }
runTestCase(testcase);

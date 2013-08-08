/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-12.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string containing negative number
 */


function testcase() {

        var obj = {1: null, 2: undefined, length: "-4294967294"};

        return Array.prototype.lastIndexOf.call(obj, null) === 1 &&
            Array.prototype.lastIndexOf.call(obj, undefined) === -1;
    }
runTestCase(testcase);

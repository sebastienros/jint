/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-11.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string containing positive number
 */


function testcase() {

        var obj = {1: true, 2: false, length: "2"};

        return Array.prototype.lastIndexOf.call(obj, true) === 1 &&
            Array.prototype.lastIndexOf.call(obj, false) === -1;
    }
runTestCase(testcase);

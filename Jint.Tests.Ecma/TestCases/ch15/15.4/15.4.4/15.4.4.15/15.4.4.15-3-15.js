/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-15.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string containing an exponential number
 */


function testcase() {

        var obj = {229: 229, 230: 2.3E2, length: "2.3E2"};

        return Array.prototype.lastIndexOf.call(obj, 229) === 229 &&
            Array.prototype.lastIndexOf.call(obj, 2.3E2) === -1;
    }
runTestCase(testcase);

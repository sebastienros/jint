/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-13.js
 * @description Array.prototype.indexOf - 'length' is a string containing a decimal number
 */


function testcase() {

        var obj = { 199: true, 200: "200.59", length: "200.59" };

        return Array.prototype.indexOf.call(obj, true) === 199 &&
            Array.prototype.indexOf.call(obj, "200.59") === -1;
    }
runTestCase(testcase);

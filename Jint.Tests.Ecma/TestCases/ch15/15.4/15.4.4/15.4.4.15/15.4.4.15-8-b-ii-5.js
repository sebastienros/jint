/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-5.js
 * @description Array.prototype.lastIndexOf - search element is -NaN
 */


function testcase() {
        return [+NaN, NaN, -NaN].lastIndexOf(-NaN) === -1;
    }
runTestCase(testcase);

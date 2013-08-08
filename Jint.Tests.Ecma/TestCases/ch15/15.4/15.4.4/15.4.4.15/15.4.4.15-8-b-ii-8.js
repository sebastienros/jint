/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-8.js
 * @description Array.prototype.lastIndexOf - both array element and search element are numbers, and they have same value
 */


function testcase() {

        return [-1, 0, 1].lastIndexOf(-1) === 0;
    }
runTestCase(testcase);

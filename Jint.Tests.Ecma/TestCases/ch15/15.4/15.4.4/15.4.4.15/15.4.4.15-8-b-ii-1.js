/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-1.js
 * @description Array.prototype.lastIndexOf - type of array element is different from type of search element
 */


function testcase() {

        return ["true"].lastIndexOf(true) === -1 &&
            ["0"].lastIndexOf(0) === -1 &&
            [false].lastIndexOf(0) === -1 &&
            [undefined].lastIndexOf(0) === -1 &&
            [null].lastIndexOf(0) === -1 &&
            [[]].lastIndexOf(0) === -1;
    }
runTestCase(testcase);

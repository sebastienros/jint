/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-1.js
 * @description Array.prototype.indexOf - type of array element is different from type of search element
 */


function testcase() {

        return ["true"].indexOf(true) === -1 &&
            ["0"].indexOf(0) === -1 &&
            [false].indexOf(0) === -1 &&
            [undefined].indexOf(0) === -1 &&
            [null].indexOf(0) === -1 &&
            [[]].indexOf(0) === -1;
    }
runTestCase(testcase);

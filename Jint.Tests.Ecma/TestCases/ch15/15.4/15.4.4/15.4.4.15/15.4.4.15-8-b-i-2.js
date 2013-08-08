/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-2.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own data property on an Array
 */


function testcase() {
        return [true, true, true].lastIndexOf(true) === 2 &&
            [true, true, false].lastIndexOf(true) === 1 &&
            [true, false, false].lastIndexOf(true) === 0;
    }
runTestCase(testcase);

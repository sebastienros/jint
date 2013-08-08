/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-10.js
 * @description Array.prototype.lastIndexOf - both array element and search element are booleans, and they have same value
 */


function testcase() {

        return [false, true].lastIndexOf(true) === 1;
    }
runTestCase(testcase);

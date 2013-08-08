/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-8.js
 * @description Array.prototype.indexOf - both array element and search element are Number, and they have same value
 */


function testcase() {

        return [-1, 0, 1].indexOf(1) === 2;
    }
runTestCase(testcase);

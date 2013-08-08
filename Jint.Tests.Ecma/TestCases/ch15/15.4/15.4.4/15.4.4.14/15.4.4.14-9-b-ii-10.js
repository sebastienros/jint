/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-10.js
 * @description Array.prototype.indexOf - both array element and search element are Boolean type, and they have same value
 */


function testcase() {

        return [false, true].indexOf(true) === 1;
    }
runTestCase(testcase);

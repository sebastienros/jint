/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-3.js
 * @description Array.prototype.lastIndexOf - both type of array element and type of search element are Null
 */


function testcase() {

        return [null].lastIndexOf(null) === 0;
    }
runTestCase(testcase);

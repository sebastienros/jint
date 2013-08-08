/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-9.js
 * @description Array.prototype.lastIndexOf - both array element and search element are strings, and they have exactly the same sequence of characters
 */


function testcase() {

        return ["abc", "ab", "bca", ""].lastIndexOf("abc") === 0;
    }
runTestCase(testcase);

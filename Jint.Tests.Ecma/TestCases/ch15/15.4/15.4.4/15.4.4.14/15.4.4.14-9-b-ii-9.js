/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-9.js
 * @description Array.prototype.indexOf - both array element and search element are String, and they have exactly the same sequence of characters
 */


function testcase() {

        return ["", "ab", "bca", "abc"].indexOf("abc") === 3;
    }
runTestCase(testcase);

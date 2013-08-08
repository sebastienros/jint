/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-7.js
 * @description Array.prototype.indexOf applied to string primitive
 */


function testcase() {

        return Array.prototype.indexOf.call("abc", "b") === 1;
    }
runTestCase(testcase);

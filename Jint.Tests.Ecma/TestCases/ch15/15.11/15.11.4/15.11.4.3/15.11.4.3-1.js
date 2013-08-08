/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.3/15.11.4.3-1.js
 * @description Error.prototype.message is not enumerable.
 */




function testcase() {
        for (var i in Error.prototype) {
            if (i==="message") {
                return false;
            }
        }
        return true;
}
runTestCase(testcase);
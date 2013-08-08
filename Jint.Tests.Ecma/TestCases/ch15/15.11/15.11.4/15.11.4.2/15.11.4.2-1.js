/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.2/15.11.4.2-1.js
 * @description Error.prototype.name is not enumerable.
 */




function testcase() {
        for (var i in Error.prototype) {
            if (i==="name") {
                return false;
            }
        }
        return true;
}
runTestCase(testcase);
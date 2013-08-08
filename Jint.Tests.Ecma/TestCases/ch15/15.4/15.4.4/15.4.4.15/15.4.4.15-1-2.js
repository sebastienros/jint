/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-2.js
 * @description Array.prototype.lastIndexOf applied to null throws a TypeError
 */


function testcase() {

        try {
            Array.prototype.lastIndexOf.call(null);
            return false;
        } catch (e) {
            if (e instanceof TypeError) {
                return true;
            }
        }
    }
runTestCase(testcase);

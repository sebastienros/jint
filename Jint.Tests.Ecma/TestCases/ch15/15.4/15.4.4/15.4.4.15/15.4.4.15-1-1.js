/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-1.js
 * @description Array.prototype.lastIndexOf applied to undefined throws a TypeError
 */


function testcase() {

        try {
            Array.prototype.lastIndexOf.call(undefined);
            return false;
        } catch (e) {
            if (e instanceof TypeError) {
                return true;
            }
        }
    }
runTestCase(testcase);

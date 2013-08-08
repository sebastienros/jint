/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1-4.js
 * @description Object.defineProperty applied to string primitive throws a TypeError
 */


function testcase() {
        try {
            Object.defineProperty("hello\nworld\\!", "foo", {});
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

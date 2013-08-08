/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-1-3.js
 * @description Object.preventExtensions throws TypeError if 'O' is a boolean primitive value
 */


function testcase() {
        try {
            Object.preventExtensions(true);
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);

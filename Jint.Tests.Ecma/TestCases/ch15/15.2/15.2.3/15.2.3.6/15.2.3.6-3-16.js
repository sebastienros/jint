/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-16.js
 * @description Object.defineProperty - 'Attributes' is null (8.10.5 step 1)
 */


function testcase() {

        try {
            Object.defineProperty({}, "property", null);
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

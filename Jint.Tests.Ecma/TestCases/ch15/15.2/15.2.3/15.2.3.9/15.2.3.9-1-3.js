/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-3.js
 * @description Object.freeze throws TypeError if type of first param is boolean primitive
 */


function testcase() {
        var result = false;
        try {
            Object.freeze(false);

            return false;
        } catch (e) {
            result = e instanceof TypeError;
        }
        try {
            Object.freeze(true);

            return false;
        } catch (e) {
            return result && e instanceof TypeError;
        }
    }
runTestCase(testcase);

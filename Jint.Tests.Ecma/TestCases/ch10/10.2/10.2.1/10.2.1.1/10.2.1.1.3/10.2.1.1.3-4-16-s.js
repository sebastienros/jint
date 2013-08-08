/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-16-s.js
 * @description Strict Mode - TypeError is thrown when changing the value of a Value Property of the Global Object under strict mode (NaN)
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            NaN = 12;
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

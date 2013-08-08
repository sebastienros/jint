/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-27-s.js
 * @description Strict Mode - TypeError is not thrown when changing the value of the Constructor Properties of the Global Object under strict mode (Number)
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var numBak = Number;
        try {
            Number = 12;
            return true;
        } finally {
            Number = numBak;
        }
    }
runTestCase(testcase);

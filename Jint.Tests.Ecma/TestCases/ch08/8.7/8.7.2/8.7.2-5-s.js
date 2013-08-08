/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-5-s.js
 * @description Strict Mode - TypeError is thrown if LeftHandSide is a reference to a non-existent property of an non-extensible object
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _8_7_2_5 = {};
        Object.preventExtensions(_8_7_2_5);

        try {
            _8_7_2_5.b = 11;
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

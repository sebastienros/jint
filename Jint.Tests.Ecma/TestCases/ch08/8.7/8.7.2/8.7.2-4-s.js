/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-4-s.js
 * @description Strict Mode - TypeError is thrown if LeftHandSide is a reference to an accessor property with no setter
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _8_7_2_4 = {};
        var _8_7_2_4_bValue = 1;
        Object.defineProperty(_8_7_2_4, "b", {
            get: function () { return _8_7_2_4_bValue; }
        });

        try {
            _8_7_2_4.b = 11;
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

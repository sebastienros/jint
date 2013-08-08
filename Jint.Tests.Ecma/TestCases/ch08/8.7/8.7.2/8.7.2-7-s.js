/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-7-s.js
 * @description Strict Mode - TypeError isn't thrown if LeftHandSide is a reference to an accessor property with setter
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _8_7_2_7 = {};
        var _8_7_2_7_bValue = 1;
        Object.defineProperty(_8_7_2_7, "b", {
            get: function () { return _8_7_2_7_bValue; },
            set: function (value) { _8_7_2_7_bValue = value; }
        });

        _8_7_2_7.b = 11;
        return _8_7_2_7.b === 11;
    }
runTestCase(testcase);

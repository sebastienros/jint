/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-6-s.js
 * @description Strict Mode - TypeError isn't thrown if LeftHandSide is a reference to a writable data property
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _8_7_2_6 = {};
        Object.defineProperty(_8_7_2_6, "b", {
            writable: true
        });

        _8_7_2_6.b = 11;

        return _8_7_2_6.b === 11;
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-8-s.js
 * @description Strict Mode - TypeError isn't thrown if LeftHandSide is a reference to a property of an extensible object
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _8_7_2_8 = {};

        _8_7_2_8.b = 11;

        return _8_7_2_8.b === 11;
    }
runTestCase(testcase);

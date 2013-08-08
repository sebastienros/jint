/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.5/10.5-7-b-2-s.js
 * @description Strict Mode - arguments object index assignment is allowed
 * @onlyStrict
 */


function testcase() {
        "use strict";

        function _10_5_7_b_2_fun() {
            arguments[7] = 12;
            return arguments[7] === 12;
        };

        return _10_5_7_b_2_fun(30);
    }
runTestCase(testcase);

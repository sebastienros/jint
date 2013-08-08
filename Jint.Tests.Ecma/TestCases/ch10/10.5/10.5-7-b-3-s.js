/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.5/10.5-7-b-3-s.js
 * @description Strict Mode - Adding property to the arguments object successful under strict mode 
 * @onlyStrict
 */


function testcase() {
        "use strict";

        function _10_5_7_b_3_fun() {
            arguments[1] = 12;
            return arguments[0] = 30 && arguments[1] === 12;
        };

        return _10_5_7_b_3_fun(30);
    }
runTestCase(testcase);

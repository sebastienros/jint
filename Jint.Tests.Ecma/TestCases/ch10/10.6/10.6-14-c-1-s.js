/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-14-c-1-s.js
 * @description Strict Mode - [[Enumerable]] attribute value in 'callee' is false under strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var argObj = function () {
            return arguments;
        } ();

        var verifyEnumerable = false;
        for (var _10_6_14_c_1 in argObj) {
            if (argObj.hasOwnProperty(_10_6_14_c_1) && _10_6_14_c_1 === "callee") {
                verifyEnumerable = true;
            }
        }
        return !verifyEnumerable && argObj.hasOwnProperty("callee");
    }
runTestCase(testcase);

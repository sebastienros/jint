/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.5/10.5-7-b-1-s.js
 * @description Strict Mode - arguments object is immutable in eval'ed functions
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("(function _10_5_7_b_1_fun() { arguments = 10;} ());");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);

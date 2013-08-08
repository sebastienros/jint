/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2.1-2-s.js
 * @description Strict Mode - Strict mode eval code cannot instantiate functions in the variable environment of the caller to eval
 * @onlyStrict
 */


function testcase() {
        "use strict";

        eval("function _10_4_2_1_2_fun(){}");
        return typeof _10_4_2_1_2_fun === "undefined";
    }
runTestCase(testcase);

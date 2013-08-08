/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2.1-4-s.js
 * @description Strict Mode - Strict mode eval code cannot instantiate functions in the variable environment of the caller to eval which is contained in strict mode code
 * @onlyStrict
 */


function testcase() {

        eval("'use strict'; function _10_4_2_1_4_fun(){}");
        return typeof _10_4_2_1_4_fun === "undefined";
    }
runTestCase(testcase);

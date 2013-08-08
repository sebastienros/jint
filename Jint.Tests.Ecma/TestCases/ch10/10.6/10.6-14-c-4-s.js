/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-14-c-4-s.js
 * @description Strict Mode - TypeError is thrown when accessing the [[Set]] attribute in 'callee' under strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var argObj = function () {
            return arguments;
        } ();

        try {
            argObj.callee = {};
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);

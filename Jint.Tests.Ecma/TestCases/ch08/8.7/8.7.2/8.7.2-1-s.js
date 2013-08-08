/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-1-s.js
 * @description Strict Mode - ReferenceError is thrown if LeftHandSide evaluates to an unresolvable Reference
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            eval("_8_7_2_1 = 11;");
            return false;
        } catch (e) {
            return e instanceof ReferenceError;
        }
    }
runTestCase(testcase);

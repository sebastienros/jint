/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-8-s.js
 * @description Strict Mode - ReferenceError is thrown if the LeftHandSideExpression of a Compound Assignment operator(>>>=) evaluates to an unresolvable reference
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            eval("_11_13_2_8 >>>= 1;");
            return false;
        } catch (e) {
            return e instanceof ReferenceError;
        }
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-23-s.js
 * @description Strict Mode - Function code of a FunctionExpression contains Use Strict Directive which appears in the middle of the block
 * @noStrict
 */


function testcase() {
        return function () {
            eval("var public = 1;");
            return public === 1;
            "use strict";
        } ();
    }
runTestCase(testcase);
